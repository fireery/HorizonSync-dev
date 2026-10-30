param([string]$OutDir = (Join-Path $PSScriptRoot 'build\app'))
$ErrorActionPreference = 'Stop'
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $frameworkRoot 'csc.exe'))) {
    $frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compilerPath = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework C# compiler was not found.' }
$targetRoot = [IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
$sourceRoot = Join-Path $PSScriptRoot 'src'
$assetRoot = Join-Path $PSScriptRoot 'assets'
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',
    ('/out:' + (Join-Path $targetRoot 'HorizonSync.exe')),
    ('/win32manifest:' + (Join-Path $sourceRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $assetRoot 'HorizonSync.ico')),
    ('/resource:' + (Join-Path $sourceRoot 'MainWindow.xaml') + ',HorizonSync.MainWindow.xaml'),
    ('/resource:' + (Join-Path $sourceRoot 'Locales\en.json') + ',HorizonSync.Locales.en.json'),
    ('/resource:' + (Join-Path $PSScriptRoot 'LICENSE') + ',HorizonSync.License'),
    ('/resource:' + (Join-Path $assetRoot 'HorizonSync-Black.ico') + ',HorizonSync.Brand.IconBlack'),
    ('/resource:' + (Join-Path $assetRoot 'HorizonSync-White.ico') + ',HorizonSync.Brand.IconWhite'),
    ('/resource:' + (Join-Path $assetRoot 'HorizonSync-Black.png') + ',HorizonSync.Brand.MarkBlack'),
    ('/resource:' + (Join-Path $assetRoot 'HorizonSync-White.png') + ',HorizonSync.Brand.MarkWhite'),
    '/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:System.Device.dll','/r:System.Xaml.dll',
    ('/r:' + (Join-Path $frameworkRoot 'WPF\WindowsBase.dll')),
    ('/r:' + (Join-Path $frameworkRoot 'WPF\PresentationCore.dll')),
    ('/r:' + (Join-Path $frameworkRoot 'WPF\PresentationFramework.dll')))
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object FullName
& $compilerPath @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'HorizonSync build failed.' }
$configSource = Join-Path $PSScriptRoot 'HorizonSync.exe.config'
$configTarget = Join-Path $targetRoot 'HorizonSync.exe.config'
if (-not [string]::Equals($configSource,$configTarget,[StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $configSource -Destination $configTarget -Force
}
Write-Output ('Built: ' + (Join-Path $targetRoot 'HorizonSync.exe'))
if (-not [string]::Equals((Join-Path $PSScriptRoot 'LICENSE'),(Join-Path $targetRoot 'LICENSE'),[StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $targetRoot 'LICENSE') -Force
}
