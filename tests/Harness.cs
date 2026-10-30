using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using HorizonSync;

class Harness
{
    static int count;
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
    static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, name); }
    static Settings Fixed(int day, int night, string zone)
    { return new Settings { FixedTime = true, DayMinute = day, NightMinute = night, TimeZoneId = zone }; }
    static DateTime Utc(int y, int m, int d, int h, int min) { return new DateTime(y,m,d,h,min,0,DateTimeKind.Utc); }
    static void Main(string[] args)
    {
        try { Run(args); }
        catch(Exception e) { Console.WriteLine("TEST ERROR: " + e.GetType().Name + ": " + e.Message); Environment.ExitCode=1; }
    }
    static void Run(string[] args)
    {
        var utc = TimeZoneInfo.Utc;
        var p = new Plan(Fixed(420,1140,"UTC"), Utc(2026,10,31,0,0));
        Check(!p.IsDay(Utc(2026,10,31,0,0)), "night across midnight");
        Check(!p.IsDay(Utc(2026,10,31,6,59)), "before day boundary");
        Check(p.IsDay(Utc(2026,10,31,7,0)), "exact day boundary");
        Check(!p.IsDay(Utc(2026,10,31,19,0)), "exact night boundary");
        Check(p.Next(Utc(2026,10,31,22,0)).Utc == Utc(2026,11,1,7,0), "next event crosses date");
        p = new Plan(Fixed(1200,360,"UTC"), Utc(2026,10,31,0,0));
        Check(p.IsDay(Utc(2026,10,31,0,0)) && !p.IsDay(Utc(2026,10,31,12,0)), "inverted custom interval");
        Reject(() => new Plan(Fixed(420,420,"UTC"), DateTime.UtcNow), "reject equal custom times");
        Reject(() => new Settings { Latitude = double.NaN, Longitude = 0 }.Validate(false), "reject NaN coordinate");
        Reject(() => new Settings { Latitude = 0, Longitude = double.PositiveInfinity }.Validate(false), "reject infinity coordinate");
        Reject(() => new Settings { Latitude = 91, Longitude = 0 }.Validate(false), "reject out-of-range coordinate");
        Reject(() => new Settings().Validate(false), "blank location blocks solar mode");
        var s = new Settings { Latitude = 12, Longitude = 34, TimeZoneId = TimeZoneInfo.Local.Id == "UTC" ? "New Zealand Standard Time" : "UTC" };
        DateTime before = DateTime.UtcNow; DateTime sampled = SystemState.RefreshClock(s);
        Check(s.TimeZoneId == TimeZoneInfo.Local.Id && sampled >= before && sampled <= DateTime.UtcNow, "system clock and timezone take precedence");
        Check(SystemState.ApplyCoordinates(s,-36.85,174.76) && s.Latitude == -36.85 && s.Longitude == 174.76, "system location replaces saved location");
        Check(!SystemState.ApplyCoordinates(s,null,null) && s.Latitude == -36.85 && s.Longitude == 174.76, "unavailable system location preserves fallback");
        Check(!SystemState.ApplyCoordinates(s,double.NaN,0) && s.Latitude == -36.85, "invalid system location cannot overwrite fallback");
        var nz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        DateTime nzStart = Plan.ToUtc(new DateTime(2026,9,27),nz), nzEnd = Plan.ToUtc(new DateTime(2026,9,28),nz);
        Check((nzEnd-nzStart).TotalHours == 23, "NZ spring DST day has 23 hours");
        p = new Plan(Fixed(150,1140,nz.Id),nzStart);
        Check(p.Switches.Any(x => x.Utc == Utc(2026,9,26,14,0) && x.Day), "DST invalid 02:30 advances to 03:00");
        var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTime nyStart = Plan.ToUtc(new DateTime(2026,11,1),ny), nyEnd = Plan.ToUtc(new DateTime(2026,11,2),ny);
        Check((nyEnd-nyStart).TotalHours == 25, "US autumn DST day has 25 hours");
        Check(Plan.ToUtc(new DateTime(2026,11,1,1,30,0),ny) == Utc(2026,11,1,6,30), "repeated custom hour chooses standard time");
        s = new Settings { Latitude = -36.85, Longitude = 174.76, TimeZoneId = nz.Id };
        p = new Plan(s,Utc(2026,10,31,0,0));
        var dawn = p.Raw.First(x => x.Day && x.Utc > Utc(2026,10,30,12,0));
        s.SunriseOffset = -30; var shifted = new Plan(s,Utc(2026,10,31,0,0));
        Check(!p.IsDay(dawn.Utc.AddMinutes(-10)) && shifted.IsDay(dawn.Utc.AddMinutes(-10)), "negative sunrise offset activates early");
        s.SunriseOffset = 0; s.SunsetOffset = 45; shifted = new Plan(s,Utc(2026,10,31,0,0));
        var dusk = p.Raw.First(x => !x.Day && x.Utc > Utc(2026,10,30,12,0));
        Check(!p.IsDay(dusk.Utc.AddMinutes(10)) && shifted.IsDay(dusk.Utc.AddMinutes(10)), "positive sunset offset delays night");
        var summer = new Plan(new Settings { Latitude = 69.65, Longitude = 18.96, TimeZoneId = "W. Europe Standard Time" }, Utc(2026,6,21,12,0));
        var winter = new Plan(new Settings { Latitude = 69.65, Longitude = 18.96, TimeZoneId = "W. Europe Standard Time" }, Utc(2026,12,21,12,0));
        Check(summer.Switches.Count == 0 && summer.IsDay(Utc(2026,6,21,12,0)), "polar summer stays day");
        Check(winter.Switches.Count == 0 && !winter.IsDay(Utc(2026,12,21,12,0)), "polar winter stays night");
        Reject(() => new Plan(new Settings { Latitude = 60, Longitude = 0, TimeZoneId = "UTC", SunriseOffset = -180, SunsetOffset = 180 }, Utc(2026,6,21,12,0)), "reject overlapping offsets");
        var noOffset = new Settings { Latitude = 0, Longitude = 0, TimeZoneId = "UTC" };
        p = new Plan(noOffset, Utc(2026,10,31,12,0));
        Check(p.Switches.Count >= 12 && p.Switches.Zip(p.Switches.Skip(1),(a,b) => a.Utc < b.Utc && a.Day != b.Day).All(x=>x), "solar events alternate in chronological order");
        Check(Engine.Quote("a b") == "\"a b\"", "quote spaces");
        Check(Engine.Quote("a\\") == "\"a\\\\\"", "quote trailing backslash");
        Check(Engine.Quote("a\"b") == "\"a\\\"b\"", "quote embedded quote");
        Check(Engine.Libraries() != null, "library discovery tolerates denied registry access");
        Check(!Engine.IsValidExecutable(""), "blank installation cannot be ready");
        Check(!Engine.IsValidExecutable(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"missing","wallpaper64.exe")), "missing cached executable is invalid");
        string profile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"profile");
        Store.Initialize(profile);
        Check(Store.Root == Path.GetFullPath(profile), "explicit profile stays separate from installation");
        Store.Save(new Settings { Latitude = 12, Longitude = 34 });
        Store.Save(new Settings { Latitude = -12, Longitude = -34 });
        Check(Store.Load().Latitude == -12 && File.Exists(Store.ConfigPath + ".bak"), "atomic settings roundtrip keeps previous backup");
        File.WriteAllText(Store.ConfigPath,"<broken>");
        Check(!Store.Load().Enabled && Store.LoadError != null && File.ReadAllText(Store.ConfigPath) == "<broken>", "corrupt settings are preserved and automation stops");
        Store.Save(new Settings { Latitude = 23, Longitude = 45 });
        Check(Directory.GetFiles(profile,"settings.xml.corrupt-*").Length > 0 && Store.Load().Latitude == 23 && Store.LoadError == null, "explicit repair preserves corrupt original and clears load error");
        int corruptCopies = Directory.GetFiles(profile,"settings.xml.corrupt-*").Length;
        for(int i=0;i<2;i++){File.WriteAllText(Store.ConfigPath,"<broken>");Store.Load();Store.Save(new Settings{Latitude=1,Longitude=2});}
        Check(Directory.GetFiles(profile,"settings.xml.corrupt-*").Length == corruptCopies + 2,"rapid repairs never collide or overwrite preserved originals");
        File.WriteAllText(Store.ConfigPath,"<Settings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />");
        Check(!Store.Load().Enabled && Store.LoadError != null, "nil settings stop safely");
        Store.Save(new Settings { SchemaVersion = 99 });
        Check(!Store.Load().Enabled && Store.LoadError != null, "future config schema stops safely");
        Store.Save(new Settings { Latitude = 0, Longitude = 0 });
        Localization.Use("en");
        Check(Localization.T("保存设置") == "Save settings", "English catalog loads from packaged resource");
        bool englishValidation=false;
        try{new Settings().Validate(false);}catch(Exception e){englishValidation=e.Message.StartsWith("Enter a valid latitude");}
        Check(englishValidation,"validation errors follow the selected language");
        new Settings { Language="en",Enabled=false }.Validate(false,false);
        Check(true,"language and disabled draft can save before location setup");
        Store.Save(new Settings { Language="en" });
        Check(Store.Load().Language=="en","language preference persists with configuration");
        Localization.Use("zh-CN");
        Check(Localization.T("保存设置")=="保存设置","Chinese can be restored without changing user data");
        Localization.Use("auto");
        if (args.Length >= 1)
        {
            string root = Path.GetDirectoryName(args[0]);
            string json = Path.Combine(root,"中文 path & $(literal)","project.json"); Directory.CreateDirectory(Path.GetDirectoryName(json)); File.WriteAllText(json,"{}");
            var mockSettings = new Settings { FixedTime=true, EnginePath=args[0], DayPath=json, NightPath=json, Monitor=3 };
            using (var host = Process.Start(new ProcessStartInfo(args[0]) { UseShellExecute=false,CreateNoWindow=true }))
            {
                try
                {
                    for(int attempt=0;attempt<30 && Engine.RunningIdentity(args[0])==null;attempt++) System.Threading.Thread.Sleep(100);
                    Engine.Apply(mockSettings,true);
                    var actual = File.ReadAllLines(Path.Combine(root,"args.txt"));
                    Check(actual.SequenceEqual(new [] { "-control","openWallpaper","-file",json,"-monitor","3" }), "CLI preserves Unicode, spaces and shell metacharacters as one file argument");
                }
                finally { if (!host.HasExited) host.Kill(); host.WaitForExit(); }
            }
            Check(Engine.RunningIdentity(args[0]) == null, "engine restart detection clears exited instance");
            Reject(() => Engine.Apply(mockSettings,true), "do not launch engine when it is stopped");
        }
        string export = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"solar-events.tsv");
        using (var f = new StreamWriter(export))
        {
            f.WriteLine("latitude\tlongitude\tday\tutc");
            var places = new double[,] { {-36.85,174.76},{40.71,-74},{27.72,85.32},{0,0},{-33.87,151.21},{69.65,18.96},{-45.03,168.66},{21.3,-157.8} };
            foreach (int month in new [] {3,6,9,12}) for(int i=0;i<places.GetLength(0);i++)
                foreach(var ev in Solar.Events(Utc(2026,month,21,0,0), Utc(2026,month,22,0,0),places[i,0],places[i,1]))
                    f.WriteLine(places[i,0].ToString(CultureInfo.InvariantCulture)+"\t"+places[i,1].ToString(CultureInfo.InvariantCulture)+"\t"+ev.Day+"\t"+ev.Utc.ToString("o"));
        }
        Console.WriteLine("ALL PASSED: " + count);
    }
}
