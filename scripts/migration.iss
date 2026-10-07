// Previous installations are stopped before the new identity migrates data.
procedure StopLegacyCore;
var Code: Integer;
begin
  if FileExists(ExpandConstant('{localappdata}\Programs\Gopeed Native\Engine\gopeed-core.exe')) then
    Exec(ExpandConstant('{localappdata}\Programs\Gopeed Native\Engine\gopeed-core.exe'),
      '--data "' + ExpandConstant('{localappdata}\GopeedNative') + '" --shutdown',
      '', SW_HIDE, ewWaitUntilTerminated, Code);
end;
