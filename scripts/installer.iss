#ifndef AppVersion
  #error Use scripts/package.ps1 to provide AppVersion
#endif
[Setup]
AppId={{B652BEF3-0741-4B5E-9066-C6F3EBF18622}
AppName=Harbor
AppVersion={#AppVersion}
AppPublisher=Tavric
DefaultDirName={localappdata}\Programs\Harbor
UsePreviousAppDir=yes
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

#include "..\artifacts\installer-localization.iss"

[Files]
Source: "..\artifacts\app\installed-files.txt"; Flags: dontcopy
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

[Code]
#include "migration.iss"
#include "cleanup.iss"
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
  if CurStep = ssInstall then CleanCurrentInstallation;
  if CurStep = ssPostInstall then
  begin
    if not Exec(ExpandConstant('{app}\Harbor.exe'), '--register-integrations', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException(CustomMessage('RegistrationLaunchFailed'));
    if Code <> 0 then
      RaiseException(FmtMessage(CustomMessage('RegistrationFailed'), [IntToStr(Code)]));
    VerifyBrowserHost('Software\Google\Chrome\NativeMessagingHosts', 'browser-host.json');
    VerifyBrowserHost('Software\Microsoft\Edge\NativeMessagingHosts', 'browser-host.json');
    VerifyBrowserHost('Software\Mozilla\NativeMessagingHosts', 'browser-host-firefox.json');
    RemoveOldInstallation(PreviousInstallDir);
    RemoveOldInstallation(ExpandConstant('{localappdata}\Programs\Gopeed Native'));
    RemoveLegacyShortcuts;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Command, Description, Icon: String;
begin
  StopInstalledVersions;
  if not RegKeyExists(HKCU, 'Software\Harbor\ProtocolBackup') and
    RegQueryStringValue(HKCU, 'Software\Classes\gopeed\shell\open\command', '', Command) and
    not IsHarborCommand(Command) then
  begin
    RegWriteStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Command', Command);
    if RegQueryStringValue(HKCU, 'Software\Classes\gopeed', '', Description) then
      RegWriteStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Description', Description);
    if RegQueryStringValue(HKCU, 'Software\Classes\gopeed\DefaultIcon', '', Icon) then
      RegWriteStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Icon', Icon);
  end;
  Result := '';
end;


procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command, Description, Icon: String; EmptyFiles: TStringList;
begin
  if CurUninstallStep = usUninstall then StopInstalledVersions;
  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Command', Command) and
      not IsHarborCommand(Command) then
    begin
      RegWriteStringValue(HKCU, 'Software\Classes\gopeed', 'URL Protocol', '');
      RegWriteStringValue(HKCU, 'Software\Classes\gopeed\shell\open\command', '', Command);
      if RegQueryStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Description', Description) then
        RegWriteStringValue(HKCU, 'Software\Classes\gopeed', '', Description);
      if RegQueryStringValue(HKCU, 'Software\Harbor\ProtocolBackup', 'Icon', Icon) then
        RegWriteStringValue(HKCU, 'Software\Classes\gopeed\DefaultIcon', '', Icon);
    end;
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Harbor');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\GopeedNative');
    RegDeleteValue(HKCU, 'Software\RegisteredApplications', 'Harbor');
    RegDeleteValue(HKCU, 'Software\RegisteredApplications', 'GopeedNative');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Harbor');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GopeedNative');
    RemoveOwnedRegistrations;
    RemoveLegacyShortcuts;
    RemoveOldInstallation(ExpandConstant('{localappdata}\Programs\Gopeed Native'));
    RemoveApplicationState(ExpandConstant('{localappdata}\Harbor'));
    RemoveApplicationState(ExpandConstant('{localappdata}\GopeedNative'));
    EmptyFiles := TStringList.Create;
    try
      CleanInstallationTree(ExpandConstant('{app}'), '', EmptyFiles, True);
      RemoveDir(ExpandConstant('{app}'));
    finally
      EmptyFiles.Free;
    end;
  end;
end;
