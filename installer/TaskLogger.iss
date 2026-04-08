; ─────────────────────────────────────────────────────────────────────────────
; TaskLogger — Inno Setup Script
; This file defines the Windows installer wizard that gets built by GitHub Actions
; ─────────────────────────────────────────────────────────────────────────────

[Setup]
; App identity shown in Programs & Features (Add/Remove Programs)
AppName=TaskLogger
AppVersion=1.0.0
AppPublisher=Innovation Team Nigeria
AppPublisherURL=https://github.com/innovationlab-ng/task-logger

; Install to %LOCALAPPDATA%\TaskLogger — no admin rights needed!
; {localappdata} = C:\Users\YourName\AppData\Local
DefaultDirName={localappdata}\TaskLogger

; Don't ask the user to pick a Start Menu group name
DefaultGroupName=TaskLogger
DisableProgramGroupPage=yes

; Output file name (without .exe — Inno adds it automatically)
OutputBaseFilename=TaskLoggerSetup

; Where to write the finished installer (relative to this .iss file)
OutputDir=Output

; Compression settings — LZMA2 solid gives the smallest file
Compression=lzma2
SolidCompression=yes

; IMPORTANT: "lowest" means no UAC admin prompt — installs per-user only
PrivilegesRequired=lowest

; Modern Fluent-style wizard UI (requires Inno Setup 6)
WizardStyle=modern

; Icon shown in Add/Remove Programs
UninstallDisplayIcon={app}\TaskLoggerApp.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ─────────────────────────────────────────────────────────────────────────────
; FILES — what gets copied to the install directory
; ─────────────────────────────────────────────────────────────────────────────
[Files]
; The single self-contained .exe produced by `dotnet publish`
; Path is relative to this .iss file (installer/)
Source: "..\publish\win\TaskLoggerApp.exe"; DestDir: "{app}"; Flags: ignoreversion

; ─────────────────────────────────────────────────────────────────────────────
; SHORTCUTS — what the installer creates
; ─────────────────────────────────────────────────────────────────────────────
[Icons]
; Start Menu shortcut
Name: "{group}\TaskLogger"; Filename: "{app}\TaskLoggerApp.exe"

; Desktop shortcut (optional — only created if user ticks the checkbox)
Name: "{commondesktop}\TaskLogger"; Filename: "{app}\TaskLoggerApp.exe"; Tasks: desktopicon

; ─────────────────────────────────────────────────────────────────────────────
; TASKS — optional checkboxes shown during install
; ─────────────────────────────────────────────────────────────────────────────
[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

; ─────────────────────────────────────────────────────────────────────────────
; POST-INSTALL — optionally launch the app after install finishes
; ─────────────────────────────────────────────────────────────────────────────
[Run]
Filename: "{app}\TaskLoggerApp.exe"; \
  Description: "Launch TaskLogger now"; \
  Flags: nowait postinstall skipifsilent

; ─────────────────────────────────────────────────────────────────────────────
; UNINSTALL — clean up log files when the user uninstalls
; ─────────────────────────────────────────────────────────────────────────────
[UninstallDelete]
; Remove the logs folder that TaskLogger creates at runtime
Type: filesandordirs; Name: "{localappdata}\TaskLogger\logs"
