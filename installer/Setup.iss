; Inno Setup script for the app's setup program. build-release.ps1 compiles it and passes:
;   AppName    the <Product> name from WinDownload.csproj
;   AppVersion the <Version> from WinDownload.csproj
;   SourceExe  the self-contained exe, so the installed app needs no .NET runtime
;   OutputDir  where Setup.exe goes

#ifndef AppName
  #error Build this through build-release.ps1, which passes AppName, AppVersion, SourceExe and OutputDir.
#endif

#define AppExe AppName + ".exe"

[Setup]
; AppId identifies the installation for upgrades and uninstalling. Keep it when renaming the app.
AppId={{6B0E2C4A-3F1D-4E8B-9C57-2D8A1F4B7E63}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Dorian Vasco
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
; Installs for the current user only, so no admin prompt.
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename={#AppName}-{#AppVersion}-setup
SetupIconFile=..\src\WinDownload\Assets\WinDownload.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000

[Tasks]
Name: startatlogin; Description: "Start {#AppName} when Windows starts"; GroupDescription: "Options:"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Recent downloads popup"

[Registry]
; The same value the app's own "Start at login" setting writes, so either can turn it off.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
    ValueData: """{app}\{#AppExe}"" --background"; Tasks: startatlogin; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Settings written by the app.
Type: filesandordirs; Name: "{localappdata}\{#AppName}"

[Code]
// The app keeps running in the background, so stop it before files are replaced or removed.
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM "{#AppExe}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // The app's own Start at login setting may have written this value without the installer's task.
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
end;
