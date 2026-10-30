param([switch]$SkipCli)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $projectRoot 'build\tests'
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
& $compilerPath /nologo /target:exe /utf8output ('/out:' + (Join-Path $testRoot 'wallpaper64.exe')) (Join-Path $PSScriptRoot 'FakeEngine.cs')
if ($LASTEXITCODE -ne 0) { throw 'Fake engine build failed.' }
& $compilerPath /nologo /target:exe /utf8output /r:System.Web.Extensions.dll ('/resource:' + (Join-Path $projectRoot 'src\Locales\en.json') + ',HorizonSync.Locales.en.json') ('/out:' + (Join-Path $testRoot 'Harness.exe')) (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\Localization.cs') (Join-Path $PSScriptRoot 'Harness.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test harness build failed.' }
if ($SkipCli) {
    Write-Warning 'Mock CLI process tests are explicitly skipped in this environment.'
    & (Join-Path $testRoot 'Harness.exe')
} else {
    & (Join-Path $testRoot 'Harness.exe') (Join-Path $testRoot 'wallpaper64.exe')
}
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
