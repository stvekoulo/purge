#ifndef MyAppVersion
  #define MyAppVersion "1.0.4"
#endif
#ifndef MyArch
  #define MyArch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64"
#endif

[Setup]
AppId={{8E6C34A2-7F19-4C8B-A540-9D31E20B1045}
AppName=Purge
AppVersion={#MyAppVersion}
AppVerName=Purge {#MyAppVersion} ({#MyArch})
AppPublisher=Steven Koulo
AppPublisherURL=https://github.com/stvekoulo/purge
AppSupportURL=https://github.com/stvekoulo/purge/issues
AppUpdatesURL=https://github.com/stvekoulo/purge/releases
DefaultDirName={autopf}\Purge
DefaultGroupName=Purge
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=Purge-{#MyAppVersion}-win-{#MyArch}-Setup
SetupIconFile=..\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Purge.exe
UninstallDisplayName=Purge
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
#if MyArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#elif MyArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Purge"; Filename: "{app}\Purge.exe"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{autodesktop}\Purge"; Filename: "{app}\Purge.exe"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\Purge.exe"; Description: "{cm:LaunchProgram,Purge}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Purge.exe"; Flags: nowait runasoriginaluser; Check: IsPurgeUpdate

[UninstallRun]
Filename: "{app}\Purge.exe"; Parameters: "--uninstall-cleanup"; Flags: runhidden; RunOnceId: "CleanupPurgeTasks"

#include "UpdateHandoff.iss"
