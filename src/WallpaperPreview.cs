// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Imaging = System.Drawing.Imaging;

namespace HorizonSync
{
    internal static class ThumbnailLoader
    {
        internal static BitmapSource Load(string path, int width)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                int originalWidth = decoder.Frames[0].PixelWidth, originalHeight = decoder.Frames[0].PixelHeight;
                int decodeWidth = Math.Max(1, (int)(originalWidth * Math.Min(1, (double)width / Math.Max(originalWidth, originalHeight))));
                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = decodeWidth;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    }

    internal sealed class ThumbnailCache : IDisposable
    {
        sealed class Entry { internal string Key; internal BitmapSource Image; internal long Bytes; }
        readonly object gate = new object();
        readonly SemaphoreSlim decoders = new SemaphoreSlim(2);
        readonly Dictionary<string, LinkedListNode<Entry>> entries = new Dictionary<string, LinkedListNode<Entry>>(StringComparer.OrdinalIgnoreCase);
        readonly LinkedList<Entry> lru = new LinkedList<Entry>();
        readonly int maxCount;
        readonly long maxBytes;
        long bytes;
        bool disposed;
        internal int Count { get { lock (gate) return entries.Count; } }
        internal long Bytes { get { lock (gate) return bytes; } }
        internal ThumbnailCache(int count = 128, long budget = 24 * 1024 * 1024) { maxCount = count; maxBytes = budget; }

        internal async Task<BitmapSource> GetAsync(string path, int width, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(path)) return null;
            string key;
            try { var info = new FileInfo(path); key = info.FullName + "|" + width + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks; }
            catch (Exception) { return null; }
            lock (gate)
            {
                if (disposed) return null;
                LinkedListNode<Entry> hit;
                if (entries.TryGetValue(key, out hit)) { lru.Remove(hit); lru.AddFirst(hit); return hit.Value.Image; }
            }
            await decoders.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                BitmapSource image;
                try { image = await Task.Run(() => ThumbnailLoader.Load(path, width), token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { return null; }
                token.ThrowIfCancellationRequested();
                if (image == null) return null;
                long size = (long)image.PixelWidth * image.PixelHeight * 4;
                lock (gate)
                {
                    if (disposed) return null;
                    LinkedListNode<Entry> hit;
                    if (entries.TryGetValue(key, out hit)) return hit.Value.Image;
                    if (size <= maxBytes && maxCount > 0)
                    {
                        while (entries.Count >= maxCount || bytes + size > maxBytes)
                        { var last = lru.Last; bytes -= last.Value.Bytes; entries.Remove(last.Value.Key); lru.RemoveLast(); }
                        var entry = new Entry { Key = key, Image = image, Bytes = size };
                        entries.Add(key, lru.AddFirst(entry)); bytes += size;
                    }
                }
                return image;
            }
            finally { decoders.Release(); }
        }

        public void Dispose()
        {
            // Semaphore is intentionally not disposed while cancelled decodes unwind.
            lock (gate) { disposed = true; entries.Clear(); lru.Clear(); bytes = 0; }
        }
    }

    internal sealed class PreviewCoordinator : IDisposable
    {
        CancellationTokenSource current;
        Task completion = Task.FromResult(0);
        int generation;
        bool disposed;
        internal Task Completion { get { return completion; } }
        internal static int ActiveAnimations;
        internal static int PeakAnimations;
        internal const long GifFileLimit = 16 * 1024 * 1024;
        internal const long GifExpandedLimit = 64 * 1024 * 1024;

        internal void Show(string path, Action<BitmapSource, string> publish, Action<byte[], int, int> publishPixels = null, bool animate = true)
        {
            if (disposed) return;
            Stop();
            var next = new CancellationTokenSource(); current = next;
            int version = generation;
            completion = RunAfter(completion, path, next, animate, (image, message) =>
            { if (version == Volatile.Read(ref generation) && !next.IsCancellationRequested) publish(image, message); },
                publishPixels == null ? (Action<byte[], int, int>)null : (pixels, width, height) =>
                { if (version == Volatile.Read(ref generation) && !next.IsCancellationRequested) publishPixels(pixels, width, height); });
        }

        async Task RunAfter(Task previous, string path, CancellationTokenSource source, bool animate, Action<BitmapSource, string> publish, Action<byte[], int, int> publishPixels)
        {
            try
            {
                await previous.ConfigureAwait(false);
                source.Token.ThrowIfCancellationRequested();
                await Task.Run(() => Play(path, animate, publish, publishPixels, source.Token), source.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!source.IsCancellationRequested) publish(null, "预览不可用，仍可选择这张壁纸。"); }
            finally { source.Dispose(); }
        }

