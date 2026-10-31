// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace HorizonSync
{
    internal sealed class WallpaperItem
    {
        public string Title { get; set; }
        public string Path { get; set; }
        public string ProjectPath { get; set; }
        public string ProjectId { get; set; }
        public string Type { get; set; }
        public string PreviewPath { get; set; }
        public string Author { get; set; }
        public string AuthorId { get; set; }
        public string WorkshopId { get; set; }
        public string TypeLabel { get { return Localization.T(Type == "video" ? "视频" : Type == "web" ? "网页" : Type == "scene" ? "场景" : "壁纸"); } }
    }

    internal sealed class WallpaperScanResult
    {
        public List<WallpaperItem> Items = new List<WallpaperItem>();
        public int Errors;
    }

    internal static class WallpaperLibrary
    {
        internal static string NumericId(string value)
        { return !String.IsNullOrEmpty(value) && Regex.IsMatch(value, @"^\d{1,20}$") ? value : ""; }

        internal static IEnumerable<string> Roots(string exe)
        {
            var roots = new List<string>();
            foreach (string lib in Engine.Libraries()) roots.Add(System.IO.Path.Combine(lib, "steamapps", "workshop", "content", "431960"));
            try
            {
                if (!String.IsNullOrWhiteSpace(exe))
                {
                    string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(exe));
                    roots.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", "..", "workshop", "content", "431960")));
                    roots.Add(System.IO.Path.Combine(dir, "projects", "myprojects"));
                    roots.Add(System.IO.Path.Combine(dir, "projects", "defaultprojects"));
                }
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            return roots;
        }

        static string Value(Dictionary<string, object> data, string key)
        {
            object value;
            return data.TryGetValue(key, out value) && (value is string || value is ValueType) ? Convert.ToString(value) : "";
        }

        internal static WallpaperScanResult Scan(IEnumerable<string> roots, CancellationToken token)
        {
            var result = new WallpaperScanResult();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                token.ThrowIfCancellationRequested();
                string[] dirs;
                try { if (!Directory.Exists(root)) continue; dirs = Directory.GetDirectories(root); }
                catch (Exception) { result.Errors++; continue; }
                foreach (string dir in dirs)
                {
                    token.ThrowIfCancellationRequested();
                    string project = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "project.json"));
                    if (!File.Exists(project) || !seen.Add(project)) continue;
                    try
                    {
                        if (new FileInfo(project).Length > 4 * 1024 * 1024) { result.Errors++; continue; }
                        var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(project));
                        string title = Value(data, "title"), type = Value(data, "type").ToLowerInvariant(), file = Value(data, "file");
                        string id = System.IO.Path.GetFileName(dir);
                        if (String.IsNullOrWhiteSpace(title)) title = id;
                        if (String.IsNullOrWhiteSpace(type)) type = "scene";
                        string path = (type == "video" || type == "web") && file != "" ? System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, file)) : project;
                        if (!File.Exists(path)) continue;
                        string preview = Value(data, "preview"), previewPath = "";
                        // Preview files must belong to this local project, never a remote URI.
                        if (!String.IsNullOrWhiteSpace(preview) && !System.IO.Path.IsPathRooted(preview))
                        {
                            string candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, preview));
                            if (candidate.StartsWith(System.IO.Path.GetFullPath(dir).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) previewPath = candidate;
                        }
                        string workshop = NumericId(Value(data, "workshopid"));
                        if (workshop == "" && dir.IndexOf("431960", StringComparison.OrdinalIgnoreCase) >= 0) workshop = NumericId(id);
                        result.Items.Add(new WallpaperItem { Title = title, Type = type, Path = path, ProjectPath = project,
                            ProjectId = id, PreviewPath = previewPath, Author = Value(data, "author"),
                            AuthorId = NumericId(Value(data, "authorsteamid")), WorkshopId = workshop });
                    }
                    catch (Exception) { result.Errors++; }
                }
            }
            result.Items = result.Items.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            return result;
        }
    }
}
