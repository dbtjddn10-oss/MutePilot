#define MyAppName "MutePilot"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "유성우"
#define MyAppExeName "MutePilot.exe"

[Setup]
AppId={{C4E88C7B-67F5-4D17-BB04-73F8B8F56DA2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MutePilot
DefaultGroupName=MutePilot
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=MutePilot-Setup-v1.0.0
SetupIconFile=..\src\MutePilot\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no
SetupLogging=yes

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로가기 만들기"; GroupDescription: "추가 바로가기:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MutePilot"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\MutePilot"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "MutePilot 실행"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(
      ExpandConstant('{sys}\schtasks.exe'),
      '/Delete /TN "MutePilot Startup" /F',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);
  end;
end;
