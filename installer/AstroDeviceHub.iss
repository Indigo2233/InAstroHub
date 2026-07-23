#define ProductName "Astro Device Hub"
#define ProductVersion "0.3.0"
#define ProductPublisher "Indigo"
#define AscomServer "ASCOM.AstroDeviceHub.LocalServer.exe"

[Setup]
AppId={{F5A6028C-3420-4E29-818A-1D5553AF1B8C}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppPublisher={#ProductPublisher}
DefaultDirName={autopf}\Astro Device Hub
DefaultGroupName=Astro Device Hub
UninstallDisplayName=Astro Device Hub
OutputDir=..\dist\installer
OutputBaseFilename=AstroDeviceHub-Setup-{#ProductVersion}
SetupIconFile=..\desktop\Assets\AstroDeviceHub.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\ascom\*"; DestDir: "{app}\ascom"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Astro Device Hub"; Filename: "{app}\AstroDeviceHub.App.exe"
Name: "{autodesktop}\Astro Device Hub"; Filename: "{app}\AstroDeviceHub.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional options:"; Flags: unchecked

[Run]
Filename: "{app}\AstroDeviceHub.App.exe"; Description: "Launch Astro Device Hub"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{app}\ascom\{#AscomServer}"; Parameters: "/unregserver /silent"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterAstroDeviceHubAscom"

[Code]
function IsAscomPlatformInstalled(): Boolean;
begin
  Result := FileExists(ExpandConstant('{commonpf32}\ASCOM\Platform\v6\ASCOM.Utilities.dll'))
    or RegKeyExists(HKLM32, 'SOFTWARE\ASCOM\Platform')
    or RegKeyExists(HKLM64, 'SOFTWARE\ASCOM\Platform');
end;

function InitializeSetup(): Boolean;
begin
  Result := IsAscomPlatformInstalled();
  if not Result then
    MsgBox('ASCOM Platform 6 or 7 is required before installing Astro Device Hub.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not Exec(ExpandConstant('{app}\ascom\{#AscomServer}'), '/regserver /silent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Unable to start the ASCOM driver registration program.');
    if ResultCode <> 0 then
      RaiseException('ASCOM driver registration failed. Exit code: ' + IntToStr(ResultCode));
  end;
end;
