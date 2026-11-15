#define AppName "GameBar CPU Profile"
#define AppVersion "0.1.0"
#define ServiceExe "GameBarCpuProfile.Service.exe"

[Setup]
AppId={{BF19CCAC-10B4-4F6D-875F-739D967EBBD0}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=GameBar CPU Profile Contributors
DefaultDirName={autopf}\GameBar CPU Profile
DefaultGroupName={#AppName}
OutputDir=Output
OutputBaseFilename=GameBarCpuProfile-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#ServiceExe}
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartIfNeededByRun=no

[Files]
Source: "..\publish\{#ServiceExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.ko.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{app}\{#ServiceExe}"; Parameters: "--install-service"; StatusMsg: "Installing and starting Windows service..."; Flags: runhidden waituntilterminated

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
  ServiceProgram: String;
begin
  Result := '';
  ServiceProgram := ExpandConstant('{app}\{#ServiceExe}');
  if FileExists(ServiceProgram) then
  begin
    if not Exec(ServiceProgram, '--stop-service', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'Could not stop the previous service. Installation was cancelled.'
    else if ExitCode <> 0 then
      Result := 'Previous GameBar CPU Profile service did not stop (exit ' + IntToStr(ExitCode) + ').';
  end;
end;

function InitializeUninstall(): Boolean;
var
  ExitCode: Integer;
  ServiceProgram: String;
begin
  Result := True;
  ServiceProgram := ExpandConstant('{app}\{#ServiceExe}');
  if FileExists(ServiceProgram) then
  begin
    if not Exec(ServiceProgram, '--uninstall-service', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    begin
      MsgBox('Unable to stop and remove the service. Uninstall cancelled to avoid orphaned compatibility databases.', mbError, MB_OK);
      Result := False;
    end
    else if ExitCode <> 0 then
    begin
      MsgBox('Service/database removal failed (exit ' + IntToStr(ExitCode) + '). Check C:\ProgramData\GameBarCpuProfile\service.log. Uninstall cancelled.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;
