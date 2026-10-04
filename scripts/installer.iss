#define AppVersion "0.6.0"
[Setup]
AppId={{B652BEF3-0741-4B5E-9066-C6F3EBF18622}
AppName=Harbor
AppVersion={#AppVersion}
AppPublisher=Tavric
DefaultDirName={localappdata}\Programs\Gopeed Native
DefaultGroupName=Harbor
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=Harbor-Setup-{#AppVersion}-x64
SetupIconFile=..\src\Gopeed.Native\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Gopeed.Native.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile=..\upstream\LICENSE

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Harbor"; Filename: "{app}\Gopeed.Native.exe"
Name: "{autodesktop}\Harbor"; Filename: "{app}\Gopeed.Native.exe"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{userprograms}\Gopeed Native\Gopeed Native.lnk"
Type: files; Name: "{autodesktop}\Gopeed Native.lnk"

[Registry]
Root: HKCU; Subkey: "Software\Classes\gopeed"; ValueType: string; ValueName: ""; ValueData: "URL:Harbor Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\gopeed"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\gopeed\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Gopeed.Native.exe,0"
Root: HKCU; Subkey: "Software\Classes\gopeed\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Gopeed.Native.exe"" ""%1"""

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; Flags: unchecked

[Run]
Filename: "{app}\Gopeed.Native.exe"; Parameters: "--register-integrations"; Flags: runhidden waituntilterminated
Filename: "{app}\Gopeed.Native.exe"; Description: "開啟 Harbor"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\Gopeed.Native.exe"; Parameters: "--unregister-integrations"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveNativeIntegration"
Filename: "{app}\Engine\gopeed-core.exe"; Parameters: "--data ""{localappdata}\GopeedNative"" --shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "StopNativeCore"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer; Command, Description, Icon: String;
begin
  if not RegKeyExists(HKCU, 'Software\GopeedNative\ProtocolBackup') and
    RegQueryStringValue(HKCU, 'Software\Classes\gopeed\shell\open\command', '', Command) and
    (Pos('Gopeed.Native.exe', Command) = 0) then
  begin
    RegWriteStringValue(HKCU, 'Software\GopeedNative\ProtocolBackup', 'Command', Command);
    if RegQueryStringValue(HKCU, 'Software\Classes\gopeed', '', Description) then
      RegWriteStringValue(HKCU, 'Software\GopeedNative\ProtocolBackup', 'Description', Description);
    if RegQueryStringValue(HKCU, 'Software\Classes\gopeed\DefaultIcon', '', Icon) then
      RegWriteStringValue(HKCU, 'Software\GopeedNative\ProtocolBackup', 'Icon', Icon);
  end;
  if FileExists(ExpandConstant('{app}\Engine\gopeed-core.exe')) then
    Exec(ExpandConstant('{app}\Engine\gopeed-core.exe'), '--data "' + ExpandConstant('{localappdata}\GopeedNative') + '" --shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command, Description, Icon: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\GopeedNative\ProtocolBackup', 'Command', Command) then
    begin
      RegWriteStringValue(HKCU, 'Software\Classes\gopeed', 'URL Protocol', '');
      RegWriteStringValue(HKCU, 'Software\Classes\gopeed\shell\open\command', '', Command);
      if RegQueryStringValue(HKCU, 'Software\GopeedNative\ProtocolBackup', 'Description', Description) then
        RegWriteStringValue(HKCU, 'Software\Classes\gopeed', '', Description);
      if RegQueryStringValue(HKCU, 'Software\GopeedNative\ProtocolBackup', 'Icon', Icon) then
        RegWriteStringValue(HKCU, 'Software\Classes\gopeed\DefaultIcon', '', Icon);
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\GopeedNative\ProtocolBackup');
    end;
  end;
end;
