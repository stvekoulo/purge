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
begin
  Result := '';
  if not IsPurgeUpdate then Exit;
  ParentId := StrToIntDef(ExpandConstant('{param:PURGEPID|0}'), 0);
  if ParentId <= 0 then begin
    Result := 'Impossible de fermer Purge. / Unable to close Purge.';
    Exit;
  end;
  Parent := OpenProcess($00100000, False, ParentId);
  if Parent = 0 then Exit; { The application has already exited. }
  try
    if WaitForSingleObject(Parent, 30000) <> 0 then
      Result := 'Fermez Purge puis réessayez. / Close Purge and try again.';
  finally
    CloseHandle(Parent);
  end;
end;
