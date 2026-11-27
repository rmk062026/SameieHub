; Inno Setup script for SameieHub (internal test version). Built by scripts\Build-Installer.ps1, which gives the values below:
;   /DVersion=0.8.4-test.1   the version shown to the user and used in the file name
;   /DNumericVersion=0.8.4.0 the same as four numbers, for the file properties
;   /DSourceDir=...          the published program folder (scripts\Publish-Windows.ps1)
;   /DOutputDir=...          where SameieHub-<version>-win-x64-Setup.exe is written
;
; The installer only ever touches the program folder, the Start menu and desktop shortcuts, and its own entry in "Installed apps".
; It never reads, writes, migrates or deletes anything in %LOCALAPPDATA%\SameieHub (the database, logs, backups and restore files).

#ifndef Version
  #define Version "0.0.0-dev"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0.0"
#endif
#ifndef SourceDir
  #error SourceDir must be given, for example /DSourceDir=..\artifacts\windows\SameieHub-0.8.4-test-win-x64-test
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
; The same AppId in every version makes a new installer an update of the old one. Never change it.
AppId={{8D3F6B2A-5C1E-4B7A-9E64-2F0A7C1D5E93}
AppName=SameieHub
AppVersion={#Version}
AppVerName=SameieHub {#Version}
AppPublisher=SameieHub (intern testversjon)
VersionInfoVersion={#NumericVersion}
VersionInfoDescription=SameieHub installasjon
; One user, no administrator rights: the program lives under the user's own profile.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\SameieHub
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=SameieHub-{#Version}-win-x64-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\SameieHub.exe
UninstallDisplayName=SameieHub
; The program does not start while Setup works (it checks this name), and two Setups do not run at once.
SetupMutex=SameieHubSetup
; The running program is asked to stop in the orderly way by the code below. Setup never ends the process by force.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "norwegian"; MessagesFile: "compiler:Languages\Norwegian.isl"

[CustomMessages]
norwegian.DesktopIcon=Lag en snarvei på skrivebordet
norwegian.StartProgram=Start SameieHub
norwegian.RunningAsk=SameieHub kjører. Installasjonen kan ikke bytte programfilene mens det kjører.%n%nSkal installasjonen be SameieHub avslutte på en trygg måte nå? Pågående arbeid avsluttes ordentlig, og dataene dine røres ikke.
norwegian.RunningStillRunning=SameieHub kjører fortsatt. Lukk vinduet «SameieHub» (eller trykk Ctrl+C i det), vent til det er borte, og kjør installasjonen på nytt. Ingenting er endret.
norwegian.UninstallRunningAsk=SameieHub kjører. Avinstallasjonen kan ikke fjerne programfilene mens det kjører.%n%nSkal avinstallasjonen be SameieHub avslutte på en trygg måte nå?
norwegian.UninstallStillRunning=SameieHub kjører fortsatt. Lukk vinduet «SameieHub», vent til det er borte, og kjør avinstallasjonen på nytt. Ingenting er fjernet.
norwegian.UninstallDataKept=SameieHub er fjernet.%n%nDataene dine (databasen, loggfilene og sikkerhetskopiene) er beholdt i mappen:%n%n%1%n%nSlett den mappen selv hvis du også vil fjerne dataene.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
; The published program. LES-MEG and the start file belong to the zip package, not to the installed program.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "LES-MEG.txt,Start-SameieHub.cmd,*.db,*.db-wal,*.db-shm,*.log"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; An update replaces the program files, so files an older version had and the new one does not are removed. Only the
; program's own file types and folder are named, so the uninstaller and everything outside this folder are left alone.
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"
Type: files; Name: "{app}\SameieHub.exe"
Type: files; Name: "{app}\createdump.exe"
Type: files; Name: "{app}\web.config"
Type: filesandordirs; Name: "{app}\wwwroot"

[Icons]
; The shortcut starts the program with no arguments. The program chooses the protected Windows mode itself.
Name: "{autoprograms}\SameieHub"; Filename: "{app}\SameieHub.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\SameieHub"; Filename: "{app}\SameieHub.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SameieHub.exe"; Description: "{cm:StartProgram}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
const
  RunningMutex = 'SameieHub.Running';
  WaitForStopMilliseconds = 20000;

function ProgramIsRunning: Boolean;
begin
  Result := CheckForMutexes(RunningMutex);
end;

// Asks the running program to shut down the way Ctrl+C does (SameieHub.exe --stop, over the pipe that only this Windows user can
// use), and waits. The process is never ended by force: a database operation that is under way is allowed to finish.
function StopProgramGently: Boolean;
var
  Exe: String;
  ResultCode, Waited: Integer;
begin
  Exe := ExpandConstant('{app}\SameieHub.exe');
  if FileExists(Exe) then
    Exec(Exe, '--stop', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Waited := 0;
  while ProgramIsRunning and (Waited < WaitForStopMilliseconds) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;
  Result := not ProgramIsRunning;
end;

// Runs after the folder is chosen and before any file is replaced. A message returned here stops the installation with nothing changed.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if ProgramIsRunning then
  begin
    if WizardSilent or (MsgBox(CustomMessage('RunningAsk'), mbConfirmation, MB_YESNO) = IDYES) then
      StopProgramGently;

    if ProgramIsRunning then
      Result := CustomMessage('RunningStillRunning');
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  if ProgramIsRunning then
  begin
    if UninstallSilent or (MsgBox(CustomMessage('UninstallRunningAsk'), mbConfirmation, MB_YESNO) = IDYES) then
      StopProgramGently;

    if ProgramIsRunning then
    begin
      if not UninstallSilent then
        MsgBox(CustomMessage('UninstallStillRunning'), mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usDone) and not UninstallSilent then
    MsgBox(FmtMessage(CustomMessage('UninstallDataKept'), [ExpandConstant('{localappdata}\SameieHub')]), mbInformation, MB_OK);
end;
