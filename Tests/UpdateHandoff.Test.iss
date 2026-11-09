[Setup]
AppId=PurgeUpdateHandoffTest
AppName=Purge Update Handoff Test
AppVersion=1.0
DefaultDirName={#SourcePath}\..\artifacts\handoff-test\installed
OutputDir=..\artifacts\handoff-test
OutputBaseFilename=HandoffTest
Uninstallable=no
CreateUninstallRegKey=no
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
Compression=none

[Files]
Source: "bin\Debug\net10.0-windows\UpdateTests.*"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{app}\UpdateTests.exe"; Parameters: "--installer-probe"; Flags: runasoriginaluser; Check: IsPurgeUpdate

#include "..\installer\UpdateHandoff.iss"
