param(
    [string]$Version = "0.3.1",
    [string]$FfmpegDirectory = "",
    [string]$InnoSetupCompiler = ""
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
$InstallerPath = Join-Path $OutputRoot "VideoBatchProcessor-$Version-$Rid-setup.exe"
$PortableZipPath = Join-Path $OutputRoot "VideoBatchProcessor-$Version-$Rid.zip"
$InstallerScript = Join-Path $Root "packaging/windows/VideoBatchProcessor.iss"
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
Get-ChildItem $FfmpegDirectory -Filter "*.txt" -File -ErrorAction SilentlyContinue |
    Copy-Item -Destination $ToolsDirectory

foreach ($RequiredPath in @(
    (Join-Path $PublishDirectory "VideoBatchProcessor.exe"),
    (Join-Path $PublishDirectory "WebUi/index.html"),
    (Join-Path $PublishDirectory "WebUi/unam-logo-blue.png"),
    (Join-Path $PublishDirectory "WebUi/ifc-logo.png"),
    (Join-Path $PublishDirectory "WebUi/pasted-table.js"),
    (Join-Path $PublishDirectory "WebUi/video-setup-panel.js"),
    (Join-Path $ToolsDirectory "ffmpeg.exe"),
    (Join-Path $ToolsDirectory "ffprobe.exe")
)) {
    if (-not (Test-Path $RequiredPath)) { throw "El paquete quedó incompleto: falta $RequiredPath" }
}

# The portable archive contains the same complete publish directory used by the installer.
Compress-Archive -Path (Join-Path $PublishDirectory "*") -DestinationPath $PortableZipPath -Force
if (-not (Test-Path $PortableZipPath)) { throw "No se creó el ZIP portátil: $PortableZipPath" }

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $InnoSetupCompiler = (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue).Source
}
if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler) -or -not (Test-Path $InnoSetupCompiler)) {
    throw "No se encontró ISCC.exe de Inno Setup. Instala Inno Setup 6 o indica -InnoSetupCompiler."
}

& $InnoSetupCompiler "/DMyAppVersion=$Version" "/DSourceDir=$PublishDirectory" `
    "/DOutputDir=$OutputRoot" "/DIconFile=$(Join-Path $Root 'src/VideoBatchProcessor.App/Assets/Packaging/VideoBatchProcessor.ico')" `
    $InstallerScript
if ($LASTEXITCODE -ne 0) { throw "Falló la creación del instalador Windows." }
if (-not (Test-Path $InstallerPath)) { throw "No se creó el instalador: $InstallerPath" }

$Digest = (Get-FileHash $InstallerPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$InstallerPath.sha256" -Value "$Digest  $(Split-Path $InstallerPath -Leaf)" -NoNewline
$PortableDigest = (Get-FileHash $PortableZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$PortableZipPath.sha256" -Value "$PortableDigest  $(Split-Path $PortableZipPath -Leaf)" -NoNewline
Write-Host "Instalador creado: $InstallerPath"
Write-Host "ZIP portátil creado: $PortableZipPath"
