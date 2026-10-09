const
  HarborUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B652BEF3-0741-4B5E-9066-C6F3EBF18622}_is1';
var
  PreviousInstallDir: String;

function IsHarborCommand(Command: String): Boolean;
begin
  Command := LowerCase(Command);
  Result := (Pos('harbor.exe', Command) > 0) or (Pos('gopeed.native.exe', Command) > 0);
end;

function IsManagedInstallation(Directory: String): Boolean;
begin
  Result := (Directory <> '') and (Length(Directory) > Length(ExtractFileDrive(Directory)) + 1) and
    (FileExists(AddBackslash(Directory) + 'Harbor.exe') or
     FileExists(AddBackslash(Directory) + 'Gopeed.Native.exe'));
end;

procedure InitializeWizard;
begin
  RegQueryStringValue(HKCU64, HarborUninstallKey, 'InstallLocation', PreviousInstallDir);
  // Keep custom locations, but migrate the original default identity to Harbor.
  if CompareText(RemoveBackslashUnlessRoot(PreviousInstallDir),
    ExpandConstant('{localappdata}\Programs\Gopeed Native')) = 0 then
    WizardForm.DirEdit.Text := ExpandConstant('{localappdata}\Programs\Harbor');
end;

procedure StopInstallation(Directory, DataDirectory: String);
var
  Code, Index: Integer;
  CorePath, ImagePath, RelativePath: String;
  Locator, Services, Processes, Process: Variant;
begin
  if not IsManagedInstallation(Directory) then Exit;
  CorePath := AddBackslash(Directory) + 'Engine\harbor-core.exe';
  if not FileExists(CorePath) then
    CorePath := AddBackslash(Directory) + 'Engine\gopeed-core.exe';
  if FileExists(CorePath) then
    Exec(CorePath, '--data "' + DataDirectory + '" --shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);

  // Stop only executables inside this installation, including browser-held hosts.
  Locator := CreateOleObject('WbemScripting.SWbemLocator');
  Services := Locator.ConnectServer('', 'root\CIMV2');
  Processes := Services.ExecQuery('SELECT * FROM Win32_Process WHERE ' +
    'Name = "Harbor.exe" OR Name = "Gopeed.Native.exe" OR ' +
    'Name = "harbor-core.exe" OR Name = "gopeed-core.exe" OR ' +
    'Name = "harbor-browser-host.exe" OR Name = "gopeed-browser-host.exe"');
  for Index := 0 to Processes.Count - 1 do
  begin
    Process := Processes.ItemIndex(Index);
    if not VarIsNull(Process.ExecutablePath) then
    begin
      ImagePath := Process.ExecutablePath;
      RelativePath := LowerCase(Copy(ImagePath, Length(AddBackslash(Directory)) + 1, MaxInt));
      if (CompareText(Copy(ImagePath, 1, Length(AddBackslash(Directory))), AddBackslash(Directory)) = 0) and
        ((RelativePath = 'harbor.exe') or (RelativePath = 'gopeed.native.exe') or
         (RelativePath = 'engine\harbor-core.exe') or (RelativePath = 'engine\gopeed-core.exe') or
         (RelativePath = 'engine\harbor-browser-host.exe') or (RelativePath = 'engine\gopeed-browser-host.exe')) then
      begin
        Log('Stopping installation process: ' + ImagePath);
        Process.Terminate(0);
      end;
    end;
  end;
end;

procedure StopInstalledVersions;
begin
  StopInstallation(ExpandConstant('{app}'), ExpandConstant('{localappdata}\Harbor'));
  if (PreviousInstallDir <> '') and (CompareText(PreviousInstallDir, ExpandConstant('{app}')) <> 0) then
    StopInstallation(PreviousInstallDir, ExpandConstant('{localappdata}\Harbor'));
  StopInstallation(ExpandConstant('{localappdata}\Programs\Gopeed Native'), ExpandConstant('{localappdata}\GopeedNative'));
end;
