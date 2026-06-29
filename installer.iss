; Tape Track Splitter — Inno Setup script
; Build with: iscc installer.iss  (after dotnet publish -o publish)

#define AppName    "Tape Track Splitter"
#define AppVersion "1.0"
#define AppExe     "TapeSplitterWpf.exe"
#define Publisher  "Kurt"

[Setup]
AppName                 = {#AppName}
AppVersion              = {#AppVersion}
AppPublisher            = {#Publisher}
AppPublisherURL         = https://github.com/your-username/tape-track-splitter
AppSupportURL           = https://github.com/your-username/tape-track-splitter/issues
DefaultDirName          = {autopf}\{#AppName}
DefaultGroupName        = {#AppName}
OutputDir               = installer-out
OutputBaseFilename      = TapeSplitter-Setup-{#AppVersion}
Compression             = lzma2
SolidCompression        = yes
PrivilegesRequired      = admin
WizardStyle             = modern
DisableProgramGroupPage = yes
UninstallDisplayName    = {#AppName}
UninstallDisplayIcon    = {app}\{#AppExe}
ArchitecturesAllowed            = x64compatible
ArchitecturesInstallIn64BitMode = x64compatible
MinVersion              = 10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Copy the entire publish folder into the install directory
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}";   Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";     Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#AppExe}"; \
  Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove user settings on uninstall if desired (comment out to keep them)
; Type: filesandordirs; Name: "{localappdata}\TapeSplitter"
