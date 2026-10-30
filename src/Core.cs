using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace HorizonSync
{
    public class Settings
    {
        public int SchemaVersion = 1;
        public string Language = "auto";
        public bool Enabled;
        public bool FixedTime;
        public double? Latitude;
        public double? Longitude;
        public string TimeZoneId = TimeZoneInfo.Local.Id;
        public bool CheckSystemLocation = true;
        public int SunriseOffset;
        public int SunsetOffset;
        public int DayMinute = 420;
        public int NightMinute = 1140;
        public string EnginePath = "";
        public string DayPath = "";
        public string NightPath = "";
        public int Monitor;

        public void Validate(bool files, bool requireSchedule = true)
        {
            if (SchemaVersion != 1) throw new Exception(Localization.T("配置来自不支持的版本，请更新 HorizonSync。"));
            TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            if (!FixedTime && (requireSchedule || Latitude.HasValue || Longitude.HasValue) && (!Latitude.HasValue || !Longitude.HasValue ||
                double.IsNaN(Latitude.Value) || double.IsNaN(Longitude.Value) ||
                Math.Abs(Latitude.Value) > 90 || Math.Abs(Longitude.Value) > 180))
                throw new Exception(Localization.T("请输入有效经纬度：纬度 -90 至 90，经度 -180 至 180。南纬、西经使用负数。"));
            if (Math.Abs((long)SunriseOffset) > 180 || Math.Abs((long)SunsetOffset) > 180)
                throw new Exception(Localization.T("时间偏移需在 -180 至 180 分钟之间。"));
            if (DayMinute < 0 || DayMinute > 1439 || NightMinute < 0 || NightMinute > 1439 ||
                (FixedTime && DayMinute == NightMinute)) throw new Exception(Localization.T("白天与夜晚时间必须有效且不同。"));
            if (Monitor < 0 || Monitor > 31) throw new Exception(Localization.T("显示器编号需在 0 至 31 之间。"));
            if (!files) return;
            if (!File.Exists(EnginePath) || !Regex.IsMatch(Path.GetFileName(EnginePath), "^wallpaper(32|64)\\.exe$", RegexOptions.IgnoreCase))
                throw new Exception(Localization.T("请选择 Wallpaper Engine 安装目录中的 wallpaper32.exe 或 wallpaper64.exe。"));
            if (!File.Exists(DayPath) || !File.Exists(NightPath)) throw new Exception(Localization.T("请分别选择存在的白天和夜晚壁纸文件。"));
            foreach (string path in new [] { DayPath, NightPath })
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (!new [] { ".json", ".mp4", ".webm", ".html", ".htm", ".pkg", ".avi", ".mkv", ".wmv", ".gif", ".jpg", ".jpeg", ".png" }.Contains(ext))
                    throw new Exception(Localization.T("不支持的壁纸文件类型：") + ext + Localization.T("。场景壁纸请选择 project.json。"));
            }
        }
    }

    public static class Store
    {
        static string root = AppDomain.CurrentDomain.BaseDirectory;
        public static string Root { get { return root; } }
        public static string ConfigPath { get { return Path.Combine(Root, "settings.xml"); } }
        public static string LoadError;
        public static void Initialize(string directory = null)
        {
            root = Path.GetFullPath(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HorizonSync"));
            Directory.CreateDirectory(root);
        }
        public static Settings Load()
        {
            if (!File.Exists(ConfigPath)) return new Settings();
            try
            {
                Settings result;
                using (var f = File.OpenRead(ConfigPath)) result = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);
                if (result == null || result.SchemaVersion != 1) throw new InvalidDataException(Localization.T("配置格式或版本不受支持。"));
                return result;
            }
            catch (Exception e) { LoadError = Localization.T("配置读取失败，自动切换已关闭。原文件未修改：") + e.Message; return new Settings(); }
        }
        public static void Save(Settings settings)
        {
            string temp = ConfigPath + ".tmp";
            using (var f = File.Create(temp)) new XmlSerializer(typeof(Settings)).Serialize(f, settings);
            if (File.Exists(ConfigPath) && LoadError != null)
            {
                File.Copy(ConfigPath, ConfigPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
                File.Replace(temp, ConfigPath, null);
            }
            else if (File.Exists(ConfigPath)) File.Replace(temp, ConfigPath, ConfigPath + ".bak");
            else File.Move(temp, ConfigPath);
            LoadError = null;
        }
        public static void Log(string message)
        {
            try
            {
                string log = Path.Combine(Root, "activity.log");
                if (File.Exists(log) && new FileInfo(log).Length > 512 * 1024) File.Copy(log, log + ".previous", true);
                if (File.Exists(log) && new FileInfo(log).Length > 512 * 1024) File.WriteAllText(log, "");
                File.AppendAllText(log, DateTimeOffset.Now.ToString("o") + " " + message + Environment.NewLine, Encoding.UTF8);
            } catch { }
        }
    }

    public static class SystemState
    {
        public static DateTime RefreshClock(Settings s)
        {
            TimeZoneInfo.ClearCachedData();
            s.TimeZoneId = TimeZoneInfo.Local.Id;
            return DateTime.UtcNow;
        }
        public static bool ApplyCoordinates(Settings s, double? latitude, double? longitude)
        {
            if (!latitude.HasValue || !longitude.HasValue || double.IsNaN(latitude.Value) || double.IsNaN(longitude.Value) ||
                Math.Abs(latitude.Value) > 90 || Math.Abs(longitude.Value) > 180) return false;
            s.Latitude = latitude; s.Longitude = longitude; return true;
        }
    }

    public class SolarEvent
    {
        public DateTime Utc;
        public bool Day;
        public SolarEvent(DateTime utc, bool day) { Utc = utc; Day = day; }
    }

    // NOAA/Meeus solar coordinates. Horizon is the standard apparent sunrise altitude -0.833 degrees.
    // https://gml.noaa.gov/grad/solcalc/calcdetails.html
    public static class Solar
    {
        static double Rad(double x) { return x * Math.PI / 180; }
        static double Deg(double x) { return x * 180 / Math.PI; }
        static double Sin(double x) { return Math.Sin(Rad(x)); }
        static double Cos(double x) { return Math.Cos(Rad(x)); }
        static double Norm(double x) { return (x % 360 + 360) % 360; }
        public static double Horizon(DateTime utc, double latitude, double longitude)
        {
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            double jd = (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays + 2451545;
            double t = (jd - 2451545) / 36525;
            double l = Norm(280.46646 + t * (36000.76983 + t * .0003032));
            double m = 357.52911 + t * (35999.05029 - .0001537 * t);
            double e = .016708634 - t * (.000042037 + .0000001267 * t);
            double c = Sin(m) * (1.914602 - t * (.004817 + .000014 * t)) + Sin(2 * m) * (.019993 - .000101 * t) + Sin(3 * m) * .000289;
            double omega = 125.04 - 1934.136 * t;
            double apparent = l + c - .00569 - .00478 * Sin(omega);
            double obliq = 23 + (26 + (21.448 - t * (46.815 + t * (.00059 - t * .001813))) / 60) / 60 + .00256 * Cos(omega);
            double decl = Deg(Math.Asin(Sin(obliq) * Sin(apparent)));
            double y = Math.Pow(Math.Tan(Rad(obliq / 2)), 2);
            double eq = 4 * Deg(y * Sin(2 * l) - 2 * e * Sin(m) + 4 * e * y * Sin(m) * Cos(2 * l) - .5 * y * y * Sin(4 * l) - 1.25 * e * e * Sin(2 * m));
            double minutes = (utc.TimeOfDay.TotalMinutes + eq + 4 * longitude) % 1440;
            if (minutes < 0) minutes += 1440;
            double hour = minutes / 4 - 180;
            double altitude = Deg(Math.Asin(Math.Max(-1, Math.Min(1, Sin(latitude) * Sin(decl) + Cos(latitude) * Cos(decl) * Cos(hour)))));
            return altitude + .833;
        }

        // Scan UTC, not local clock hours: a local date may have 23 or 25 hours at DST changes.
        public static List<SolarEvent> Events(DateTime fromUtc, DateTime toUtc, double latitude, double longitude)
        {
            var result = new List<SolarEvent>();
            DateTime a = fromUtc;
            double fa = Horizon(a, latitude, longitude);
            while (a < toUtc)
            {
                DateTime b = a.AddMinutes(5); if (b > toUtc) b = toUtc;
                double fb = Horizon(b, latitude, longitude);
                if ((fa < 0 && fb >= 0) || (fa >= 0 && fb < 0))
                {
                    DateTime lo = a, hi = b;
                    for (int i = 0; i < 20; i++)
                    {
                        DateTime mid = lo.AddTicks((hi.Ticks - lo.Ticks) / 2);
                        if ((Horizon(mid, latitude, longitude) >= 0) == (fa >= 0)) lo = mid; else hi = mid;
                    }
                    result.Add(new SolarEvent(hi, fb >= 0));
                }
                a = b; fa = fb;
            }
            return result;
        }
    }

    public class Plan
    {
        public List<SolarEvent> Raw;
        public List<SolarEvent> Switches;
        public Settings Settings;
        public DateTime LocalDate;
        public TimeZoneInfo Zone;
        public static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
        {
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            // In a DST gap, use the first valid local minute. For repeated hours choose standard time.
            while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
            return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }
        public Plan(Settings s, DateTime utc)
        {
            s.Validate(false); Settings = s;
            Zone = TimeZoneInfo.FindSystemTimeZoneById(s.TimeZoneId);
            LocalDate = TimeZoneInfo.ConvertTimeFromUtc(utc, Zone).Date;
            Raw = new List<SolarEvent>();
            if (s.FixedTime)
            {
                for (int i = -3; i <= 3; i++)
                {
                    DateTime date = LocalDate.AddDays(i);
                    Raw.Add(new SolarEvent(ToUtc(date.AddMinutes(s.DayMinute), Zone), true));
                    Raw.Add(new SolarEvent(ToUtc(date.AddMinutes(s.NightMinute), Zone), false));
                }
            }
            else Raw = Solar.Events(ToUtc(LocalDate.AddDays(-3), Zone), ToUtc(LocalDate.AddDays(4), Zone), s.Latitude.Value, s.Longitude.Value);
            Switches = Raw.Select(x => new SolarEvent(x.Utc.AddMinutes(s.FixedTime ? 0 : (x.Day ? s.SunriseOffset : s.SunsetOffset)), x.Day)).OrderBy(x => x.Utc).ToList();
            for (int i = 1; i < Switches.Count; i++)
                if (Switches[i].Day == Switches[i-1].Day || Switches[i].Utc == Switches[i-1].Utc)
                    throw new Exception(Localization.T("偏移使昼夜切换顺序重叠，请减小偏移数值。"));
        }
        public bool IsDay(DateTime utc)
        {
            SolarEvent previous = Switches.LastOrDefault(x => x.Utc <= utc);
            if (previous != null) return previous.Day;
            SolarEvent next = Switches.FirstOrDefault(x => x.Utc > utc);
            if (next != null) return !next.Day;
            return Solar.Horizon(utc, Settings.Latitude.Value, Settings.Longitude.Value) >= 0;
        }
        public SolarEvent Next(DateTime utc) { return Switches.FirstOrDefault(x => x.Utc > utc); }
        public string LocalTime(DateTime utc) { return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone).ToString("MM-dd HH:mm"); }
        public string TodayEvent(bool day)
        {
            var item = Raw.FirstOrDefault(x => x.Day == day && TimeZoneInfo.ConvertTimeFromUtc(x.Utc, Zone).Date == LocalDate);
            return item == null ? Localization.T("当天无此事件") : TimeZoneInfo.ConvertTimeFromUtc(item.Utc, Zone).ToString("HH:mm");
        }
    }

    public class EngineProbe
    {
        public string Path = "";
        public string Identity;
        public string State;
        public string Title;
        public string Detail;
    }
    public static class Engine
    {
        public static bool IsValidExecutable(string path)
        {
            try { return !String.IsNullOrWhiteSpace(path) && File.Exists(path) && Regex.IsMatch(Path.GetFileName(path), "^wallpaper(32|64)\\.exe$", RegexOptions.IgnoreCase); }
            catch { return false; }
        }
        public static EngineProbe Probe(string preferred)
        {
            bool stale = !String.IsNullOrWhiteSpace(preferred) && !IsValidExecutable(preferred);
            string path = IsValidExecutable(preferred) ? preferred : Find();
            if (!IsValidExecutable(path)) return new EngineProbe { State="error", Title=Localization.T("未找到 Wallpaper Engine"), Detail=stale ? Localization.T("原安装位置已失效，自动查找也未找到可用程序。请在设置中重新指定位置。") : Localization.T("没有检测到可用安装。请确认已安装，或在设置中手动选择程序位置。") };
            bool restricted=false; string identity=null;
            foreach(string name in new[]{"wallpaper32","wallpaper64"})
            {
                Process[] instances;
                try { instances=Process.GetProcessesByName(name); } catch { restricted=true;continue; }
                foreach(Process p in instances) using(p) try
                {
                    string actualPath=p.MainModule.FileName;
                    if(String.Equals(Path.GetDirectoryName(actualPath),Path.GetDirectoryName(Path.GetFullPath(path)),StringComparison.OrdinalIgnoreCase))
                    { identity=p.Id+":"+p.StartTime.ToUniversalTime().Ticks;path=actualPath; }
                }catch{restricted=true;}
            }
            if(identity!=null)return new EngineProbe{Path=path,Identity=identity,State="ready",Title=Localization.T("Wallpaper Engine 已就绪"),Detail=Localization.T("已找到安装，运行状态正常。")};
            return new EngineProbe{Path=path,State="waiting",Title=restricted?Localization.T("已找到，但无法确认运行状态"):Localization.T("已找到，等待 Wallpaper Engine 启动"),Detail=restricted?Localization.T("读取进程状态受限。请检查两个程序是否由同一用户、以相同权限运行。"):Localization.T("程序位置有效。启动 Wallpaper Engine 后会自动恢复检测。")};
        }
        // Windows CommandLineToArgvW quoting; no shell is used.
        public static string Quote(string value)
        {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') b.Append('\\', slashes * 2 + 1); else b.Append('\\', slashes);
                slashes = 0; b.Append(c);
            }
            b.Append('\\', slashes * 2); return b.Append('"').ToString();
        }
        public static string Arguments(string path, int monitor)
        { return "-control openWallpaper -file " + Quote(path) + " -monitor " + monitor.ToString(CultureInfo.InvariantCulture); }
        public static string RunningIdentity(string exe)
        {
            string expected = Path.GetFullPath(exe);
            foreach (string name in new [] { "wallpaper32", "wallpaper64" })
                foreach (Process p in Process.GetProcessesByName(name))
                    using (p) try
                    {
                        string path = p.MainModule.FileName;
                        if (String.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(expected), StringComparison.OrdinalIgnoreCase))
                            return p.Id + ":" + p.StartTime.ToUniversalTime().Ticks;
                    } catch { }
            return null;
        }
        public static void Apply(Settings s, bool day)
        {
            s.Validate(true);
            if (RunningIdentity(s.EnginePath) == null) throw new Exception(Localization.T("等待 Wallpaper Engine 启动（请以相同用户权限运行两个程序）。"));
            var start = new ProcessStartInfo(s.EnginePath, Arguments(day ? s.DayPath : s.NightPath, s.Monitor));
            start.UseShellExecute = false; start.CreateNoWindow = true; start.WorkingDirectory = Path.GetDirectoryName(s.EnginePath);
            using (var p = Process.Start(start))
            {
                if (!p.WaitForExit(8000)) throw new Exception(Localization.T("Wallpaper Engine 命令超时；60 秒后重试。"));
                if (p.ExitCode != 0) throw new Exception(Localization.T("Wallpaper Engine 命令返回错误：") + p.ExitCode);
            }
        }
        public static List<string> Libraries()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string steam = null;
            try { steam = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null); } catch { }
            if (steam != null) roots.Add(steam);
            foreach(string key in new[]{@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"})
                try { string value=Registry.GetValue(key,"InstallPath",null) as string;if(!String.IsNullOrWhiteSpace(value))roots.Add(value); } catch { }
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            foreach (string root in roots.ToArray())
            {
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                try
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"")) roots.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                } catch { }
            }
            return roots.Where(Directory.Exists).Select(x=>Path.GetFullPath(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static string Find()
        {
            foreach (string name in new [] { "wallpaper64", "wallpaper32" })
            {
                Process[] instances;try{instances=Process.GetProcessesByName(name);}catch{continue;}
                foreach (Process p in instances) using (p) try { string path=p.MainModule.FileName;if(IsValidExecutable(path))return path; } catch { }
            }
            foreach(string key in new[]{@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 431960",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 431960"})
                try
                {
                    string directory=Registry.GetValue(key,"InstallLocation",null) as string;
                    if(!String.IsNullOrWhiteSpace(directory))foreach(string file in new[]{"wallpaper64.exe","wallpaper32.exe"})
                    {string path=Path.Combine(directory,file);if(IsValidExecutable(path))return path;}
                }catch{}
            foreach (string root in Libraries()) foreach (string exe in new [] { "wallpaper64.exe", "wallpaper32.exe" })
            { string path = Path.Combine(root, "steamapps", "common", "wallpaper_engine", exe); if (File.Exists(path)) return path; }
            return "";
        }
    }
}
