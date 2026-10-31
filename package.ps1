param([string]$IsccPath)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
& (Join-Path $projectRoot 'build.ps1')
$appRoot = Join-Path $projectRoot 'build\app'
$stageRoot = Join-Path $projectRoot 'build\portable\HorizonSync'
$distRoot = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Force -Path $stageRoot,$distRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $appRoot 'HorizonSync.exe') -Destination $stageRoot -Force
Copy-Item -LiteralPath (Join-Path $appRoot 'HorizonSync.exe.config') -Destination $stageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $stageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\USAGE.zh-CN.md') -Destination (Join-Path $stageRoot '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\USAGE.md') -Destination (Join-Path $stageRoot 'README.md') -Force
[IO.File]::WriteAllText((Join-Path $stageRoot 'portable.flag'),'Portable mode: settings and logs are stored in the data subfolder.')
$portableArchive = Join-Path $distRoot 'HorizonSync-0.2.0-preview.1-Portable.zip'
# Explicit file list excludes any old user data or logs left beside a staged app.
$portableFiles = @('HorizonSync.exe','HorizonSync.exe.config','LICENSE','README.md','使用说明.md','portable.flag') | ForEach-Object { Join-Path $stageRoot $_ }
Compress-Archive -LiteralPath $portableFiles -DestinationPath $portableArchive -Force
if ($IsccPath) {
    & $IsccPath (Join-Path $projectRoot 'installer\HorizonSync.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourceArchive = Join-Path $distRoot 'HorizonSync-0.2.0-preview.1-source.zip'
$sourceDirectories = @('src','tests','docs','installer','assets','.github')
$sourceFiles = @('README.md','README.zh-CN.md','LICENSE','.gitignore','HorizonSync.csproj','HorizonSync.exe.config','build.ps1','package.ps1','CODEX_HANDOFF.md') | ForEach-Object { Get-Item -LiteralPath (Join-Path $projectRoot $_) }
foreach ($sourceDirectory in $sourceDirectories) { $sourceFiles += Get-ChildItem -LiteralPath (Join-Path $projectRoot $sourceDirectory) -File -Recurse -Force }
$localDesignPreviews = @('assets\brand\approved-concept.png','assets\brand\HorizonSync-brand-preview.png') | ForEach-Object { Join-Path $projectRoot $_ }
$sourceFiles = $sourceFiles | Where-Object { $_.FullName -notin $localDesignPreviews }
$archiveStream = [IO.File]::Open($sourceArchive,[IO.FileMode]::Create)
try {
    $sourceZip = [IO.Compression.ZipArchive]::new($archiveStream,[IO.Compression.ZipArchiveMode]::Create,$true)
    try {
        foreach ($sourceFile in $sourceFiles) {
            $relativeName = $sourceFile.FullName.Substring($projectRoot.Length + 1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip,$sourceFile.FullName,('HorizonSync-dev/' + $relativeName),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $sourceZip.Dispose() }
} finally { $archiveStream.Dispose() }
$hashFiles = Get-ChildItem -LiteralPath $distRoot -File | Where-Object { $_.Extension -in '.exe','.zip' }
$checksumLines = $hashFiles | ForEach-Object { $artifactHash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256; $artifactHash.Hash.ToLowerInvariant() + '  ' + $_.Name }
[IO.File]::WriteAllLines((Join-Path $distRoot 'SHA256SUMS.txt'),[string[]]$checksumLines)
Write-Output ('Portable package: ' + $portableArchive)
Write-Output ('Source package: ' + $sourceArchive)
