// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;

namespace HorizonSync
{
    internal sealed class AuthorInfo
    {
        internal string Name = "", Id = "";
        internal bool Failed;
    }

    internal sealed class WallpaperAuthors
    {
        internal static readonly WallpaperAuthors Shared = new WallpaperAuthors();
        readonly Func<Uri, string, CancellationToken, Task<string>> request;
        readonly Dictionary<string, AuthorInfo> cache = new Dictionary<string, AuthorInfo>();
        readonly object gate = new object();
        readonly int timeout;
        internal WallpaperAuthors(Func<Uri, string, CancellationToken, Task<string>> transport = null, int timeoutMs = 10000)
        { request = transport ?? Request; timeout = timeoutMs; }

        internal async Task<AuthorInfo> GetAsync(WallpaperItem item, bool retry, CancellationToken token)
        {
            if (!String.IsNullOrWhiteSpace(item.Author)) return new AuthorInfo { Name = item.Author, Id = item.AuthorId };
            string key = !String.IsNullOrEmpty(item.AuthorId) ? "author:" + item.AuthorId : "workshop:" + item.WorkshopId;
            if (key == "workshop:") return new AuthorInfo();
            lock (gate) { AuthorInfo hit; if (!retry && cache.TryGetValue(key, out hit)) return hit; }
            var result = new AuthorInfo { Id = item.AuthorId ?? "" };
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(timeout);
                try
                {
                    if (String.IsNullOrEmpty(result.Id))
                    {
                        string json = await request(new Uri("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/"),
                            "itemcount=1&publishedfileids%5B0%5D=" + item.WorkshopId, deadline.Token).ConfigureAwait(false);
                        result.Id = ReadCreator(json, item.WorkshopId);
                    }
                    if (String.IsNullOrEmpty(result.Id)) throw new InvalidDataException();
                    string xml = await request(new Uri("https://steamcommunity.com/profiles/" + result.Id + "/?xml=1"), null, deadline.Token).ConfigureAwait(false);
                    result.Name = ReadName(xml);
                    if (String.IsNullOrWhiteSpace(result.Name)) throw new InvalidDataException();
                }
                catch (Exception)
                {
                    token.ThrowIfCancellationRequested();
                    result.Failed = true;
                }
                token.ThrowIfCancellationRequested();
                lock (gate)
                {
                    // Cache failures too, until the user explicitly retries. Bound session metadata.
                    if (cache.Count >= 512) cache.Clear();
                    cache[key] = result;
                }
                return result;
            }
        }

        internal static string ReadCreator(string json, string workshopId)
        {
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            var response = data["response"] as Dictionary<string, object>;
            var items = response["publishedfiledetails"] as System.Collections.IEnumerable;
            if (items == null) throw new InvalidDataException();
            foreach (var raw in items)
            {
                var item = raw as Dictionary<string, object>;
                if (item == null || Convert.ToString(item["publishedfileid"], CultureInfo.InvariantCulture) != workshopId || Convert.ToInt32(item["result"]) != 1) continue;
                object app;
                if (item.TryGetValue("consumer_app_id", out app) && Convert.ToInt32(app) != 431960) continue;
                return WallpaperLibrary.NumericId(Convert.ToString(item["creator"], CultureInfo.InvariantCulture));
            }
            return "";
        }

        internal static string ReadName(string xml)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2 * 1024 * 1024 };
            using (var input = new StringReader(xml))
            using (var reader = XmlReader.Create(input, settings))
                while (reader.Read()) if (reader.NodeType == XmlNodeType.Element && reader.Name == "steamID") return reader.ReadElementContentAsString();
            return "";
        }

        static async Task<string> Request(Uri uri, string body, CancellationToken token)
        {
            var req = (HttpWebRequest)WebRequest.Create(uri);
            req.UserAgent = "HorizonSync/0.1"; req.AllowAutoRedirect = false;
            req.Method = body == null ? "GET" : "POST";
            using (token.Register(() => req.Abort()))
            {
                if (body != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(body);
                    req.ContentType = "application/x-www-form-urlencoded"; req.ContentLength = data.Length;
                    using (var output = await req.GetRequestStreamAsync().ConfigureAwait(false)) await output.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                }
                using (var response = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                using (var stream = response.GetResponseStream())
                using (var output = new MemoryStream())
                {
                    if ((int)response.StatusCode != 200) throw new WebException();
                    var buffer = new byte[8192]; int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                    { if (output.Length + read > 2 * 1024 * 1024) throw new InvalidDataException(); await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false); }
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
        }
    }
}
