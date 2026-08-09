#ifndef BrandName
  #error BrandName must be passed in with /DBrandName=...
#endif
#ifndef AppId
  #error AppId must be passed in with /DAppId=... (bare GUID, no braces)
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef DistDir
  #define DistDir "..\dist"
#endif
#ifndef OutputDir
  #define OutputDir "..\build\setup"
#endif

#define ShellDll   BrandName + "Shell.dll"
#define WorkerExe  BrandName + ".exe"

#define RuntimeMajor "10"
#define RuntimeUrl "https://dotnet.microsoft.com/download/dotnet/10.0"

[Setup]
AppId={{{#AppId}}
AppName={#BrandName}
AppVersion={#AppVersion}
AppVerName={#BrandName} {#AppVersion}
DefaultDirName={localappdata}\{#BrandName}
UninstallDisplayName={#BrandName}
UninstallDisplayIcon={app}\{#WorkerExe}
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no
OutputDir={#OutputDir}
OutputBaseFilename={#BrandName}-{#AppVersion}-setup
#ifdef IconFile
SetupIconFile={#IconFile}
#endif
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Messages]
FinishedLabelNoIcons=Setup is done.%n%nRight-click a file or folder and choose "Show more options" (Shift+F10) to find {#BrandName}.

[Files]
Source: "{#DistDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
const
  DirectoryAttribute = $10;
  CRLF = #13#10;

function ShellDllPath(): String;
begin
  Result := ExpandConstant('{app}\{#ShellDll}');
end;

function HasDesktopRuntime(): Boolean;
var
  Root: String;
  Rec: TFindRec;
  Wanted: String;
begin
  Result := False;
  Root := ExpandConstant('{%ProgramW6432|C:\Program Files}') +
          '\dotnet\shared\Microsoft.WindowsDesktop.App';
  if not DirExists(Root) then
    exit;

  Wanted := '{#RuntimeMajor}' + '.';
  if FindFirst(Root + '\*', Rec) then
  begin
    try
      repeat
        if (Rec.Attributes and DirectoryAttribute) <> 0 then
          if Copy(Rec.Name, 1, Length(Wanted)) = Wanted then
          begin
            Result := True;
            exit;
          end;
      until not FindNext(Rec);
    finally
      FindClose(Rec);
    end;
  end;
end;

procedure RestartExplorer();
var
  ResultCode: Integer;
  AutoRestart: Cardinal;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM explorer.exe', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1500);

  if RegQueryDWordValue(HKEY_LOCAL_MACHINE,
       'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon',
       'AutoRestartShell', AutoRestart) and (AutoRestart = 0) then
  begin
    Exec(ExpandConstant('{win}\explorer.exe'), '', '', SW_SHOWNORMAL,
         ewNoWait, ResultCode);
    Sleep(1000);
  end;
end;

function RunRegsvr32(const Arguments: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\regsvr32.exe'), Arguments, '',
                 SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure UnregisterHandler();
begin
  if FileExists(ShellDllPath()) then
    RunRegsvr32('/u /s "' + ShellDllPath() + '"');
end;

procedure StopWorker();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM "{#WorkerExe}"', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function ReleaseShellDll(): Boolean;
begin
  Result := True;
  if not FileExists(ShellDllPath()) then
    exit;

  UnregisterHandler();
  if DeleteFile(ShellDllPath()) then
    exit;

  RestartExplorer();
  Result := DeleteFile(ShellDllPath());
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if HasDesktopRuntime() then
    exit;

  if MsgBox('{#BrandName} needs the .NET {#RuntimeMajor} Desktop Runtime (x64),' +
            ' which is not installed.' + CRLF + CRLF +
            'Open the download page now?' + CRLF +
            'Choose No to install anyway and add the runtime later.',
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    ShellExecAsOriginalUser('open', '{#RuntimeUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;

procedure ClearPreviousInstall();
var
  Root: String;
  Rec: TFindRec;
  Doomed: TStringList;
  I: Integer;
begin
  Root := ExpandConstant('{app}');
  Doomed := TStringList.Create;
  try
    if FindFirst(Root + '\*', Rec) then
    begin
      try
        repeat
          if (Rec.Name = '.') or (Rec.Name = '..') then
            Continue;
          if Pos('unins', Lowercase(Rec.Name)) = 1 then
            Continue;
          Doomed.Add(Root + '\' + Rec.Name);
        until not FindNext(Rec);
      finally
        FindClose(Rec);
      end;
    end;

    for I := 0 to Doomed.Count - 1 do
      if DirExists(Doomed[I]) then
        DelTree(Doomed[I], True, True, True)
      else
        DeleteFile(Doomed[I]);
  finally
    Doomed.Free;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Existing: Boolean;
begin
  Result := '';
  StopWorker();

  Existing := FileExists(ShellDllPath());
  if not ReleaseShellDll() then
  begin
    Result := 'The Explorer menu handler is still in use and could not be replaced.' +
              CRLF + 'Sign out and back in, then run Setup again.';
    exit;
  end;

  if Existing then
    ClearPreviousInstall();
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not RunRegsvr32('/s "' + ShellDllPath() + '"') then
      MsgBox('The files were installed, but registering the Explorer menu failed.' +
             CRLF + 'Running Setup again usually fixes it.', mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopWorker();
    ReleaseShellDll();
  end;
end;
