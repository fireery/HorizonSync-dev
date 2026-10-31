#ifndef AppBuildDir
  #define AppBuildDir "..\build\app"
#endif
#ifndef AppVersion
  #define AppVersion "0.2.0-preview"
#endif
[Setup]
AppId={{D4A7B69D-48D4-4B79-BD55-E29F1632A9BA}
AppName=HorizonSync
AppVersion={#AppVersion}
VersionInfoVersion=0.2.0.0
AppPublisher=HorizonSync contributors
DefaultDirName={localappdata}\Programs\HorizonSync
DefaultGroupName=HorizonSync
PrivilegesRequired=lowest
MinVersion=10.0.15063
WizardStyle=modern
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=HorizonSync-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\HorizonSync.exe
SetupIconFile=..\assets\HorizonSync.ico
CloseApplications=yes
RestartApplications=no
[Languages]
Name: english; MessagesFile: "compiler:Default.isl"
Name: chinesesimplified; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#AppBuildDir}\HorizonSync.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppBuildDir}\HorizonSync.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{autoprograms}\HorizonSync"; Filename: "{app}\HorizonSync.exe"
Name: "{autodesktop}\HorizonSync"; Filename: "{app}\HorizonSync.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\HorizonSync.exe"; Description: "Open HorizonSync"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox('.NET Framework 4.8 or newer is required. Install it from Microsoft, then run this installer again.', mbError, MB_OK);
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RunCommand: String;
begin
  if (CurUninstallStep = usUninstall) and RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'HorizonSync', RunCommand) then
    if CompareText(RunCommand, '"' + ExpandConstant('{app}\HorizonSync.exe') + '" --tray') = 0 then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'HorizonSync');
end;
