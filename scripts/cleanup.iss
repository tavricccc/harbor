// Walk only owned installation trees. Never follow directory junctions.
procedure CleanInstallationTree(Directory, Relative: String; CurrentFiles: TStringList; KeepUninstaller: Boolean);
var
  Entry: TFindRec;
  Name, Path, Child: String;
begin
  if FindFirst(AddBackslash(Directory) + '*', Entry) then
  begin
    try
      repeat
        Name := Entry.Name;
        if (Name <> '.') and (Name <> '..') then
        begin
          Path := AddBackslash(Directory) + Name;
          Child := Relative + Name;
          if (Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          begin
            if (Entry.Attributes and $400) = 0 then
              CleanInstallationTree(Path, Child + '\', CurrentFiles, KeepUninstaller);
            RemoveDir(Path);
          end
          else if not (KeepUninstaller and (Relative = '') and (Pos('unins', LowerCase(Name)) = 1)) then
          begin
            if CurrentFiles.IndexOf(LowerCase(Child)) < 0 then
            begin
              Log('Removing obsolete installation file: ' + Path);
              if not DeleteFile(Path) then
                RaiseException(FmtMessage(CustomMessage('CleanupFailed'), [Path]));
            end;
          end;
        end;
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
end;

procedure CleanCurrentInstallation;
var CurrentFiles: TStringList;
begin
  if not IsManagedInstallation(ExpandConstant('{app}')) then Exit;
  ExtractTemporaryFile('installed-files.txt');
  CurrentFiles := TStringList.Create;
  try
    CurrentFiles.LoadFromFile(ExpandConstant('{tmp}\installed-files.txt'));
    CleanInstallationTree(ExpandConstant('{app}'), '', CurrentFiles, True);
  finally
    CurrentFiles.Free;
  end;
end;

procedure RemoveOldInstallation(Directory: String);
var EmptyFiles: TStringList;
begin
  if (CompareText(RemoveBackslashUnlessRoot(Directory), ExpandConstant('{app}')) = 0) or
    not IsManagedInstallation(Directory) then Exit;
  EmptyFiles := TStringList.Create;
  try
    CleanInstallationTree(Directory, '', EmptyFiles, False);
    RemoveDir(Directory);
  finally
    EmptyFiles.Free;
  end;
end;

procedure RemoveLegacyShortcuts;
begin
  DeleteFile(ExpandConstant('{userdesktop}\Gopeed Native.lnk'));
  DeleteFile(ExpandConstant('{userprograms}\Gopeed Native\Gopeed Native.lnk'));
  DeleteFile(ExpandConstant('{userprograms}\Gopeed Native\Uninstall Gopeed Native.lnk'));
  RemoveDir(ExpandConstant('{userprograms}\Gopeed Native'));
end;

procedure RemoveOwnedRegistrations;
var Index: Integer; Names, BrowserKeys: TArrayOfString; Manifest: String;
begin
  Names := ['Harbor.Torrent', 'Harbor.Magnet', 'Harbor.Ed2k',
    'GopeedNative.Torrent', 'GopeedNative.Magnet', 'GopeedNative.Ed2k'];
  for Index := 0 to GetArrayLength(Names) - 1 do
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\' + Names[Index]);
  BrowserKeys := ['Software\Google\Chrome\NativeMessagingHosts\com.gopeed.gopeed',
    'Software\Microsoft\Edge\NativeMessagingHosts\com.gopeed.gopeed',
    'Software\Mozilla\NativeMessagingHosts\com.gopeed.gopeed'];
  for Index := 0 to GetArrayLength(BrowserKeys) - 1 do
    if RegQueryStringValue(HKCU, BrowserKeys[Index], '', Manifest) then
      if (CompareText(ExtractFileDir(Manifest), ExpandConstant('{localappdata}\Harbor')) = 0) or
        (CompareText(ExtractFileDir(Manifest), ExpandConstant('{localappdata}\GopeedNative')) = 0) then
        RegDeleteKeyIncludingSubkeys(HKCU, BrowserKeys[Index]);
end;

procedure RemoveApplicationState(DataDirectory: String);
var
  Index: Integer;
  Files, Directories: TArrayOfString;
begin
  // Delete owned metadata, never a user's selected download directory.
  Files := ['api-token', 'session.json', 'session.json.tmp', 'preferences.json', 'preferences.json.tmp',
    'deferred-downloads.json', 'deferred-downloads.json.tmp', 'gopeed.db', '.torrent.bolt.db',
    'browser-host.json', 'browser-host-firefox.json', 'browser-integration-backup.json', 'frontend-error.log'];
  Directories := ['logs', 'pending-downloads', 'extensions', 'webview', 'legacy-migration'];
  for Index := 0 to GetArrayLength(Files) - 1 do
    if FileExists(AddBackslash(DataDirectory) + Files[Index]) and
      not DeleteFile(AddBackslash(DataDirectory) + Files[Index]) then
      RaiseException(FmtMessage(CustomMessage('CleanupFailed'), [AddBackslash(DataDirectory) + Files[Index]]));
  for Index := 0 to GetArrayLength(Directories) - 1 do
    if DirExists(AddBackslash(DataDirectory) + Directories[Index]) and
      not DelTree(AddBackslash(DataDirectory) + Directories[Index], True, True, True) then
      RaiseException(FmtMessage(CustomMessage('CleanupFailed'), [AddBackslash(DataDirectory) + Directories[Index]]));
  RemoveDir(DataDirectory);
end;
