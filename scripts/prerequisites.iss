function CheckDotNetRuntime: String;
var Versions: TArrayOfString; Index, ErrorCode: Integer; VersionPrefix, Message: String;
begin
  Result := '';
  VersionPrefix := '{#RequiredDotNet}.';
  { The .NET installer registers all architectures in the 32-bit registry view. }
  if RegGetValueNames(HKLM32,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App', Versions) then
    for Index := 0 to GetArrayLength(Versions) - 1 do
      if Pos(VersionPrefix, Versions[Index]) = 1 then Exit;

  Result := FmtMessage(CustomMessage('DotNetMissing'), ['{#RequiredDotNet}']);
  Message := Result + #13#10#13#10 + CustomMessage('DotNetInstallPrompt');
  if not WizardSilent and
    (SuppressibleMsgBox(Message, mbConfirmation, MB_YESNO, IDNO) = IDYES) then
    ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/{#RequiredDotNet}/runtime?os=windows&arch=x64',
      '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;
