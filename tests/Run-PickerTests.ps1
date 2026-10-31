param([switch]$Ui, [switch]$Fixture, [switch]$English)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $projectRoot 'build\tests'
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $frameworkRoot 'csc.exe'))) { $frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$arguments = @('/nologo','/target:exe','/utf8output','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:System.Xaml.dll',
    ('/out:' + (Join-Path $testRoot 'PickerHarness.exe')),
    ('/r:' + (Join-Path $frameworkRoot 'WPF\WindowsBase.dll')),
    ('/r:' + (Join-Path $frameworkRoot 'WPF\PresentationCore.dll')),
    ('/r:' + (Join-Path $frameworkRoot 'WPF\PresentationFramework.dll')),
    ('/resource:' + (Join-Path $projectRoot 'src\Locales\en.json') + ',HorizonSync.Locales.en.json'),
    ('/resource:' + (Join-Path $projectRoot 'src\MainWindow.xaml') + ',HorizonSync.MainWindow.xaml'))
$sources = @('AssemblyInfo','Core','Localization','WallpaperLibrary','WallpaperPreview','WallpaperAuthors','WallpaperPicker') | ForEach-Object { Join-Path $projectRoot ('src\' + $_ + '.cs') }
& (Join-Path $frameworkRoot 'csc.exe') @arguments @sources (Join-Path $PSScriptRoot 'PickerHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'Picker harness build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'HorizonSync.exe.config') -Destination (Join-Path $testRoot 'PickerHarness.exe.config') -Force
$runArgs = @()
if ($Ui) { $runArgs += '--ui' }
if ($Fixture) { $runArgs += '--fixture' }
if ($English) { $runArgs += '--en' }
& (Join-Path $testRoot 'PickerHarness.exe') @runArgs
if ($LASTEXITCODE -ne 0) { throw 'Picker tests failed.' }
