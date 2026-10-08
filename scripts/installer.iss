#ifndef AppVersion
  #error Use scripts/package.ps1 to provide AppVersion
#endif
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
LicenseFile=..\artifacts\app\LICENSE-Gopeed.txt

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zhTW"; MessagesFile: "languages\ChineseTraditional.isl"
Name: "zhCN"; MessagesFile: "languages\ChineseSimplified.isl"

[CustomMessages]
en.DesktopIcon=Create a desktop shortcut
zhTW.DesktopIcon=建立桌面捷徑
zhCN.DesktopIcon=创建桌面快捷方式
en.LaunchHarbor=Open Harbor
zhTW.LaunchHarbor=開啟 Harbor
zhCN.LaunchHarbor=打开 Harbor
en.RegistrationMissing=Harbor browser integration was not registered: %1
zhTW.RegistrationMissing=Harbor 瀏覽器接管註冊未完成：%1
zhCN.RegistrationMissing=Harbor 浏览器接管注册未完成：%1
en.RegistrationOutdated=Browser integration still points to an old manifest: %1
zhTW.RegistrationOutdated=瀏覽器接管仍指向舊的設定檔：%1
zhCN.RegistrationOutdated=浏览器接管仍指向旧的配置文件：%1
en.ManifestMissing=The Harbor browser integration manifest is missing.
zhTW.ManifestMissing=Harbor 瀏覽器接管設定檔不存在。
zhCN.ManifestMissing=Harbor 浏览器接管配置文件不存在。
en.RegistrationLaunchFailed=Unable to start Harbor to register browser integration.
zhTW.RegistrationLaunchFailed=無法啟動 Harbor 完成瀏覽器接管註冊。
zhCN.RegistrationLaunchFailed=无法启动 Harbor 完成浏览器接管注册。
en.RegistrationFailed=Harbor browser integration registration failed. Code: %1
zhTW.RegistrationFailed=Harbor 瀏覽器接管註冊失敗，代碼：%1
zhCN.RegistrationFailed=Harbor 浏览器接管注册失败，代码：%1

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
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Run]
Filename: "{app}\Harbor.exe"; Description: "{cm:LaunchHarbor}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\Harbor.exe"; Parameters: "--unregister-integrations"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveNativeIntegration"
Filename: "{app}\Engine\harbor-core.exe"; Parameters: "--data ""{localappdata}\Harbor"" --shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "StopNativeCore"

[Code]
#include "migration.iss"
procedure VerifyBrowserHost(BrowserKey, ManifestName: String);
var Manifest: String;
begin
  if not RegQueryStringValue(HKCU, BrowserKey + '\com.gopeed.gopeed', '', Manifest) then
    RaiseException(FmtMessage(CustomMessage('RegistrationMissing'), [BrowserKey]));
  if CompareText(Manifest, ExpandConstant('{localappdata}\Harbor\' + ManifestName)) <> 0 then
    RaiseException(FmtMessage(CustomMessage('RegistrationOutdated'), [Manifest]));
  if not FileExists(Manifest) then
    RaiseException(CustomMessage('ManifestMissing'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not Exec(ExpandConstant('{app}\Harbor.exe'), '--register-integrations', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException(CustomMessage('RegistrationLaunchFailed'));
    if Code <> 0 then
      RaiseException(FmtMessage(CustomMessage('RegistrationFailed'), [IntToStr(Code)]));
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
