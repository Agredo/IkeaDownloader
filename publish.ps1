#!/usr/bin/env pwsh
# publish.ps1 — Build AOT single-file binaries for Windows-x64 and Linux-x64
# Requires: .NET 10 SDK  (https://dotnet.microsoft.com/download)
# Run from the solution root:  ./publish.ps1

$ErrorActionPreference = "Stop"
$project = "IkeaDownloader.Console/IkeaDownloader.Console.csproj"
$outBase  = "publish"

# Verify SDK version
$sdkVer = dotnet --version 2>$null
if (-not $sdkVer.StartsWith("10.")) {
    Write-Warning "Expected .NET 10 SDK, found: $sdkVer"
}

$targets = @(
    @{ rid = "win-x64";   ext = ".exe" },
    @{ rid = "linux-x64"; ext = ""     }
)

foreach ($t in $targets) {
    $rid = $t.rid
    $out = "$outBase/$rid"

    Write-Host ""
    Write-Host "==> Publishing $rid …" -ForegroundColor Cyan

    dotnet publish $project `
        --configuration Release `
        --runtime $rid `
        --output $out

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed for $rid"
        exit 1
    }

    $binary = Get-ChildItem $out -Filter "ikea-dl$($t.ext)" | Select-Object -First 1
    if ($binary) {
        $size = [math]::Round($binary.Length / 1MB, 1)
        Write-Host "    ✔  $($binary.Name)  ($size MB)" -ForegroundColor Green
        Write-Host "       $($binary.FullName)"
    }
}

Write-Host ""
Write-Host "All done. Binaries are in ./$outBase/" -ForegroundColor Green
