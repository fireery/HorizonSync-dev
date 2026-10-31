// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using HorizonSync;
using Localization = HorizonSync.Localization;

class PickerHarness
{
    static int count;
    static readonly string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "picker-fixtures");
    static void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); count++; Console.WriteLine("PASS: " + label); }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(root); Fixtures();
            if (args.Contains("--ui")) { ShowUi(args); return; }
            if (args.Contains("--installed")) { Installed(); return; }
            Run().GetAwaiter().GetResult(); Console.WriteLine("PICKER ALL PASSED: " + count);
        }
        catch (Exception e) { Console.WriteLine(e); Environment.ExitCode = 1; }
    }
    static string FileAt(string name) { return Path.Combine(root, name); }
    static async Task<bool> Within(Task ready)
    { using(var stop=new CancellationTokenSource()) { var timeout=Task.Delay(3000,stop.Token);bool completed=await Task.WhenAny(ready,timeout)==ready;stop.Cancel();return completed; } }
    static void Installed()
    {
        var scan=WallpaperLibrary.Scan(WallpaperLibrary.Roots(Engine.Find()),CancellationToken.None);
        int decoded=0,missing=0;
        foreach(var item in scan.Items)try{if(ThumbnailLoader.Load(item.PreviewPath,360)!=null)decoded++;else missing++;}catch(Exception){missing++;}
        Console.WriteLine("INSTALLED items="+scan.Items.Count+" decoded="+decoded+" missing="+missing+" metadataErrors="+scan.Errors);
        Check(scan.Items.Count>0 && decoded>0,"real installed library has decodable previews");
    }
    static void Fixtures()
    {
        var bytes = new List<byte>(System.Text.Encoding.ASCII.GetBytes("GIF89a"));
        // Nearly identical muted greens: never put high-contrast flashing fixtures on a desktop.
        bytes.AddRange(new byte[] { 1,0,1,0,128,0,0,91,126,120,94,129,123 });
        bytes.AddRange(new byte[] { 33,255,11 }); bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0")); bytes.AddRange(new byte[] { 3,1,0,0,0 });
        for (int i = 0; i < 2; i++) bytes.AddRange(new byte[] { 33,249,4,4,(byte)(10+i*10),0,0,0,44,0,0,0,0,1,0,1,0,0,2,2,(byte)(68+i*8),1,0 });
        bytes.Add(59); File.WriteAllBytes(FileAt("animated.gif"), bytes.ToArray());
        byte[] pixels = new byte[320 * 180 * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 130; pixels[i+1] = 110; pixels[i+2] = 40; pixels[i+3] = (byte)(i % 8 == 0 ? 255 : 0); }
        var bitmap = BitmapSource.Create(320,180,96,96,PixelFormats.Bgra32,null,pixels,320*4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output=File.Create(FileAt("transparent.png"))) encoder.Save(output);
        File.WriteAllText(FileAt("broken.jpg"),"broken image");
        string library = FileAt("library"); Directory.CreateDirectory(library);
        for (int i=0;i<500;i++)
        {
            string dir=Path.Combine(library,i.ToString("D4")); Directory.CreateDirectory(dir);
            File.Copy(FileAt(i%2==0 ? "animated.gif" : "transparent.png"),Path.Combine(dir,i%2==0 ? "preview.gif" : "preview.png"),true);
            File.WriteAllText(Path.Combine(dir,"project.json"),"{\"title\":\""+(i==0 ? "中文与很长的标题 Long wallpaper title with extra words to check truncation " : "Wallpaper ")+i+"\",\"type\":\"scene\",\"author\":\"Fixture author\",\"preview\":\""+(i%2==0 ? "preview.gif" : "preview.png")+"\"}");
        }
    }
    static void ShowUi(string[] args)
    {
        Localization.Use(args.Contains("--en") ? "en" : "zh-CN");
        var app=new Application(); var owner=new Window { Title="HorizonSync picker validation", Width=1100,Height=800,FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI") };
        FrameworkElement view;
        using(var stream=typeof(PickerHarness).Assembly.GetManifestResourceStream("HorizonSync.MainWindow.xaml")) view=(FrameworkElement)XamlReader.Load(stream);
        owner.Content=new System.Windows.Controls.TextBlock{Text="Isolated picker validation — no desktop switching",Margin=new Thickness(30)};
        owner.Loaded+=delegate {
            var picker=new WallpaperPicker(owner,Engine.Find(),"",true,view.Resources,args.Contains("--fixture") ? new[]{FileAt("library")} : null);
            if(args.Contains("--fixture"))picker.Title+=" · Synthetic test library";
            var diagnostics=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
            diagnostics.Tick+=delegate { Console.WriteLine("UI realizedRows="+picker.RealizedRows+" activeAnimations="+PreviewCoordinator.ActiveAnimations+" focused="+Keyboard.FocusedElement+" logical="+FocusManager.GetFocusedElement(picker)+" active="+picker.IsActive); };
            diagnostics.Start();
            picker.Closed+=delegate {diagnostics.Stop();Console.WriteLine("Selected path: "+picker.SelectedPath); owner.Close();};
            picker.ShowDialog();
        };
        app.Run(owner);
    }
    static async Task Run()
    {
        Localization.Use("zh-CN");
        string library=FileAt("library");
        var scan=WallpaperLibrary.Scan(new[]{library,library.ToUpperInvariant()},CancellationToken.None);
        Check(scan.Items.Count==500,"500 item scan deduplicates normalized project paths");
        Check(scan.Items[0].Author=="Fixture author" && scan.Items.All(x=>x.Path==x.ProjectPath),"local author and scene command paths retained");
        string special=FileAt("special");Directory.CreateDirectory(special);
        string video=Path.Combine(special,"video");Directory.CreateDirectory(video);
        File.WriteAllText(Path.Combine(video,"movie.mp4"),"");
        File.WriteAllText(Path.Combine(video,"project.json"),"{\"title\":\"same\",\"type\":\"video\",\"file\":\"movie.mp4\",\"preview\":\"../../escape.jpg\"}");
        string web=Path.Combine(special,"web");Directory.CreateDirectory(web);File.WriteAllText(Path.Combine(web,"index.html"),"");
        File.WriteAllText(Path.Combine(web,"project.json"),"{\"title\":\"same\",\"type\":\"web\",\"file\":\"index.html\"}");
        string bad=Path.Combine(special,"bad");Directory.CreateDirectory(bad);File.WriteAllText(Path.Combine(bad,"project.json"),"{");
        scan=WallpaperLibrary.Scan(new[]{special},CancellationToken.None);
        Check(scan.Items.Count==2 && scan.Items.Any(x=>x.Path.EndsWith("movie.mp4")) && scan.Items.Any(x=>x.Path.EndsWith("index.html")),"same names remain distinct; video and web use entry files");
        Check(scan.Errors==1 && scan.Items.All(x=>String.IsNullOrEmpty(x.PreviewPath)),"corrupt metadata isolated and preview traversal rejected");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();
        bool rejected=false;try{WallpaperLibrary.Scan(new[]{library},cancelled.Token);}catch(OperationCanceledException){rejected=true;}
        Check(rejected,"scan honours cancellation");cancelled.Dispose();
        var png=ThumbnailLoader.Load(FileAt("transparent.png"),160);
        Check(png.PixelWidth==160 && png.PixelHeight==90 && png.IsFrozen,"thumbnail scaled, frozen and aspect preserved");
        var transparent=ThumbnailLoader.Load(FileAt("transparent.png"),320);
        byte[] alpha=new byte[320*180*4];transparent.CopyPixels(alpha,320*4,0);
        Check(alpha.Where((value,index)=>index%4==3).Any(value=>value==0),"transparent image alpha preserved");
        Check(ThumbnailLoader.Load(FileAt("animated.gif"),360)!=null,"GIF first frame decodes");
        using(var exclusive=File.Open(FileAt("transparent.png"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) Check(exclusive.Length>0,"static decode releases file handle");
        using(var cache=new ThumbnailCache(2,320*180*4))
        {
            await cache.GetAsync(FileAt("transparent.png"),160,CancellationToken.None);
            await cache.GetAsync(FileAt("transparent.png"),150,CancellationToken.None);
            await cache.GetAsync(FileAt("transparent.png"),140,CancellationToken.None);
            Check(cache.Count<=2 && cache.Bytes<=320*180*4,"LRU respects count and byte budgets");
            Check(await cache.GetAsync(FileAt("broken.jpg"),100,CancellationToken.None)==null,"broken image degrades to placeholder");
            Check(await cache.GetAsync(FileAt("missing.png"),100,CancellationToken.None)==null,"missing preview degrades to placeholder");
            cache.Dispose();Check(cache.Count==0 && cache.Bytes==0,"cache disposal drops all retained images");
        }
        using(var cache=new ThumbnailCache(128,100)) { await cache.GetAsync(FileAt("transparent.png"),160,CancellationToken.None);Check(cache.Count==0 && cache.Bytes==0,"single oversize thumbnail is never cached"); }
        using(var cache=new ThumbnailCache(128,30000))
        {
            foreach(int size in new[]{80,70,60})await cache.GetAsync(FileAt("transparent.png"),size,CancellationToken.None);
            Check(cache.Count==2 && cache.Bytes<=30000,"byte budget evicts independently of entry count");
        }
        Check(PreviewCoordinator.FrameAt(0,new[]{100,200})==0 && PreviewCoordinator.FrameAt(100,new[]{100,200})==1 && PreviewCoordinator.FrameAt(300,new[]{100,200})==0,"GIF delay boundaries and loop time");
        Check(PreviewCoordinator.Delays(new byte[]{0,0,0,0},1)[0]==20,"zero GIF delay bounded");
        using(var coordinator=new PreviewCoordinator())
        {
            var colors=new List<byte>();var looped=new TaskCompletionSource<bool>();
            byte[] firstBuffer=null;bool reused=true;
            coordinator.Show(FileAt("animated.gif"),(image,message)=>{},(pixels,width,height)=>{
                if(firstBuffer==null)firstBuffer=pixels;else if(!Object.ReferenceEquals(firstBuffer,pixels))reused=false;
                lock(colors){if(colors.Count==0 || colors[colors.Count-1]!=pixels[2])colors.Add(pixels[2]);if(colors.Count>=3)looped.TrySetResult(true);}
            });
            Check(await Within(looped.Task),"real GIF frames change colour and loop");
            coordinator.Stop();await coordinator.Completion;
            Check(reused,"animated preview reuses pixel buffer between frames");
            BitmapSource pausedImage=null;coordinator.Show(FileAt("animated.gif"),(image,message)=>pausedImage=image,null,false);await coordinator.Completion;
            Check(pausedImage!=null && PreviewCoordinator.ActiveAnimations==0,"pause releases decoder and shows static first frame");
            for(int batch=0;batch<4;batch++)
            {
                for(int i=0;i<50;i++)
                {
                    var frame=new TaskCompletionSource<bool>();
                    coordinator.Show(FileAt("animated.gif"),(image,message)=>{},(pixels,width,height)=>frame.TrySetResult(true));
                    if(!await Within(frame.Task)) throw new Exception("GIF never rendered");
                    coordinator.Stop();await coordinator.Completion;
                    if(PreviewCoordinator.ActiveAnimations!=0)throw new Exception("Preview did not release decoder");
                }
                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect(); // Measurement only; never in application code.
                using(var process=Process.GetCurrentProcess()) Console.WriteLine("MEMORY batch="+(batch+1)+" managed="+GC.GetTotalMemory(false)+" private="+process.PrivateMemorySize64+" handles="+process.HandleCount);
            }
            Check(PreviewCoordinator.ActiveAnimations==0 && PreviewCoordinator.PeakAnimations==1,"200 actual previews released; at most one animation");
            using(var exclusive=File.Open(FileAt("animated.gif"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) Check(exclusive.Length>0,"GIF stream released after cancellation");
            int stale=0;coordinator.Show(FileAt("animated.gif"),(image,message)=>Interlocked.Increment(ref stale));
            coordinator.Stop();await coordinator.Completion;int after=stale;await Task.Delay(60);Check(stale==after,"no frames arrive after stopped session drains");
            for(int i=0;i<200;i++)coordinator.Show(FileAt("animated.gif"),(image,message)=>{});
            coordinator.Stop();await coordinator.Completion;Check(PreviewCoordinator.PeakAnimations==1,"rapid replacement serializes decoder ownership");
            string big=FileAt("too-large.gif");File.Copy(FileAt("animated.gif"),big,true);using(var stream=File.OpenWrite(big)){stream.SetLength(PreviewCoordinator.GifFileLimit+1);}
            string note="";BitmapSource fallback=null;coordinator.Show(big,(image,message)=>{note=message;fallback=image;});await coordinator.Completion;
            Check(note.Contains("预算") && fallback!=null && PreviewCoordinator.ActiveAnimations==0,"valid oversize GIF falls back to first frame without animation");
            var expanded=File.ReadAllBytes(FileAt("animated.gif"));expanded[6]=0;expanded[7]=16;expanded[8]=0;expanded[9]=16;
            File.WriteAllBytes(FileAt("expanded.gif"),expanded);
            coordinator.Show(FileAt("expanded.gif"),(image,message)=>note=message);await coordinator.Completion;
            Check(note.Contains("预算") && PreviewCoordinator.ActiveAnimations==0,"expanded GIF frame budget enforced");
        }
        await Authors();
    }
    static async Task Authors()
    {
        int requests=0;
        Func<Uri,string,CancellationToken,Task<string>> transport=(uri,body,token)=>{
            requests++;return Task.FromResult(body!=null ? "{\"response\":{\"publishedfiledetails\":[{\"publishedfileid\":\"123\",\"result\":1,\"consumer_app_id\":431960,\"creator\":\"76561198000000000\"}]}}" : "<profile><steamID><![CDATA[作者 & Name]]></steamID></profile>");};
        var service=new WallpaperAuthors(transport);var item=new WallpaperItem{WorkshopId="123"};
        var author=await service.GetAsync(item,false,CancellationToken.None);
        Check(author.Name=="作者 & Name" && !author.Failed && requests==2,"author enrichment parses API and profile XML");
        await service.GetAsync(item,false,CancellationToken.None);Check(requests==2,"author success cached for session");
        await service.GetAsync(item,true,CancellationToken.None);Check(requests==4,"explicit author retry bypasses cache");
        await service.GetAsync(new WallpaperItem{Author="Local",AuthorId="76561198000000000"},false,CancellationToken.None);Check(requests==4,"local author never makes network request");
        service=new WallpaperAuthors((uri,body,token)=>{requests++;throw new System.Net.WebException("429");});requests=0;
        Check((await service.GetAsync(item,false,CancellationToken.None)).Failed,"rate limiting cannot block selection");
        await service.GetAsync(item,false,CancellationToken.None);Check(requests==1,"failed lookup does not automatically repeat");
        service=new WallpaperAuthors(async(uri,body,token)=>{await Task.Delay(1000,token);return "";},20);
        var result=await service.GetAsync(new WallpaperItem{AuthorId="76561198000000000"},false,CancellationToken.None);
        Check(result.Failed && result.Id=="76561198000000000","deadline retains author ID fallback");
        using(var stop=new CancellationTokenSource())
        { stop.Cancel();bool canceled=false;try{await service.GetAsync(item,false,stop.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"author selection cancellation propagated"); }
        Check(WallpaperAuthors.ReadName("<profile><error>private</error></profile>")=="","private profile has unknown author");
        bool rejected=false;try{WallpaperAuthors.ReadName("<!DOCTYPE profile [<!ENTITY x SYSTEM 'file:///none'>]><profile><steamID>&x;</steamID></profile>");}catch(System.Xml.XmlException){rejected=true;}
        Check(rejected,"profile XML external entities prohibited");
    }
}
