; One-file installer for non-technical Windows users.
; SourceDir, OutputDir, IconFile, and MyAppVersion are passed by package-windows.ps1.

#ifndef MyAppVersion
  #define MyAppVersion "0.3.1"
#endif
#ifndef SourceDir
  #error SourceDir must point to the published application directory.
#endif
#ifndef OutputDir
  #error OutputDir must point to the release output directory.
#endif
#ifndef IconFile
  #error IconFile must point to the Windows application icon.
#endif

[Setup]
AppId={{D4B3F51A-1C17-4D5A-92D7-7B7346372D41}
AppName=Video Batch Processor
AppVersion={#MyAppVersion}
AppPublisher=Instituto de Fisiologia Celular, UNAM
AppPublisherURL=https://github.com/albertobzn9/PreProcesamiento
AppSupportURL=https://github.com/albertobzn9/PreProcesamiento/issues
DefaultDirName={localappdata}\Programs\Video Batch Processor
DefaultGroupName=Video Batch Processor
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=VideoBatchProcessor-{#MyAppVersion}-win-x64-setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\VideoBatchProcessor.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UsePreviousAppDir=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Video Batch Processor"; Filename: "{app}\VideoBatchProcessor.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Video Batch Processor"; Filename: "{app}\VideoBatchProcessor.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\VideoBatchProcessor.exe"; Description: "Launch Video Batch Processor"; Flags: nowait postinstall skipifsilent
