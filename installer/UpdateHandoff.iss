[Code]
function IsPurgeUpdate: Boolean;
begin
  Result := ExpandConstant('{param:PURGEUPDATE|0}') = '1';
end;

function OpenProcess(Access: LongWord; InheritHandle: Boolean; ProcessId: LongWord): THandle;
  external 'OpenProcess@kernel32.dll stdcall';
function WaitForSingleObject(Handle: THandle; Milliseconds: LongWord): LongWord;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ParentId: Integer;
  Parent: THandle;
  ResultCode: Integer;
begin
  Result := '';
  if IsPurgeUpdate then
  begin
    ParentId := StrToIntDef(ExpandConstant('{param:PURGEPID|0}'), 0);
    if ParentId > 0 then
    begin
      Parent := OpenProcess($00100000, False, ParentId);
      if Parent <> 0 then
      begin
        try
          if WaitForSingleObject(Parent, 30000) <> 0 then
            Result := 'Fermez Purge puis réessayez. / Close Purge and try again.';
        finally
          CloseHandle(Parent);
        end;
      end;
    end;
  end;

  // Terminate any running Purge.exe process (e.g. background system tray icon)
  // so that locked binaries (clrjit.dll, Purge.dll, etc.) are freed before file replacement.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Purge.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Purge.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
  end;
end;
