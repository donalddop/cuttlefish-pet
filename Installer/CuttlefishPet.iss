; Installer for Cuttlefish Pet.
;
; Installs per-user into LocalAppData so Windows never raises a UAC prompt —
; one of the two friction points for a small unsigned app. (The other is
; SmartScreen, which only code signing removes.)
;
; Build:  ISCC.exe Installer\CuttlefishPet.iss
; Expects the self-contained publish output in publish\CuttlefishPet\.

#define AppName "Cuttlefish Pet"
#define AppVersion "2.7"
#define AppPublisher "donalddop"
#define AppUrl "https://github.com/donalddop/cuttlefish-pet"
#define AppExe "CuttlefishPet.exe"

[Setup]
AppId={{8E2C7A61-4F3B-4E9D-9C2A-5D7E1B0A6F34}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
VersionInfoVersion={#AppVersion}
; NOT {localappdata}\CuttlefishPet -- that is where the tank, the graveyard
; and the settings live. Installing on top of them would mean the uninstaller
; deleted the whole population, which is the one thing in here that cannot be
; rebuilt: days of inherited learning and bonds.
DefaultDirName={localappdata}\Programs\CuttlefishPet
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
; Per-user: no admin rights, no UAC prompt.
PrivilegesRequired=lowest
OutputDir=..\publish
OutputBaseFilename=CuttlefishPet-setup
SetupIconFile=..\CuttlefishPet\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
dutch.StartWithWindows=Automatisch starten met Windows
dutch.LaunchNow=Zeekatten nu loslaten
english.StartWithWindows=Start automatically with Windows
english.LaunchNow=Release the cuttlefish now

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\CuttlefishPet\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchNow}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Ask the running instance to close itself before the files are removed.
Filename: "{app}\{#AppExe}"; Parameters: "exit"; Flags: runhidden skipifdoesntexist; RunOnceId: "StopPet"

[UninstallDelete]
; Only the program. The aquarium in {localappdata}\CuttlefishPet is left alone,
; so reinstalling later finds the same animals where you left them.
Type: filesandordirs; Name: "{app}"

[Code]
// Leftover autostart entry from the tray menu's own toggle.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'CuttlefishPet');
end;

