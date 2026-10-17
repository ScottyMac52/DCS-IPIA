#ifndef AppVersion
  #define AppVersion "0.0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define AppName "DCS Input Profile Importer"
#define AppPublisher "ScottyMac52"
#define AppExeName "Dcs.Ipia.App.exe"

[Setup]
AppId={{A5F785B7-C3D8-44D5-9E40-5ED48113F9E4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\DCS-IPIA
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=..\src\Dcs.Ipia.App\Branding\flight-patch.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardImageFile=..\src\Dcs.Ipia.App\Branding\setup-sidebar.bmp
WizardSmallImageFile=..\src\Dcs.Ipia.App\Branding\setup-small.bmp

[Files]
Source: "Configure-SavedGames.ps1"; Flags: dontcopy
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  SavedGamesPage: TInputDirWizardPage;

function RunFolderSettings(const Arguments: String): Boolean;
var
  ExitCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ' +
    AddQuotes(ExpandConstant('{tmp}\Configure-SavedGames.ps1')) + ' ' + Arguments,
    '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if Result then Result := ExitCode = 0;
end;

procedure InitializeWizard;
var
  Lines: TArrayOfString;
begin
  ExtractTemporaryFile('Configure-SavedGames.ps1');
  SavedGamesPage := CreateInputDirPage(wpSelectDir, 'Saved Games DCS Folder',
    'Where should Fly Now! install your DCS controls and kneeboards?',
    'Select the DCS instance folder (for example Saved Games\DCS).' + #13#10 +
    'Keep the current selection or click Browse to select another instance.', False, '');
  SavedGamesPage.Add('Saved Games DCS Folder:');
  if not RunFolderSettings('-Mode Read -DefaultsPath ' +
    AddQuotes(AddBackslash(WizardDirValue) + 'appsettings.json') + ' -OutputPath ' +
    AddQuotes(ExpandConstant('{tmp}\saved-games-folder.txt'))) then
    RaiseException('Unable to read the Saved Games DCS Folder setting.');
  if not LoadStringsFromFile(ExpandConstant('{tmp}\saved-games-folder.txt'), Lines) then
    RaiseException('Unable to load the Saved Games DCS Folder selection.');
  if GetArrayLength(Lines) <> 1 then
    RaiseException('Invalid Saved Games DCS Folder selection.');
  SavedGamesPage.Values[0] := ExpandConstant('{param:SAVEDGAMES|}');
  if SavedGamesPage.Values[0] = '' then SavedGamesPage.Values[0] := Lines[0];
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = SavedGamesPage.ID) and (Trim(SavedGamesPage.Values[0]) = '') then
  begin
    MsgBox('Choose a Saved Games DCS Folder.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not RunFolderSettings('-Mode Write -Folder ' + AddQuotes(SavedGamesPage.Values[0])) then
      RaiseException('Unable to save Saved Games DCS Folder to per-user appsettings.json. ' +
        'Check the settings file and permissions, then rerun setup.');
end;
