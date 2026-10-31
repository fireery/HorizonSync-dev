using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace HorizonSync
{
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            try
            {
                string directory=null;
                int dataArg=Array.IndexOf(args,"--data-dir");
                if(dataArg>=0){if(dataArg+1>=args.Length)throw new Exception(Localization.T("--data-dir 需要目录路径。"));directory=args[dataArg+1];}
                else if(args.Contains("--portable")||File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"portable.flag")))directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
                Store.Initialize(directory);
                Localization.Use(Store.Load().Language);
                bool first;
                string mutexName="Local\\HorizonSync-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
                if(dataArg>=0)using(var hash=System.Security.Cryptography.SHA256.Create())mutexName+="-"+BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Store.Root.ToUpperInvariant()))).Replace("-","").Substring(0,16);
                using(var mutex=new Mutex(true,mutexName,out first))
                {
                    if(!first){MessageBox.Show(Localization.T("HorizonSync 已运行，请从系统托盘打开设置。"),"HorizonSync");return;}
                    var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
                    app.DispatcherUnhandledException+=(s,e)=>{Store.Log(e.Exception.ToString());MessageBox.Show(e.Exception.Message,"HorizonSync");e.Handled=true;};
                    app.Run(new MainWindow(args.Contains("--tray")));
                }
            }
            catch(Exception e)
            {
                try{File.WriteAllText(Path.Combine(Store.Root,"startup-error.log"),e.ToString());}catch{}
                MessageBox.Show(Localization.T("启动失败：")+e.Message+Localization.T("\n详见 startup-error.log。"),"HorizonSync");
            }
        }
    }
    public class MainWindow : Window
    {
        readonly FrameworkElement view;
        readonly UiLocalization uiLanguage;
        readonly DispatcherTimer timer=new DispatcherTimer();
        readonly Forms.NotifyIcon tray=new Forms.NotifyIcon();
        Settings saved; Plan plan; string appliedKey;
        DateTime retryAfter=DateTime.MinValue;
        bool busy,auditing,closing,originalStartup,discovering;
        EngineProbe latestProbe;
        System.Drawing.Icon brandTrayIcon;
        bool brandDark;
        int brandPixels;
        int locationState;
        int generation;
        const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
        T C<T>(string name) where T:FrameworkElement{return (T)view.FindName(name);}
        TextBox Input(string name){return C<TextBox>(name);}
        TextBlock Label(string name){return C<TextBlock>(name);}
        bool Checked(string name){return C<System.Windows.Controls.Primitives.ToggleButton>(name).IsChecked==true;}
        void SetCheck(string name,bool value){C<System.Windows.Controls.Primitives.ToggleButton>(name).IsChecked=value;}
        void Click(string name,Action action){C<Button>(name).Click+=(s,e)=>action();}
        public MainWindow(bool startInTray)
        {
            saved=Store.Load();Localization.Use(saved.Language);Title="HorizonSync";Width=1100;Height=850;MinWidth=900;MinHeight=680;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");FontSize=16;
            Language=XmlLanguage.GetLanguage(Localization.IsEnglish?"en":"zh-CN");UseLayoutRounding=true;SnapsToDevicePixels=true;
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
            using(var stream=typeof(Program).Assembly.GetManifestResourceStream("HorizonSync.MainWindow.xaml"))view=(FrameworkElement)XamlReader.Load(stream);
            Content=view;uiLanguage=new UiLocalization(view);LoadFields();uiLanguage.Apply();
            foreach(string name in new[]{"NavTime","NavWallpaper","NavSettings"})C<RadioButton>(name).Checked+=(s,e)=>Navigate();
            foreach(string name in new[]{"FixedRadio","SystemRadio","ManualRadio"})C<RadioButton>(name).Checked+=(s,e)=>UpdateMode();
            UpdateMode();
            C<ComboBox>("LanguageSelect").SelectionChanged+=async(s,e)=>await ChangeLanguage();
            Click("LicenseButton",ShowLicense);
            Click("SaveButton",async()=>await Save());Click("HideButton",()=>Hide());Click("PreviewButton",ShowPreview);
            Click("LocationButton",async()=>{await AuditLocation(false);await Tick();});
            Click("EngineBrowse",()=>Browse("EngineInput",true));Click("DayBrowse",()=>Browse("DayPathInput",false));Click("NightBrowse",()=>Browse("NightPathInput",false));
            Click("FindEngine",async()=>await DiscoverEngine(false));
            Click("EngineSettingsLink",()=>
            {
                SetCheck("NavSettings",true);
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{C<Border>("EngineSettingsSection").BringIntoView();Input("EngineInput").Focus();}));
            });
            Click("DayLibrary",async()=>await PickWallpaper("DayPathInput"));Click("NightLibrary",async()=>await PickWallpaper("NightPathInput"));
            Click("DayTest",async()=>await TestWallpaper(true));Click("NightTest",async()=>await TestWallpaper(false));
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add(Localization.T("打开设置"),null,(s,e)=>Dispatcher.BeginInvoke(new Action(ShowWindow)));
            menu.Items.Add(Localization.T("立即重新判断"),null,(s,e)=>Dispatcher.BeginInvoke(new Action(async()=>{Invalidate();await Tick();})));
            menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add(Localization.T("彻底退出"),null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{closing=true;Close();})));
            RefreshBrandIcon(true);tray.Text=Localization.T("HorizonSync · 未启用");tray.ContextMenuStrip=menu;tray.Visible=true;
            tray.DoubleClick+=(s,e)=>Dispatcher.BeginInvoke(new Action(ShowWindow));
            timer.Interval=TimeSpan.FromSeconds(15);timer.Tick+=async(s,e)=>await Tick();
            SystemEvents.TimeChanged+=TimeChanged;SystemEvents.PowerModeChanged+=PowerChanged;SystemEvents.SessionSwitch+=SessionChanged;
            SystemEvents.UserPreferenceChanged+=PreferencesChanged;
            LocationChanged+=(s,e)=>RefreshBrandIcon();
            Loaded+=async(s,e)=>
            {
                MinWidth=Math.Min(MinWidth,SystemParameters.WorkArea.Width);MinHeight=Math.Min(MinHeight,SystemParameters.WorkArea.Height);
                Width=Math.Min(Width,SystemParameters.WorkArea.Width);Height=Math.Min(Height,SystemParameters.WorkArea.Height);
                RefreshBrandIcon(true);
                if(startInTray&&Store.LoadError==null)Hide();if(Store.LoadError!=null)MessageBox.Show(this,Store.LoadError,"HorizonSync");
                await Task.WhenAll(AuditLocation(true),DiscoverEngine(true));timer.Start();await Tick();
            };
            Closing+=(s,e)=>{if(!closing&&!Application.Current.Dispatcher.HasShutdownStarted){e.Cancel=true;Hide();}};
            Closed+=(s,e)=>{timer.Stop();tray.Visible=false;tray.Dispose();if(brandTrayIcon!=null)brandTrayIcon.Dispose();SystemEvents.TimeChanged-=TimeChanged;SystemEvents.PowerModeChanged-=PowerChanged;SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.UserPreferenceChanged-=PreferencesChanged;};
        }
        void LoadFields()
        {
            SetCheck(saved.FixedTime?"FixedRadio":saved.CheckSystemLocation?"SystemRadio":"ManualRadio",true);SetCheck("EnabledToggle",saved.Enabled);
            Input("LatitudeInput").Text=saved.Latitude.HasValue?saved.Latitude.Value.ToString(CultureInfo.InvariantCulture):"";
            Input("LongitudeInput").Text=saved.Longitude.HasValue?saved.Longitude.Value.ToString(CultureInfo.InvariantCulture):"";
            Input("RiseOffsetInput").Text=saved.SunriseOffset.ToString();Input("SetOffsetInput").Text=saved.SunsetOffset.ToString();
            Input("DayTimeInput").Text=MinuteText(saved.DayMinute);Input("NightTimeInput").Text=MinuteText(saved.NightMinute);
            Input("EngineInput").Text=saved.EnginePath;Input("DayPathInput").Text=saved.DayPath;Input("NightPathInput").Text=saved.NightPath;Input("MonitorInput").Text=saved.Monitor.ToString();
            Label("StartupInfo").Text=Localization.T("在当前用户登录后启动到托盘");
            try{using(var key=Registry.CurrentUser.OpenSubKey(RunKey))originalStartup=key!=null&&String.Equals(key.GetValue("HorizonSync") as string,StartupCommand(),StringComparison.OrdinalIgnoreCase);}
            catch{C<CheckBox>("StartupToggle").IsEnabled=false;Label("StartupInfo").Text=Localization.T("当前环境不允许读取启动项；在普通用户环境中可设置。");}
            SetCheck("StartupToggle",originalStartup);UpdateClock();
            C<ComboBox>("LanguageSelect").SelectedIndex=saved.Language=="en"?2:saved.Language=="zh-CN"?1:0;
        }
        string MinuteText(int m){m=Math.Max(0,Math.Min(1439,m));return(m/60).ToString("00")+":"+(m%60).ToString("00");}
        void Navigate()
        {
            C<StackPanel>("TimePage").Visibility=Checked("NavTime")?Visibility.Visible:Visibility.Collapsed;
            C<StackPanel>("WallpaperPage").Visibility=Checked("NavWallpaper")?Visibility.Visible:Visibility.Collapsed;
            C<StackPanel>("SettingsPage").Visibility=Checked("NavSettings")?Visibility.Visible:Visibility.Collapsed;C<ScrollViewer>("PageScroll").ScrollToTop();
        }
        void UpdateMode()
        {
            bool f=Checked("FixedRadio");C<Border>("FixedCard").Visibility=f?Visibility.Visible:Visibility.Collapsed;
            C<Border>("SolarCard").Visibility=f?Visibility.Collapsed:Visibility.Visible;C<StackPanel>("OffsetSection").Visibility=f?Visibility.Collapsed:Visibility.Visible;
            C<Button>("LocationButton").Visibility=Checked("SystemRadio")?Visibility.Visible:Visibility.Collapsed;
            Label("LocationInfo").Text=Checked("SystemRadio")?Localization.T("每次启动采用 Windows 位置；无法获取时使用下方备用坐标。"):Localization.T("使用指定坐标计算；日期、时间和时区采用 Windows 当前值。");
        }
        double? Coordinate(string name)
        {
            string text=Input(name).Text.Trim();if(text=="")return null;double v;
            if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!double.TryParse(text,out v))throw new Exception(Localization.T("经纬度需填写数字。"));return v;
        }
        int Integer(string name,string description){int v;if(!int.TryParse(Input(name).Text.Trim(),out v))throw new Exception(description+Localization.T("需填写整数。"));return v;}
        int TimeMinute(string name)
        {
            DateTime t;if(!DateTime.TryParseExact(Input(name).Text.Trim(),new[]{"HH:mm","H:mm"},CultureInfo.InvariantCulture,DateTimeStyles.None,out t))throw new Exception(Localization.T("固定时间请使用 HH:mm 格式，例如 07:00。"));return t.Hour*60+t.Minute;
        }
        Settings Draft()
        {
            TimeZoneInfo.ClearCachedData();return new Settings{Language=LanguageChoice(),Enabled=Checked("EnabledToggle"),FixedTime=Checked("FixedRadio"),CheckSystemLocation=Checked("SystemRadio"),Latitude=Coordinate("LatitudeInput"),Longitude=Coordinate("LongitudeInput"),TimeZoneId=TimeZoneInfo.Local.Id,
                SunriseOffset=Integer("RiseOffsetInput",Localization.T("日出偏移")),SunsetOffset=Integer("SetOffsetInput",Localization.T("日落偏移")),DayMinute=TimeMinute("DayTimeInput"),NightMinute=TimeMinute("NightTimeInput"),Monitor=Integer("MonitorInput",Localization.T("显示器编号")),
                EnginePath=Input("EngineInput").Text.Trim().Trim('"'),DayPath=Input("DayPathInput").Text.Trim().Trim('"'),NightPath=Input("NightPathInput").Text.Trim().Trim('"')};
        }
        void ShowPreview(){try{DateTime now=DateTime.UtcNow;Label("PreviewText").Text=Describe(new Plan(Draft(),now),now);}catch(Exception e){Label("PreviewText").Text=e.Message;}}
        string Describe(Plan p,DateTime now)
        {
            var n=p.Next(now);return(p.Settings.FixedTime?Localization.T("自定义时间"):Localization.T("日出  ")+p.TodayEvent(true)+Localization.T("       日落  ")+p.TodayEvent(false))+Localization.T("\n当前应为：")+(p.IsDay(now)?Localization.T("白天"):Localization.T("夜晚"))+"\n"+(n==null?Localization.T("附近日期无切换事件（可能为极昼 / 极夜）。"):Localization.T("下次切换：")+p.LocalTime(n.Utc)+" → "+(n.Day?Localization.T("白天"):Localization.T("夜晚")))+Localization.T("（系统时区）");
        }
        static string StartupCommand(){return Engine.Quote(typeof(Program).Assembly.Location)+" --tray";}
        async Task Save()
        {
            try
            {
                if(auditing||busy)throw new Exception(Localization.T("正在校对位置或执行切换，请稍后保存。"));
                var draft=Draft();draft.Validate(draft.Enabled,draft.Enabled);Plan check=null;
                if(draft.FixedTime||(draft.Latitude.HasValue&&draft.Longitude.HasValue))check=new Plan(draft,DateTime.UtcNow);
                Store.Save(draft);saved=draft;Invalidate();plan=check;
                try
                {
                    bool startup=Checked("StartupToggle");if(startup!=originalStartup){using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){if(startup)key.SetValue("HorizonSync",StartupCommand());else key.DeleteValue("HorizonSync",false);}originalStartup=startup;}
                }
                catch(Exception e){MessageBox.Show(this,Localization.T("其他设置已保存，但登录启动设置未生效：\n")+e.Message,"HorizonSync");}
                Label("PreviewText").Text=check==null?Localization.T("填写位置后预览今日计划。"):Describe(check,DateTime.UtcNow);Store.Log(Localization.T("设置已保存；自动切换：")+saved.Enabled);await Tick();
            }catch(Exception e){MessageBox.Show(this,Localization.T("保存失败：\n")+e.Message,"HorizonSync");}
        }
        async Task AuditLocation(bool initial)
        {
            if(auditing||busy)return;auditing=true;Invalidate();SystemState.RefreshClock(saved);UpdateClock();
            try
            {
                if(initial&&!saved.CheckSystemLocation){Label("LocationInfo").Text=Localization.T("使用已保存坐标；系统日期、时间和时区已校对。");ShowPreview();return;}
                Label("LocationInfo").Text=Localization.T("正在读取 Windows 位置，最多等待约 8 秒…");var result=await Task.Run(()=>LocationResult.Read());if(closing)return;Label("LocationInfo").Text=result.Message;
                locationState=result.Latitude.HasValue?1:-1;
                if(SystemState.ApplyCoordinates(saved,result.Latitude,result.Longitude))
                {
                    Input("LatitudeInput").Text=saved.Latitude.Value.ToString("0.######",CultureInfo.InvariantCulture);Input("LongitudeInput").Text=saved.Longitude.Value.ToString("0.######",CultureInfo.InvariantCulture);
                    if(File.Exists(Store.ConfigPath)&&Store.LoadError==null)Store.Save(saved);Store.Log(Localization.T("已采用 Windows 系统坐标。"));
                }else Store.Log(Localization.T("系统位置暂不可用，保留已有坐标。"));ShowPreview();
            }catch(Exception e){Label("LocationInfo").Text=Localization.T("位置校对未完成：")+e.Message;Store.Log(e.Message);}finally{auditing=false;}
        }
        void UpdateClock(){Label("ClockText").Text=Localization.T("系统时间：")+DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz")+Localization.T("\n系统时区：")+(Localization.IsEnglish?TimeZoneInfo.Local.Id:TimeZoneInfo.Local.StandardName);}
        void Invalidate(){generation++;plan=null;appliedKey=null;retryAfter=DateTime.MinValue;}
        async Task DiscoverEngine(bool useSaved)
        {
            if(discovering||closing)return;discovering=true;
            try
            {
                string preferred=useSaved?saved.EnginePath:Input("EngineInput").Text.Trim().Trim('"');
                var result=await Task.Run(()=>Engine.Probe(preferred));if(closing)return;
                latestProbe=result;
                if(!String.IsNullOrEmpty(result.Path))
                {
                    bool changed=!String.Equals(saved.EnginePath,result.Path,StringComparison.OrdinalIgnoreCase);
                    if(changed){saved.EnginePath=result.Path;Invalidate();if(Store.LoadError==null)Store.Save(saved);}
                    if(changed||!useSaved||String.IsNullOrWhiteSpace(Input("EngineInput").Text))Input("EngineInput").Text=result.Path;
                }
                var color=(Color)ColorConverter.ConvertFromString(result.State=="ready"?"#23825B":result.State=="waiting"?"#A66B19":"#B64747");
                C<System.Windows.Shapes.Ellipse>("EngineStatusDot").Fill=new SolidColorBrush(color);
                Label("EngineStatusTitle").Text=result.Title;Label("EngineStatusTitle").Foreground=new SolidColorBrush(color);
                Label("EngineStatusDetail").Text=result.Detail;
                C<Button>("EngineSettingsLink").Visibility=result.State=="ready"?Visibility.Collapsed:Visibility.Visible;
            }
            catch(Exception e)
            {
                latestProbe=new EngineProbe{State="error",Title=Localization.T("检测未完成"),Detail=Localization.T("无法读取安装信息，请前往设置检查位置和运行权限。")};
                Label("EngineStatusTitle").Text=latestProbe.Title;Label("EngineStatusDetail").Text=latestProbe.Detail;
                Label("EngineStatusTitle").Foreground=Brushes.Firebrick;C<System.Windows.Shapes.Ellipse>("EngineStatusDot").Fill=Brushes.Firebrick;
                C<Button>("EngineSettingsLink").Visibility=Visibility.Visible;Store.Log(Localization.T("Wallpaper Engine 检测失败：")+e.GetType().Name);
            }
            finally{discovering=false;}
        }
        async Task Tick()
        {
            if(busy||auditing||closing)return;
            busy=true;
            try
            {
                await DiscoverEngine(true);if(closing)return;
                string previous=saved.TimeZoneId;DateTime now=SystemState.RefreshClock(saved);UpdateClock();if(previous!=saved.TimeZoneId)Invalidate();
                if(!saved.Enabled){Label("StatusText").Text=Localization.T("自动切换未启用");tray.Text=Localization.T("HorizonSync · 未启用");return;}
                saved.Validate(true);if(plan==null||plan.LocalDate!=TimeZoneInfo.ConvertTimeFromUtc(now,TimeZoneInfo.Local).Date)plan=new Plan(saved,now);
                Label("PreviewText").Text=Describe(plan,now);bool day=plan.IsDay(now);string identity=latestProbe==null?null:latestProbe.Identity;
                if(identity==null){appliedKey=null;Label("StatusText").Text=Localization.T("等待 Wallpaper Engine 启动…");return;}
                string key=identity+":"+day;if(appliedKey!=key&&now>=retryAfter)
                {
                    busy=true;var current=saved;int captured=generation;await Task.Run(()=>Engine.Apply(current,day));if(captured!=generation||closing)return;
                    appliedKey=key;Store.Log(Localization.T("已发送")+(day?Localization.T("白天"):Localization.T("夜晚"))+Localization.T("壁纸命令；显示器 ")+current.Monitor);
                }
                Label("StatusText").Text=appliedKey==key?Localization.T("已发送")+(day?Localization.T("白天"):Localization.T("夜晚"))+Localization.T("壁纸命令，请首次目视确认"):Localization.T("等待重试…");tray.Text="HorizonSync · "+(day?Localization.T("白天"):Localization.T("夜晚"));
            }catch(Exception e){Label("StatusText").Text=e.Message;retryAfter=DateTime.UtcNow.AddSeconds(60);Store.Log(e.Message);}finally{busy=false;}
        }
        async Task TestWallpaper(bool day)
        {
            if(busy||auditing)return;try{var draft=Draft();draft.Validate(true);busy=true;await Task.Run(()=>Engine.Apply(draft,day));appliedKey=null;retryAfter=DateTime.UtcNow.AddSeconds(30);Label("StatusText").Text=Localization.T("已发送")+(day?Localization.T("白天"):Localization.T("夜晚"))+Localization.T("测试命令；自动模式约 30 秒后对齐。");}
            catch(Exception e){MessageBox.Show(this,e.Message,"HorizonSync");}finally{busy=false;}
        }
        void Browse(string name,bool exe)
        {
            var d=new OpenFileDialog{Title=exe?Localization.T("选择 Wallpaper Engine"):Localization.T("选择壁纸文件"),Filter=exe?"Wallpaper Engine|wallpaper32.exe;wallpaper64.exe":Localization.T("壁纸文件|*.json;*.mp4;*.webm;*.html;*.htm;*.pkg;*.avi;*.mkv;*.wmv;*.gif;*.jpg;*.jpeg;*.png|所有文件|*.*")};if(d.ShowDialog(this)==true)Input(name).Text=d.FileName;
        }
        async Task PickWallpaper(string name)
        {
            var trigger=C<Button>(name=="DayPathInput"?"DayLibrary":"NightLibrary");trigger.IsEnabled=false;
            try
            {
                var dialog=new WallpaperPicker(this,Input("EngineInput").Text,Input(name).Text,name=="DayPathInput",view.Resources);
                if(dialog.ShowDialog()==true)Input(name).Text=dialog.SelectedPath;
                await dialog.PreviewCompletion;
            }catch(Exception e){MessageBox.Show(this,e.Message,"HorizonSync");}finally{trigger.IsEnabled=true;}
        }
        void ShowWindow(){Show();WindowState=WindowState.Normal;Activate();}
        string LanguageChoice(){var item=C<ComboBox>("LanguageSelect").SelectedItem as ComboBoxItem;return item==null?"auto":Convert.ToString(item.Tag);}
        async Task ChangeLanguage()
        {
            Localization.Use(LanguageChoice());Language=XmlLanguage.GetLanguage(Localization.IsEnglish?"en":"zh-CN");uiLanguage.Apply();UpdateMode();
            if(Checked("SystemRadio")&&locationState<0)Label("LocationInfo").Text=Localization.T("Windows 暂未提供位置，保留已有坐标。可检查 Windows 定位权限，或手动填写坐标。");
            else if(Checked("SystemRadio")&&locationState>0)Label("LocationInfo").Text=Localization.T("已采用 Windows 系统坐标。");
            if(!C<CheckBox>("StartupToggle").IsEnabled)Label("StartupInfo").Text=Localization.T("当前环境不允许读取启动项；在普通用户环境中可设置。");
            else Label("StartupInfo").Text=Localization.T("在当前用户登录后启动到托盘");
            var items=tray.ContextMenuStrip.Items;items[0].Text=Localization.T("打开设置");items[1].Text=Localization.T("立即重新判断");items[3].Text=Localization.T("彻底退出");
            UpdateClock();ShowPreview();await Tick();
        }
        void ShowLicense()
        {
            using(var stream=typeof(Program).Assembly.GetManifestResourceStream("HorizonSync.License"))
            using(var reader=new StreamReader(stream))
            {
                var content=new TextBox{Text=reader.ReadToEnd(),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=Double.NaN,FontFamily=new FontFamily("Consolas"),FontSize=13,Margin=new Thickness(20)};
                new Window{Title="GPL-3.0-only",Owner=this,Icon=Icon,Width=780,Height=620,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content}.ShowDialog();
            }
        }
        void RefreshBrandIcon(bool force=false)
        {
            if(closing)return;
            bool dark=false;
            try{dark=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","SystemUsesLightTheme",1))==0;}catch{}
            int pixels=Math.Max(16,(int)Math.Round(16*VisualTreeHelper.GetDpi(this).DpiScaleX));
            if(!force&&brandTrayIcon!=null&&brandDark==dark&&brandPixels==pixels)return;
            string variant=dark?"White":"Black";
            var assembly=typeof(Program).Assembly;
            using(var stream=assembly.GetManifestResourceStream("HorizonSync.Brand.Mark"+variant))
            {var frame=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);frame.Freeze();Icon=frame;}
            System.Drawing.Icon next;
            using(var stream=assembly.GetManifestResourceStream("HorizonSync.Brand.Icon"+variant))
            using(var source=new System.Drawing.Icon(stream,new System.Drawing.Size(pixels,pixels)))next=(System.Drawing.Icon)source.Clone();
            var previous=brandTrayIcon;brandTrayIcon=next;tray.Icon=next;if(previous!=null)previous.Dispose();
            brandDark=dark;brandPixels=pixels;
        }
        void PreferencesChanged(object s,UserPreferenceChangedEventArgs e)
        {if(!closing&&!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>RefreshBrandIcon()));}
        void Recheck(){if(closing||Dispatcher.HasShutdownStarted)return;Dispatcher.BeginInvoke(new Action(async()=>{Invalidate();await Tick();}));}
        void TimeChanged(object s,EventArgs e){Recheck();}
        void PowerChanged(object s,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Resume)Recheck();}
        void SessionChanged(object s,SessionSwitchEventArgs e){if(e.Reason==SessionSwitchReason.SessionUnlock)Recheck();}
    }
}
