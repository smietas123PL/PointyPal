#define AppPublisher "PointyPal"

#ifndef AppName
#define AppName "PointyPal"
#endif

#ifndef AppVersion
#define AppVersion "0.21.0"
#endif

#ifndef ReleaseLabel
#define ReleaseLabel "private-rc.1"
#endif

#ifndef RuntimeTarget
#define RuntimeTarget "win-x64"
#endif

#ifndef PortableDir
#define PortableDir "..\artifacts\PointyPal-portable"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\installer"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename AppName + "-v" + AppVersion + "-" + ReleaseLabel + "-" + RuntimeTarget + "-setup"
#endif

#ifndef InstallerExcludes
#define InstallerExcludes "config.json,logs\*,debug\*,history\*,usage\*,secrets\*,.env,.env.*,*.log,*.tmp,*.bak,*.secret,*.key,*.pem,*.pfx,*.pdb"
#endif

[Setup]
AppId={{4D663364-4581-4B7B-9722-DF3AF3E8C7F0}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion} {#ReleaseLabel}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\PointyPal.exe

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PortableDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "{#InstallerExcludes}"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\PointyPal.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\PointyPal.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PointyPal.exe"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent unchecked
