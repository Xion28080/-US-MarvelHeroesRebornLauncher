param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root "release-package"
$launcherPublish = Join-Path $root "bin\$Configuration\net8.0-windows\$Runtime\publish"
$updaterPublish = Join-Path $root "MHRebornLauncher.Updater\bin\$Configuration\net8.0-windows\$Runtime\publish"

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}
New-Item -ItemType Directory -Path $out | Out-Null

function Invoke-Publish {
    param([string]$ProjectPath)

    dotnet publish $ProjectPath -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:EnableCompressionInSingleFile=true
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing failed for $ProjectPath (exit code $LASTEXITCODE)."
    }
}

Invoke-Publish (Join-Path $root "MHRebornLauncher.csproj")
Invoke-Publish (Join-Path $root "MHRebornLauncher.Updater\MHRebornLauncher.Updater.csproj")

Copy-Item (Join-Path $launcherPublish "MHRebornLauncher.exe") (Join-Path $out "MHRebornLauncher.exe") -Force
Copy-Item (Join-Path $updaterPublish "MHRebornLauncher.Updater.exe") (Join-Path $out "MHRebornLauncher.Updater.exe") -Force

$readme = @"
Marvel Heroes Reborn Launcher
Version $Version

Place both files in your Marvel Heroes install folder, next to the UnrealEngine3 folder:

- MHRebornLauncher.exe
- MHRebornLauncher.Updater.exe

Open MHRebornLauncher.exe to launch the game.
"@
Set-Content -Path (Join-Path $out "README.txt") -Value $readme -Encoding UTF8

$zip = Join-Path $root "MHRebornLauncher-v$Version.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -Force

Write-Host "Created $zip"
