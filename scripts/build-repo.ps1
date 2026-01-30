# Build Poster Tags plugin and create a .zip for Jellyfin plugin repository.
# Run from repo root. Outputs: dist/Jellyfin.Plugin.PosterTags_<version>.zip and prints manifest entry.

$ErrorActionPreference = "Stop"
$RepoRoot = Join-Path $PSScriptRoot ".."
$ProjectDir = Join-Path $RepoRoot "Jellyfin.Plugin.PosterTags"
$PublishDir = Join-Path $RepoRoot "publish"
$DistDir = Join-Path $RepoRoot "dist"

# Read version from manifest.json
$manifestPath = Join-Path $ProjectDir "manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$version = $manifest.version
$guid = $manifest.guid
$name = $manifest.name

Write-Host "Building $name v$version..." -ForegroundColor Cyan
dotnet publish "$ProjectDir\Jellyfin.Plugin.PosterTags.csproj" -c Release -o $PublishDir | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Copy manifest.json into publish (catalog zip needs it at root)
Copy-Item $manifestPath -Destination "$PublishDir\manifest.json" -Force

# Create dist folder and zip
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
$zipName = "Jellyfin.Plugin.PosterTags_$version.zip"
$zipPath = Join-Path $DistDir $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipPath -Force

# Also copy to releases/ for direct raw URL (avoids GitHub release redirect 404 issues in Jellyfin)
$ReleasesDir = Join-Path $RepoRoot "releases"
New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null
Copy-Item $zipPath -Destination (Join-Path $ReleasesDir $zipName) -Force

# MD5 checksum (32-char lowercase, no dashes)
$hash = (Get-FileHash -Path $zipPath -Algorithm MD5).Hash.ToLowerInvariant()

Write-Host "Created: $zipPath" -ForegroundColor Green
Write-Host "Copied to: $ReleasesDir\$zipName (use this URL in catalog for reliable install)" -ForegroundColor Green
Write-Host "MD5: $hash" -ForegroundColor Green
Write-Host ""
Write-Host "Add this repository in Jellyfin: Dashboard -> Plugins -> Repositories" -ForegroundColor Yellow
Write-Host "Use the URL to your manifest-catalog.json (e.g. raw GitHub URL)." -ForegroundColor Yellow
Write-Host ""
Write-Host "Recommended sourceUrl (direct, no redirects):" -ForegroundColor Cyan
Write-Host "  https://raw.githubusercontent.com/bestopensors/JF-plugin/main/releases/$zipName" -ForegroundColor White
Write-Host ""
Write-Host "Paste this into manifest-catalog.json:" -ForegroundColor Cyan
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$entry = @"
  {
    "version": "$version",
    "changelog": "See https://github.com/bestopensors/JF-plugin/releases",
    "targetAbi": "10.11.0.0",
    "sourceUrl": "https://raw.githubusercontent.com/bestopensors/JF-plugin/main/releases/$zipName",
    "checksum": "$hash",
    "timestamp": "$timestamp"
  }
"@
Write-Host $entry
