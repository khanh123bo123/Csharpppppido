param(
    [string]$ApkPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
function Resolve-ApkSourcePath {
    param([string]$InputApkPath)

    if (-not [string]::IsNullOrWhiteSpace($InputApkPath)) {
        $resolved = Resolve-Path -Path $InputApkPath -ErrorAction Stop
        return $resolved.Path
    }

    $searchRoot = Join-Path $repoRoot "TouristGuideApp\bin"
    if (-not (Test-Path -Path $searchRoot -PathType Container)) {
        throw "APK path was not provided and no build output directory was found at: $searchRoot"
    }

    $latestApk = Get-ChildItem -Path $searchRoot -Recurse -Filter *.apk -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $latestApk) {
        throw "APK path was not provided and no .apk file was found under TouristGuideApp\\bin."
    }

    return $latestApk.FullName
}

$sourcePath = Resolve-ApkSourcePath -InputApkPath $ApkPath

if (-not (Test-Path -Path $sourcePath -PathType Leaf)) {
    throw "APK file not found: $sourcePath"
}

if (-not [string]::Equals([System.IO.Path]::GetExtension($sourcePath), ".apk", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Input file must be an .apk file."
}

$apkRootDirectory = Join-Path $repoRoot "TouristGuideWeb\wwwroot\apk"
$releaseDirectory = Join-Path $apkRootDirectory "releases"
New-Item -Path $releaseDirectory -ItemType Directory -Force | Out-Null

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$versionedFileName = "TouristGuideApp-$timestamp.apk"
$versionedDestinationPath = Join-Path $releaseDirectory $versionedFileName
Copy-Item -Path $sourcePath -Destination $versionedDestinationPath -Force

$latestPath = Join-Path $apkRootDirectory "latest.apk"
Copy-Item -Path $versionedDestinationPath -Destination $latestPath -Force

$fileInfo = Get-Item -Path $versionedDestinationPath
$hash = (Get-FileHash -Path $versionedDestinationPath -Algorithm SHA256).Hash

$encodedFileName = [System.Uri]::EscapeDataString($versionedFileName)
$directPath = "/apk/direct/$encodedFileName"

$metadata = [ordered]@{
    fileName = $versionedFileName
    originalFileName = [System.IO.Path]::GetFileName($sourcePath)
    sizeBytes = $fileInfo.Length
    updatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    sha256 = $hash
    directPath = $directPath
}

$metadataPath = Join-Path $apkRootDirectory "latest.json"
$currentMetadataPath = Join-Path $apkRootDirectory "current.json"
$metadataJson = $metadata | ConvertTo-Json -Depth 5
$metadataJson | Set-Content -Path $metadataPath -Encoding UTF8
$metadataJson | Set-Content -Path $currentMetadataPath -Encoding UTF8

Write-Host "APK published successfully:" -ForegroundColor Green
Write-Host "- Versioned APK: $versionedDestinationPath"
Write-Host "- Current APK:   $latestPath"
Write-Host "Dynamic URLs (when TouristGuideWeb is running):" -ForegroundColor Green
Write-Host "- /apk"
Write-Host "- /apk/qr.png"
Write-Host "- $directPath"
