; Inno Setup Script for SysMonBar
; Build the installer by right-clicking this file and selecting "Compile" (requires Inno Setup installed)
; CI can override these values with ISCC /DAppVersion=..., /DOutputBaseFilename=..., and /DPublishDir=...

#ifndef AppVersion
  #define AppVersion "2.1.0"
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "SysMonBar_Installer"
#endif

#ifndef PublishDir
  #define PublishDir "publish"
#endif

[Setup]
AppId={{D3E5F6B7-C8A9-4B0D-9E1F-2A3B4C5D6E7F}}
AppName=SysMonBar
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}.0
AppPublisher=SysMonBar Team
DefaultDirName={autopf}\SysMonBar
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultGroupName=SysMonBar
; Disable the "Start Menu Folder" page
DisableProgramGroupPage=yes
; Application icon
UninstallDisplayIcon={app}\SysMonBar.exe
SetupIconFile=icon.ico
OutputDir=.\Installer
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Require Administrator to install (and for the app itself)
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Include all files from the publish directory
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Include the ico file
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SysMonBar"; Filename: "{app}\SysMonBar.exe"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\SysMonBar"; Filename: "{app}\SysMonBar.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\SysMonBar.exe"; Description: "{cm:LaunchProgram,SysMonBar}"; Flags: nowait postinstall skipifsilent shellexec
