// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HorizonSync
{
    // Virtualize and recycle whole rows; each realized row contains a small uniform grid.
    // Unlike a WrapPanel, this keeps offscreen cards and their Image.Source out of memory.
    internal sealed class WallpaperRow { public List<WallpaperItem> Items { get; set; } }

    internal sealed class WallpaperCard : Button
    {
        readonly Image image = new Image { Stretch = Stretch.UniformToFill };
        readonly TextBlock placeholder = new TextBlock { FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock title = new TextBlock { TextWrapping = TextWrapping.Wrap, Height = 40, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 3) };
        readonly TextBlock type = new TextBlock { FontSize = 12, Foreground = Brushes.SlateGray };
        CancellationTokenSource loading;
        public WallpaperCard()
        {
            SetResourceReference(StyleProperty, typeof(Button));
            Background = Brushes.White; BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 218, 218));
            GotKeyboardFocus += (s, e) => { BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32,92,97)); BorderThickness = new Thickness(2); };
            LostKeyboardFocus += (s, e) => { BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204,218,218)); BorderThickness = new Thickness(1); };
            Margin = new Thickness(5); Padding = new Thickness(10); HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var content = new StackPanel(); var preview = new Grid { Height = 110, ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(232, 240, 240)) };
            preview.Children.Add(image); preview.Children.Add(placeholder); content.Children.Add(preview); content.Children.Add(title); content.Children.Add(type); Content = content;
            Loaded += (s, e) => Reload(); Unloaded += (s, e) => Release();
            DataContextChanged += (s, e) => { Release(); if (IsLoaded) Reload(); };
        }
        void Release()
        {
            var old = loading; loading = null; if (old != null) old.Cancel();
            image.Source = null;
        }
        async void Reload()
        {
            Release(); var item = DataContext as WallpaperItem; var cache = Tag as ThumbnailCache;
            Visibility = item == null ? Visibility.Hidden : Visibility.Visible;
            if (item == null || cache == null) return;
            title.Text = item.Title; type.Text = item.TypeLabel; ToolTip = item.Title;
            AutomationProperties.SetName(this, item.Title + " — " + item.TypeLabel);
            placeholder.Text = Localization.T("暂无预览图"); placeholder.Visibility = Visibility.Visible;
            var source = new CancellationTokenSource(); loading = source;
            try
            {
                var bitmap = await cache.GetAsync(item.PreviewPath, 360, source.Token);
                if (source.IsCancellationRequested || loading != source || !IsLoaded) return;
                image.Source = bitmap; placeholder.Visibility = bitmap == null ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (OperationCanceledException) { }
            finally { if (loading == source) loading = null; source.Dispose(); }
        }
    }

    internal sealed class WallpaperPicker : Window
    {
        readonly string engine, originalPath;
        readonly IEnumerable<string> scanRoots;
        readonly ThumbnailCache cache = new ThumbnailCache();
        readonly PreviewCoordinator preview = new PreviewCoordinator();
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly Grid root = new Grid(), browser = new Grid(), shade = new Grid();
        readonly Border panel = new Border();
        readonly ListBox rows = new ListBox();
        readonly TextBox search = new TextBox();
        readonly TextBlock status = new TextBlock(), largeTitle = new TextBlock(), author = new TextBlock(), info = new TextBlock(), previewStatus = new TextBlock();
        readonly Image largeImage = new Image { Stretch = Stretch.Uniform };
        readonly Button retry = new Button(), workshop = new Button(), profile = new Button(), refresh = new Button(), use = new Button(), playback = new Button();
        readonly DispatcherTimer rendering = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(34) };
        readonly DispatcherTimer filtering = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        readonly object frameGate = new object();
        CancellationTokenSource authorRequest, scanRequest;
        BitmapSource pendingFrame;
        byte[] pendingPixels;
        int pixelWidth, pixelHeight;
        WriteableBitmap liveBitmap;
        string pendingMessage;
        int pendingVersion, sessionVersion, columns, scanVersion;
        bool frameReady, closed, rebuilding, paused;
        List<WallpaperItem> items = new List<WallpaperItem>();
        List<WallpaperItem> filtered = new List<WallpaperItem>();
        WallpaperItem selected;
        AuthorInfo authorInfo;
        WallpaperCard previousCard;
        int scanErrors;
        internal string SelectedPath { get; private set; }
        internal Task PreviewCompletion { get { return preview.Completion; } }
        internal int RealizedRows { get { return Enumerable.Range(0, rows.Items.Count).Count(i => rows.ItemContainerGenerator.ContainerFromIndex(i) != null); } }

        static string T(string value) { return Localization.T(value); }
        static Brush Color(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
        static void At(Grid grid, UIElement child, int row) { Grid.SetRow(child, row); grid.Children.Add(child); }
        Button ActionButton(string text, Action action)
        {
            var button = new Button { Content = T(text), Margin = new Thickness(5), MinWidth = 100 };
            button.Click += (s, e) => action(); return button;
        }

        internal WallpaperPicker(Window owner, string enginePath, string currentPath, bool day, ResourceDictionary resources, IEnumerable<string> roots = null)
        {
            Owner = owner; Icon = owner.Icon; Resources = resources;
            engine = enginePath; originalPath = currentPath;
            scanRoots = roots;
            Title = T(day ? "选择白天壁纸" : "选择夜晚壁纸");
            Width = Math.Min(1080, Math.Max(700, SystemParameters.WorkArea.Width - 32));
            Height = Math.Min(740, Math.Max(500, SystemParameters.WorkArea.Height - 32)); MinWidth = 700; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = owner.FontFamily; FontSize = 15;
            Background = Color("#F3F9F9"); UseLayoutRounding = true;
            Language = owner.Language;
            Content = root; root.Children.Add(browser); browser.Margin = new Thickness(24);
            browser.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            browser.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            browser.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            browser.RowDefinitions.Add(new RowDefinition());
            browser.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            At(browser, new TextBlock { Text = Title, FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) }, 0);
            var searchRow = new Grid(); searchRow.ColumnDefinitions.Add(new ColumnDefinition()); searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            search.ToolTip = T("输入壁纸名称筛选"); AutomationProperties.SetName(search, T("搜索壁纸名称")); searchRow.Children.Add(search);
            refresh.Content = T("重新扫描"); refresh.Margin = new Thickness(12, 0, 0, 0); refresh.Click += async (s, e) => await Scan(); Grid.SetColumn(refresh, 1); searchRow.Children.Add(refresh); At(browser, searchRow, 1);
            status.Margin = new Thickness(0, 12, 0, 12); status.Foreground = Color("#607476"); At(browser, status, 2);
            rows.BorderThickness = new Thickness(0); rows.Background = Brushes.Transparent;
            rows.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            ScrollViewer.SetHorizontalScrollBarVisibility(rows, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(rows, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(rows, true);
            VirtualizingPanel.SetIsVirtualizing(rows, true); VirtualizingPanel.SetVirtualizationMode(rows, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(rows, ScrollUnit.Pixel);
            var rowStyle = new Style(typeof(ListBoxItem));
            rowStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            rowStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            rowStyle.Setters.Add(new Setter(UIElement.FocusableProperty, false));
            var rowTemplate = new ControlTemplate(typeof(ListBoxItem)); var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            rowTemplate.VisualTree = presenter; rowStyle.Setters.Add(new Setter(Control.TemplateProperty, rowTemplate)); rows.ItemContainerStyle = rowStyle;
            rows.AddHandler(Button.ClickEvent, new RoutedEventHandler(CardClick));
            rows.PreviewKeyDown += GridKeys; At(browser, rows, 3);
            var cancel = ActionButton("取消", () => Close()); cancel.HorizontalAlignment = HorizontalAlignment.Right; At(browser, cancel, 4);
            search.TextChanged += (s, e) => { filtering.Stop(); filtering.Start(); };
            filtering.Tick += FilterTick;
            rows.SizeChanged += (s, e) => { int next = ColumnCount(); if (columns != next) Rebuild(false); };
            BuildOverlay();
            rendering.Tick += RenderFrame;
            PreviewKeyDown += WindowKeys;
            SizeChanged += (s, e) => { panel.MaxWidth = Math.Max(1, root.ActualWidth * .9); panel.MaxHeight = Math.Max(1, root.ActualHeight * .9); };
            StateChanged += (s, e) => { if (WindowState == WindowState.Minimized) StopSession(); else if (shade.Visibility == Visibility.Visible && selected != null) StartSession(); };
            Loaded += async (s, e) => { search.Focus(); await Scan(); };
            Closed += (s, e) => Cleanup();
        }

        void BuildOverlay()
        {
            shade.Visibility = Visibility.Collapsed; shade.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(175, 10, 24, 27));
            shade.MouseDown += (s, e) => { if (e.OriginalSource == shade) HidePreview(); };
            KeyboardNavigation.SetTabNavigation(shade, KeyboardNavigationMode.Cycle);
            panel.Background = Color("#FCFEFE"); panel.CornerRadius = new CornerRadius(10); panel.Padding = new Thickness(24);
            panel.HorizontalAlignment = HorizontalAlignment.Center; panel.VerticalAlignment = VerticalAlignment.Center;
            panel.Width = 960; panel.Height = 650;
            shade.Children.Add(panel); root.Children.Add(shade);
            var layout = new Grid(); panel.Child = layout;
            layout.RowDefinitions.Add(new RowDefinition());
            for (int i = 0; i < 5; i++) layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var imageSurface = new Grid { Background = Color("#E8F0F0"), MinHeight = 80, Margin = new Thickness(0, 0, 0, 16) };
            imageSurface.Children.Add(largeImage); previewStatus.HorizontalAlignment = HorizontalAlignment.Center; previewStatus.VerticalAlignment = VerticalAlignment.Bottom; previewStatus.Margin = new Thickness(10);
            previewStatus.Background = Color("#E8F0F0"); imageSurface.Children.Add(previewStatus); At(layout, imageSurface, 0);
            largeTitle.FontSize = 21; largeTitle.FontWeight = FontWeights.SemiBold; largeTitle.TextWrapping = TextWrapping.Wrap; largeTitle.MaxHeight = 80; largeTitle.TextTrimming = TextTrimming.CharacterEllipsis; At(layout, largeTitle, 1);
            author.Margin = new Thickness(0, 8, 0, 6); author.TextTrimming = TextTrimming.CharacterEllipsis; author.TextWrapping = TextWrapping.NoWrap; At(layout, author, 2);
            info.Foreground = Color("#607476"); info.FontSize = 12; info.TextWrapping = TextWrapping.NoWrap; info.TextTrimming = TextTrimming.CharacterEllipsis; At(layout, info, 3);
            var links = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
            playback.Content = T("暂停预览"); playback.Margin = new Thickness(0, 0, 8, 0);
            playback.Click += (s, e) => { paused = !paused; StartSession(); }; links.Children.Add(playback);
            retry.Content = T("重试作者查询"); retry.Click += async (s, e) => await LoadAuthor(true); links.Children.Add(retry);
            workshop.Content = T("创意工坊详情"); workshop.Margin = new Thickness(8, 0, 0, 0); workshop.Click += (s, e) => OpenSteam(false); links.Children.Add(workshop);
            profile.Content = T("作者主页"); profile.Margin = new Thickness(8, 0, 0, 0); profile.Click += (s, e) => OpenSteam(true); links.Children.Add(profile); At(layout, links, 4);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ActionButton("返回浏览", HidePreview));
            use.Content = T("使用这张壁纸"); use.Style = Resources["Primary"] as Style; use.Margin = new Thickness(8, 0, 0, 0);
            use.Click += (s, e) => { if (selected != null) { SelectedPath = selected.Path; DialogResult = true; } }; buttons.Children.Add(use); At(layout, buttons, 5);
            AutomationProperties.SetName(largeImage, T("选中壁纸大图预览"));
        }

        async Task Scan()
        {
            if (closed) return;
            if (scanRequest != null) scanRequest.Cancel();
            var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); scanRequest = source;
            int version = ++scanVersion;
            refresh.IsEnabled = false; status.Text = T("正在扫描本地壁纸…");
            try
            {
                var result = await Task.Run(() => WallpaperLibrary.Scan(scanRoots ?? WallpaperLibrary.Roots(engine), source.Token), source.Token);
                if (closed || version != scanVersion) return;
                items = result.Items; scanErrors = result.Errors; Rebuild(true);
                var current = filtered.FirstOrDefault(x => String.Equals(x.Path, originalPath, StringComparison.OrdinalIgnoreCase));
                if (current != null) await Dispatcher.InvokeAsync(new Action(() => { if (!closed && shade.Visibility != Visibility.Visible) FocusItem(filtered.IndexOf(current)); }), DispatcherPriority.Input);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!closed) status.Text = T("扫描失败，请重新扫描或检查程序位置。"); }
            finally { if (scanRequest == source) scanRequest = null; source.Dispose(); if (!closed) refresh.IsEnabled = true; }
        }

        int ColumnCount() { return Math.Max(1, (int)(Math.Max(180, rows.ActualWidth - 24) / 200)); }
        void Rebuild(bool resetScroll)
        {
            if (closed || rebuilding) return;
            rebuilding = true;
            try
            {
                columns = ColumnCount(); string query = search.Text.Trim();
                filtered = items.Where(x => x.Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
                var grouped = new List<WallpaperRow>();
                for (int i = 0; i < filtered.Count; i += columns)
                {
                    var row = filtered.Skip(i).Take(columns).ToList(); while (row.Count < columns) row.Add(null);
                    grouped.Add(new WallpaperRow { Items = row });
                }
                var template = new DataTemplate(typeof(WallpaperRow));
                var control = new FrameworkElementFactory(typeof(ItemsControl)); control.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Items"));
                var uniform = new FrameworkElementFactory(typeof(UniformGrid)); uniform.SetValue(UniformGrid.ColumnsProperty, columns);
                control.SetValue(ItemsControl.ItemsPanelProperty, new ItemsPanelTemplate(uniform));
                var card = new FrameworkElementFactory(typeof(WallpaperCard)); card.SetValue(FrameworkElement.TagProperty, cache);
                var cardTemplate = new DataTemplate(typeof(WallpaperItem)) { VisualTree = card }; control.SetValue(ItemsControl.ItemTemplateProperty, cardTemplate);
                template.VisualTree = control; rows.ItemTemplate = template; rows.ItemsSource = grouped;
                if (resetScroll && grouped.Count > 0) rows.ScrollIntoView(grouped[0]);
                status.Text = items.Count == 0 ? T(scanErrors > 0 ? "扫描失败，请重新扫描或检查程序位置。" : "没有找到壁纸。请检查程序位置，或返回使用浏览按钮。") :
                    filtered.Count == 0 ? T("没有匹配的壁纸，请尝试其他名称。") : String.Format(T("显示 {0} / {1} 张壁纸"), filtered.Count, items.Count);
                if (scanErrors > 0 && items.Count > 0) status.Text += T(" · 部分项目无法读取");
            }
            finally { rebuilding = false; }
        }

        void CardClick(object sender, RoutedEventArgs e)
        {
            var card = e.OriginalSource as WallpaperCard;
            if (card == null || shade.Visibility == Visibility.Visible) return;
            var item = card.DataContext as WallpaperItem; if (item == null) return;
            previousCard = card; selected = item;
            paused = false;
            playback.Visibility = String.Equals(System.IO.Path.GetExtension(item.PreviewPath), ".gif", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            largeTitle.Text = item.Title; largeTitle.ToolTip = item.Title;
            info.Text = item.TypeLabel + " · " + T("项目编号：") + item.ProjectId; info.ToolTip = item.ProjectPath;
            browser.IsEnabled = false; shade.Visibility = Visibility.Visible;
            StartSession();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            { if (!closed && shade.Visibility == Visibility.Visible) { FocusManager.SetFocusedElement(this, use); Keyboard.Focus(use); } }));
            e.Handled = true;
        }

        void GridKeys(object sender, KeyEventArgs e)
        {
            var card = Keyboard.FocusedElement as WallpaperCard;
            if (card == null) return;
            int index = filtered.IndexOf(card.DataContext as WallpaperItem), next = index;
            if (e.Key == Key.Right) next++; else if (e.Key == Key.Left) next--;
            else if (e.Key == Key.Down) next += columns; else if (e.Key == Key.Up) next -= columns;
            else if (e.Key == Key.Enter) { card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, card)); e.Handled = true; return; }
            else return;
            next = Math.Max(0, Math.Min(filtered.Count - 1, next));
            if (next >= 0) FocusItem(next); e.Handled = true;
        }

        void FocusItem(int index)
        {
            if (closed || index < 0 || index >= filtered.Count) return;
            var item = filtered[index]; rows.ScrollIntoView(rows.Items[index / columns]); rows.UpdateLayout();
            var container = rows.ItemContainerGenerator.ContainerFromIndex(index / columns);
            var card = FindCards(container).FirstOrDefault(x => x.DataContext == item); if (card != null) card.Focus();
        }
        static IEnumerable<WallpaperCard> FindCards(DependencyObject node)
        {
            if (node == null) yield break;
            var card = node as WallpaperCard; if (card != null) { yield return card; yield break; }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (var child in FindCards(VisualTreeHelper.GetChild(node, i))) yield return child;
        }
        void WindowKeys(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { if (shade.Visibility == Visibility.Visible) HidePreview(); else Close(); e.Handled = true; }
        }

        void FilterTick(object sender, EventArgs e) { filtering.Stop(); Rebuild(true); }
        async void StartSession()
        {
            StopSession(); if (closed || selected == null || WindowState == WindowState.Minimized) return;
            playback.Content = T(paused ? "播放预览" : "暂停预览");
            int version = sessionVersion; previewStatus.Text = T("正在加载预览…"); rendering.Start();
            preview.Show(selected.PreviewPath, (image, message) =>
            {
                lock (frameGate) { if (version != sessionVersion || closed) return; pendingPixels = null; pendingFrame = image; pendingMessage = message; pendingVersion = version; frameReady = true; }
            }, (pixels, width, height) =>
            {
                lock (frameGate)
                {
                    if (version != sessionVersion || closed) return;
                    if (pendingPixels == null || pendingPixels.Length != pixels.Length) pendingPixels = new byte[pixels.Length];
                    Buffer.BlockCopy(pixels, 0, pendingPixels, 0, pixels.Length);
                    pixelWidth = width; pixelHeight = height; pendingFrame = null; pendingMessage = ""; pendingVersion = version; frameReady = true;
                }
            }, !paused);
            await LoadAuthor(false); // Every continuation is guarded by its session version.
        }
        void RenderFrame(object sender, EventArgs e)
        {
            lock (frameGate)
            {
                if (!frameReady) return;
                if (pendingVersion == sessionVersion)
                {
                    if (pendingPixels != null)
                    {
                        if (liveBitmap == null || liveBitmap.PixelWidth != pixelWidth || liveBitmap.PixelHeight != pixelHeight)
                            liveBitmap = new WriteableBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32, null);
                        liveBitmap.WritePixels(new Int32Rect(0, 0, pixelWidth, pixelHeight), pendingPixels, pixelWidth * 4, 0);
                        largeImage.Source = liveBitmap;
                    }
                    else { liveBitmap = null; largeImage.Source = pendingFrame; rendering.Stop(); }
                    previewStatus.Text = T(pendingMessage);
                }
                pendingFrame = null; pendingMessage = null; frameReady = false;
            }
        }
        void StopSession()
        {
            lock (frameGate) { sessionVersion++; pendingFrame = null; pendingPixels = null; liveBitmap = null; pendingMessage = null; frameReady = false; }
            preview.Stop(); rendering.Stop(); largeImage.Source = null;
            if (authorRequest != null) { authorRequest.Cancel(); authorRequest = null; }
        }
        void HidePreview()
        {
            StopSession(); shade.Visibility = Visibility.Collapsed; browser.IsEnabled = true;
            var card = previousCard; var item = selected;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (closed || shade.Visibility == Visibility.Visible) return;
                if (card != null && card.IsLoaded && card.DataContext == item) { FocusManager.SetFocusedElement(this, card); Keyboard.Focus(card); } else if (item != null) FocusItem(filtered.IndexOf(item));
            }));
            previousCard = null; selected = null; authorInfo = null;
        }

        async Task LoadAuthor(bool force)
        {
            if (selected == null || closed) return;
            if (authorRequest != null) authorRequest.Cancel();
            var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); authorRequest = source;
            int version = sessionVersion; var item = selected;
            authorInfo = null; author.Text = T("作者：") + T("正在查询…"); profile.Visibility = Visibility.Collapsed;
            workshop.Visibility = String.IsNullOrEmpty(item.WorkshopId) ? Visibility.Collapsed : Visibility.Visible;
            retry.Visibility = Visibility.Collapsed;
            try
            {
                var result = await WallpaperAuthors.Shared.GetAsync(item, force, source.Token);
                if (closed || source.IsCancellationRequested || version != sessionVersion || item != selected) return;
                authorInfo = result;
                author.Text = T("作者：") + (!String.IsNullOrWhiteSpace(result.Name) ? result.Name : !String.IsNullOrEmpty(result.Id) ? "Steam ID " + result.Id : T("作者未知"));
                author.ToolTip = author.Text; profile.Visibility = String.IsNullOrEmpty(result.Id) ? Visibility.Collapsed : Visibility.Visible;
                retry.Visibility = result.Failed ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (OperationCanceledException) { }
            finally { if (authorRequest == source) authorRequest = null; source.Dispose(); }
        }
        void OpenSteam(bool user)
        {
            string id = user ? (authorInfo == null ? "" : authorInfo.Id) : (selected == null ? "" : selected.WorkshopId);
            if (WallpaperLibrary.NumericId(id) == "") return;
            try { Process.Start(new ProcessStartInfo(user ? "https://steamcommunity.com/profiles/" + id + "/" : "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id) { UseShellExecute = true }); }
            catch (Exception) { MessageBox.Show(this, T("无法打开浏览器。"), "HorizonSync"); }
        }
        void Cleanup()
        {
            if (closed) return; closed = true; StopSession(); lifetime.Cancel(); preview.Dispose(); cache.Dispose();
            filtering.Stop(); filtering.Tick -= FilterTick; rendering.Tick -= RenderFrame;
            rows.ItemsSource = null; rows.ItemTemplate = null; items.Clear(); filtered.Clear(); previousCard = null; selected = null; authorInfo = null;
            lifetime.Dispose();
        }
    }
}
