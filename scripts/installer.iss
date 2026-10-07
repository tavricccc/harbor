#define AppVersion "0.7.2"
[Setup]
AppId={{B652BEF3-0741-4B5E-9066-C6F3EBF18622}
AppName=Harbor
AppVersion={#AppVersion}
AppPublisher=Tavric
DefaultDirName={localappdata}\Programs\Harbor
UsePreviousAppDir=no
DefaultGroupName=Harbor
UsePreviousGroup=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=Harbor-Setup-{#AppVersion}-x64
SetupIconFile=..\src\Harbor\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Harbor.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile=..\upstream\LICENSE

[Files]
Source: "..\artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Harbor"; Filename: "{app}\Harbor.exe"
Name: "{autodesktop}\Harbor"; Filename: "{app}\Harbor.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\gopeed"; ValueType: string; ValueName: ""; ValueData: "URL:Harbor Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\gopeed"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\gopeed\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Harbor.exe,0"
Root: HKCU; Subkey: "Software\Classes\gopeed\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Harbor.exe"" ""%1"""

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; Flags: unchecked

[Run]
Filename: "{app}\Harbor.exe"; Description: "開啟 Harbor"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\Harbor.exe"; Parameters: "--unregister-integrations"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveNativeIntegration"
Filename: "{app}\Engine\harbor-core.exe"; Parameters: "--data ""{localappdata}\Harbor"" --shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "StopNativeCore"

[Code]
#include "migration.iss"
procedure VerifyBrowserHost(BrowserKey, ManifestName: String);
var Manifest: String;
begin
  if not RegQueryStringValue(HKCU, BrowserKey + '\com.gopeed.gopeed', '', Manifest) then
    RaiseException('Harbor 瀏覽器接管註冊未完成：' + BrowserKey);
  if CompareText(Manifest, ExpandConstant('{localappdata}\Harbor\' + ManifestName)) <> 0 then
    RaiseException('瀏覽器接管仍指向舊的設定檔：' + Manifest);
  if not FileExists(Manifest) then
    RaiseException('Harbor 瀏覽器接管設定檔不存在。');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not Exec(ExpandConstant('{app}\Harbor.exe'), '--register-integrations', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException('無法啟動 Harbor 完成瀏覽器接管註冊。');
    if Code <> 0 then
      RaiseException('Harbor 瀏覽器接管註冊失敗，代碼：' + IntToStr(Code));
    VerifyBrowserHost('Software\Google\Chrome\NativeMessagingHosts', 'browser-host.json');
    VerifyBrowserHost('Software\Microsoft\Edge\NativeMessagingHosts', 'browser-host.json');
    VerifyBrowserHost('Software\Mozilla\NativeMessagingHosts', 'browser-host-firefox.json');
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer; Command, Description, Icon: String;
begin
  StopLegacyCore;
  if not RegKeyExists(HKCU, 'Software\Harbor\ProtocolBackup') and
    RegQueryStringValue(HKCU, 'Software\Classes\gopeed\shell\open\command', '', Command) and
    (Pos('Harbor.exe', Command) = 0) then
  begin
    RegWriteStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Command', Command);
    if RegQueryStringValue(HKCU, 'Software\Classes\gopeed', '', Description) then
      RegWriteStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Description', Description);
    if RegQueryStringValue(HKCU, 'Software\Classes\gopeed\DefaultIcon', '', Icon) then
      RegWriteStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Icon', Icon);
  end;
  if FileExists(ExpandConstant('{app}\Engine\harbor-core.exe')) then
    Exec(ExpandConstant('{app}\Engine\harbor-core.exe'), '--data "' + ExpandConstant('{localappdata}\Harbor') + '" --shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := '';
end;


procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command, Description, Icon: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Command', Command) then
    begin
      RegWriteStringValue(HKCU, 'Software\Classes\gopeed', 'URL Protocol', '');
      RegWriteStringValue(HKCU, 'Software\Classes\gopeed\shell\open\command', '', Command);
      if RegQueryStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Description', Description) then
        RegWriteStringValue(HKCU, 'Software\Classes\gopeed', '', Description);
      if RegQueryStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Icon', Icon) then
        RegWriteStringValue(HKCU, 'Software\Classes\gopeed\DefaultIcon', '', Icon);
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Harbor\ProtocolBackup');
    end;
  end;
end;
