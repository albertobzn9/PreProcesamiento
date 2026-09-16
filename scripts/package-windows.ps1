param(
    [string]$Version = "0.1.0",
    [string]$FfmpegDirectory = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src/VideoBatchProcessor.App/VideoBatchProcessor.App.csproj"
$Rid = "win-x64"
if ([string]::IsNullOrWhiteSpace($FfmpegDirectory)) {
    $FfmpegDirectory = Join-Path $Root "vendor/ffmpeg/$Rid"
}

$Ffmpeg = Join-Path $FfmpegDirectory "ffmpeg.exe"
$Ffprobe = Join-Path $FfmpegDirectory "ffprobe.exe"
if (-not (Test-Path $Ffmpeg) -or -not (Test-Path $Ffprobe)) {
    throw "Faltan ffmpeg.exe y ffprobe.exe en $FfmpegDirectory"
}

$OutputRoot = Join-Path $Root "artifacts/release/$Version/$Rid"
$PublishDirectory = Join-Path $OutputRoot "VideoBatchProcessor"
$ZipPath = Join-Path $OutputRoot "VideoBatchProcessor-$Version-$Rid.zip"
Remove-Item $OutputRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item $PublishDirectory -ItemType Directory -Force | Out-Null

dotnet publish $Project `
    --configuration Release `
    --runtime $Rid `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    --output $PublishDirectory
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de .NET." }

$ToolsDirectory = Join-Path $PublishDirectory "tools"
New-Item $ToolsDirectory -ItemType Directory -Force | Out-Null
Copy-Item $Ffmpeg (Join-Path $ToolsDirectory "ffmpeg.exe")
Copy-Item $Ffprobe (Join-Path $ToolsDirectory "ffprobe.exe")

foreach ($RequiredPath in @(
    (Join-Path $PublishDirectory "VideoBatchProcessor.exe"),
    (Join-Path $PublishDirectory "WebUi/index.html"),
    (Join-Path $ToolsDirectory "ffmpeg.exe"),
    (Join-Path $ToolsDirectory "ffprobe.exe")
)) {
    if (-not (Test-Path $RequiredPath)) { throw "El paquete quedó incompleto: falta $RequiredPath" }
}

Compress-Archive -Path "$PublishDirectory/*" -DestinationPath $ZipPath -Force
Write-Host "Paquete creado: $ZipPath"
