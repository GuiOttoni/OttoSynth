; ── OttoSynth Inno Setup Installer ──────────────────────────────
; Usage (local):  ISCC.exe OttoSynth.iss
; Usage (CI):     ISCC.exe OttoSynth.iss "/DAppVersion=1.0.0-beta.1"
;
; Prerequisites (run from repo root):
;   dotnet publish src/OttoSynth.Standalone/OttoSynth.Standalone.csproj ^
;     -c Release -r win-x64 --self-contained -p:PublishSingleFile=true ^
;     -p:PublishReadyToRun=true --output artifacts/standalone
;   dotnet build src/OttoSynth.Plugin/OttoSynth.Plugin.csproj -c Release

#ifndef AppVersion
  #define AppVersion "1.0.0-beta.1"
#endif

#define AppName       "OttoSynth"
#define AppPublisher  "OttoSound"
#define AppURL        "https://ottosound.io"
#define AppExe        "OttoSynth.exe"
#define StandaloneDir "..\artifacts\standalone"
#define PluginDir     "..\src\OttoSynth.Plugin\bin\Release\net10.0-windows"

[Setup]
AppId={{A3F1C2E4-9B7D-4E8A-B6F2-1D5C3A9E7F42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL=https://github.com/GuiOttoni/OttoSynth/issues
AppUpdatesURL=https://github.com/GuiOttoni/OttoSynth/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename={#AppName}-{#AppVersion}-Setup
; VersionInfoVersion must be numeric (x.y.z.w); keep separate from the display version
VersionInfoVersion=1.0.0.0
VersionInfoDescription={#AppName} Setup
VersionInfoCompany={#AppPublisher}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExe}
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Components]
Name: "standalone"; Description: "OttoSynth Standalone (.exe)"; Types: full compact custom; Flags: fixed
Name: "vst3";       Description: "OttoSynth Plugin (VST3)";      Types: full

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
; ── Standalone ──────────────────────────────────────────────────
Source: "{#StandaloneDir}\{#AppExe}"; DestDir: "{app}"; \
  Flags: ignoreversion; Components: standalone

; ── VST3 plugin ─────────────────────────────────────────────────
; All VST3 entries use skipifsourcedoesntexist so ISCC never fails
; if a file is missing (e.g. native bridge name differs between AudioPlugSharp versions).
Source: "{#PluginDir}\AudioPlugSharpVst.vst3"; \
  DestDir: "{commoncf64}\VST3\{#AppName}"; \
  Flags: ignoreversion skipifsourcedoesntexist; Components: vst3

Source: "{#PluginDir}\OttoSynth.Plugin.dll"; \
  DestDir: "{commoncf64}\VST3\{#AppName}"; \
  Flags: ignoreversion skipifsourcedoesntexist; Components: vst3

Source: "{#PluginDir}\OttoSynth.Plugin.runtimeconfig.json"; \
  DestDir: "{commoncf64}\VST3\{#AppName}"; \
  Flags: ignoreversion skipifsourcedoesntexist; Components: vst3

Source: "{#PluginDir}\OttoSynth.Core.dll"; \
  DestDir: "{commoncf64}\VST3\{#AppName}"; \
  Flags: ignoreversion skipifsourcedoesntexist; Components: vst3

Source: "{#PluginDir}\OttoSynth.UI.dll"; \
  DestDir: "{commoncf64}\VST3\{#AppName}"; \
  Flags: ignoreversion skipifsourcedoesntexist; Components: vst3

[Icons]
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExe}"; Components: standalone
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";    Filename: "{app}\{#AppExe}"; \
  Tasks: desktopicon; Components: standalone

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName} agora"; \
  Flags: nowait postinstall skipifsilent; Components: standalone

[UninstallDelete]
Type: dirifempty; Name: "{commoncf64}\VST3\{#AppName}"
