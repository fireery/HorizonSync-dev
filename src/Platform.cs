using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace HorizonSync
{
    public class LocationResult
    {
        public double? Latitude, Longitude;
        public string Message;
        public static LocationResult Read()
        {
            try
            {
                using (var watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default))
                {
                    // Respect existing Windows permissions, never change privacy settings or request a prompt.
                    watcher.TryStart(true, TimeSpan.FromSeconds(8));
                    var position = watcher.Position;
                    if (!position.Location.IsUnknown)
                        return new LocationResult { Latitude = position.Location.Latitude, Longitude = position.Location.Longitude,
                            Message = Localization.T("已采用 Windows 位置（系统记录时间：") + position.Timestamp.ToLocalTime().ToString("MM-dd HH:mm") + "）" };
                    return new LocationResult { Message = Localization.T("Windows 暂未提供位置，保留已有坐标。可检查 Windows 定位权限，或手动填写坐标。") };
                }
            }
            catch (Exception e) { return new LocationResult { Message = Localization.T("系统位置不可用，保留已有坐标：") + e.Message }; }
        }
    }

    class WallpaperItem { public string Title, Path; public override string ToString() { return Title; } }
    static class WallpaperLibrary
    {
        public static List<WallpaperItem> Scan(string exe)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lib in Engine.Libraries()) roots.Add(System.IO.Path.Combine(lib, "steamapps", "workshop", "content", "431960"));
            try
            {
                string dir = System.IO.Path.GetDirectoryName(exe);
                roots.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", "..", "workshop", "content", "431960")));
                roots.Add(System.IO.Path.Combine(dir, "projects", "myprojects")); roots.Add(System.IO.Path.Combine(dir, "projects", "defaultprojects"));
            } catch { }
            var result = new List<WallpaperItem>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                string[] dirs; try { dirs = Directory.GetDirectories(root); } catch { continue; }
                foreach (string dir in dirs)
                {
                    string project = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "project.json"));
                    if (!File.Exists(project) || !seen.Add(project)) continue;
                    try
                    {
                        if (new FileInfo(project).Length > 4 * 1024 * 1024) continue;
                        var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(project));
                        string title = data.ContainsKey("title") ? Convert.ToString(data["title"]) : System.IO.Path.GetFileName(dir);
                        string file = data.ContainsKey("file") ? Convert.ToString(data["file"]) : "";
                        string type = data.ContainsKey("type") ? Convert.ToString(data["type"]) : "scene";
                        string path = (type == "video" || type == "web") && file != "" ? System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, file)) : project;
                        if (File.Exists(path)) result.Add(new WallpaperItem { Title = title + "  [" + System.IO.Path.GetFileName(dir) + "]", Path = path });
                    } catch { }
                }
            }
            return result.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