        internal void Stop()
        {
            Interlocked.Increment(ref generation);
            var source = current; current = null;
            if (source != null) try { source.Cancel(); } catch (ObjectDisposedException) { }
        }

        static void Static(string path, string message, Action<BitmapSource, string> publish, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            BitmapSource image = null;
            try { image = ThumbnailLoader.Load(path, 1280); } catch (Exception) { }
            token.ThrowIfCancellationRequested();
            publish(image, image == null ? "预览不可用，仍可选择这张壁纸。" : message);
        }

        internal static int[] Delays(byte[] data, int count)
        {
            var result = new int[count];
            for (int i = 0; i < count; i++) result[i] = data != null && data.Length >= (i + 1) * 4 ? (int)Math.Max(20L, Math.Min(60000L, (long)BitConverter.ToUInt32(data, i * 4) * 10)) : 100;
            return result;
        }

        internal static int FrameAt(long elapsed, int[] delays)
        {
            return FrameAt(elapsed, Timeline(delays));
        }
        static long[] Timeline(int[] delays)
        { var ends = new long[delays.Length]; long total = 0; for (int i = 0; i < delays.Length; i++) { total += delays[i]; ends[i] = total; } return ends; }
        static int FrameAt(long elapsed, long[] ends)
        { int found = Array.BinarySearch(ends, elapsed % ends[ends.Length - 1] + 1); return found >= 0 ? found : ~found; }

        static async Task Play(string path, bool animate, Action<BitmapSource, string> publish, Action<byte[], int, int> publishPixels, CancellationToken token)
        {
            if (!animate || String.IsNullOrWhiteSpace(path) || !String.Equals(System.IO.Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase))
            { Static(path, "", publish, token); return; }
            if (!File.Exists(path)) { Static(path, "", publish, token); return; }
            if (new FileInfo(path).Length > GifFileLimit)
            { Static(path, "动图超过预览预算，已显示静态首帧。", publish, token); return; }
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var image = Drawing.Image.FromStream(stream, false, false))
                {
                    token.ThrowIfCancellationRequested();
                    int count = image.GetFrameCount(Imaging.FrameDimension.Time);
                    if ((long)image.Width * image.Height * 4 * count > GifExpandedLimit)
                    { Static(path, "动图超过预览预算，已显示静态首帧。", publish, token); return; }
                    if (count <= 1) { Static(path, "", publish, token); return; }
                    byte[] delayData = null;
                    try { delayData = image.GetPropertyItem(0x5100).Value; } catch (ArgumentException) { }
                    long[] timeline = Timeline(Delays(delayData, count));
                    double scale = Math.Min(1, 1280.0 / Math.Max(image.Width, image.Height));
                    int width = Math.Max(1, (int)(image.Width * scale)), height = Math.Max(1, (int)(image.Height * scale));
                    int stride = width * 4;
                    var pixels = new byte[stride * height];
                    int active = Interlocked.Increment(ref ActiveAnimations);
                    if (active > PeakAnimations) PeakAnimations = active;
                    try
                    {
                        using (var canvas = new Drawing.Bitmap(width, height, Imaging.PixelFormat.Format32bppPArgb))
                        using (var graphics = Drawing.Graphics.FromImage(canvas))
                        {
                            var clock = Stopwatch.StartNew(); int last = -1;
                            while (true)
                            {
                                token.ThrowIfCancellationRequested();
                                int frame = FrameAt(clock.ElapsedMilliseconds, timeline);
                                if (frame != last)
                                {
                                    image.SelectActiveFrame(Imaging.FrameDimension.Time, frame);
                                    graphics.Clear(Drawing.Color.Transparent);
                                    graphics.DrawImage(image, 0, 0, width, height);
                                    var bits = canvas.LockBits(new Drawing.Rectangle(0, 0, width, height), Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppPArgb);
                                    try { Marshal.Copy(bits.Scan0, pixels, 0, pixels.Length); }
                                    finally { canvas.UnlockBits(bits); }
                                    token.ThrowIfCancellationRequested();
                                    if (publishPixels != null) publishPixels(pixels, width, height);
                                    else
                                    {
                                        var output = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
                                        output.Freeze(); publish(output, "");
                                    }
                                    last = frame;
                                }
                                await Task.Delay(34, token).ConfigureAwait(false);
                            }
                        }
                    }
                    finally { Interlocked.Decrement(ref ActiveAnimations); }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { Static(path, "动图无法播放，已显示静态首帧。", publish, token); }
        }

        public void Dispose() { if (disposed) return; disposed = true; Stop(); }
    }
}
