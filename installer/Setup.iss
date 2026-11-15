; Inno Setup script for the app's setup program. build-release.ps1 compiles it and passes:
;   AppName     the display name, <Product> in DownloadsInTray.csproj
;   AppFileName the file name, <AssemblyName> in DownloadsInTray.csproj
;   AppVersion  the <Version> from DownloadsInTray.csproj
;   SourceExe   the standard exe, which needs the .NET 10 Desktop Runtime; setup checks for it
;   OutputDir   where the setup goes

#ifndef AppName
  #error Build this through build-release.ps1, which passes AppName, AppFileName, AppVersion, SourceExe and OutputDir.
#endif

#define AppExe AppFileName + ".exe"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
; The app was called WinDownload before; its install is replaced.
#define OldName "WinDownload"

[Setup]
; AppId identifies the installation for upgrades and uninstalling. Keep it when renaming the app.
AppId={{6B0E2C4A-3F1D-4E8B-9C57-2D8A1F4B7E63}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Dorian Vasco
DefaultDirName={localappdata}\Programs\{#AppFileName}
; An upgrade from WinDownload must not reuse its folder.
UsePreviousAppDir=no
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
; Installs for the current user only, so no admin prompt.
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename={#AppFileName}-{#AppVersion}-setup
SetupIconFile=..\src\DownloadsInTray\Assets\DownloadsInTray.ico
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

[InstallDelete]
; What the WinDownload install left behind. Its settings and start-at-login entry are carried over
; by the app itself on first start.
Type: filesandordirs; Name: "{localappdata}\Programs\{#OldName}"
Type: files; Name: "{userprograms}\{#OldName}.lnk"

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Recent downloads popup"

[Registry]
; The same value the app's own "Start at login" setting writes, so either can turn it off.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppFileName}"; \
    ValueData: """{app}\{#AppExe}"" --background"; Tasks: startatlogin; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: postinstall nowait skipifsilent; Check: HasDesktopRuntime

[UninstallDelete]
; Settings written by the app.
Type: filesandordirs; Name: "{localappdata}\{#AppFileName}"

[Code]
const
  RuntimeDownload = 'https://dotnet.microsoft.com/download/dotnet/10.0';

// The app is the small build without a bundled .NET runtime, so check for the one it needs.
function HasDesktopRuntime(): Boolean;
var
  Found: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Found);
  if Result then FindClose(Found);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if HasDesktopRuntime() or WizardSilent() then Exit;
  if MsgBox('{#AppName} needs the .NET 10 Desktop Runtime from Microsoft, which isn''t installed yet.' + #13#10#13#10 +
            'Open the download page now? Setup continues either way; start {#AppName} once the runtime is installed.',
            mbConfirmation, MB_YESNO) = IDYES then
    ShellExec('open', RuntimeDownload, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

// The app keeps running in the background, so stop it before files are replaced or removed.
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM "{#AppExe}" /IM "{#OldName}.exe"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
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
  // The app's own Start at login setting may have written these without the installer's task.
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, '{#RunKey}', '{#AppFileName}');
    RegDeleteValue(HKEY_CURRENT_USER, '{#RunKey}', '{#OldName}');
  end;
end;
