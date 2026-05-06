; Inno Setup Script for SysMonBar
; Build the installer by right-clicking this file and selecting "Compile" (requires Inno Setup installed)

[Setup]
AppId={{D3E5F6B7-C8A9-4B0D-9E1F-2A3B4C5D6E7F}}
AppName=SysMonBar
AppVersion=2.0.2
AppPublisher=SysMonBar Team
DefaultDirName={commonpf}\SysMonBar
DefaultGroupName=SysMonBar
; Disable the "Start Menu Folder" page
DisableProgramGroupPage=yes
; Application icon
UninstallDisplayIcon={app}\SysMonBar.exe
SetupIconFile=icon.ico
OutputDir=.
OutputBaseFilename=SysMonBar_Installer
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
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Include the ico file if it exists (for internal use by setup)
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SysMonBar"; Filename: "{app}\SysMonBar.exe"
Name: "{autodesktop}\SysMonBar"; Filename: "{app}\SysMonBar.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SysMonBar.exe"; Description: "{cm:LaunchProgram,SysMonBar}"; Flags: nowait postinstall skipifsilent shellexec
