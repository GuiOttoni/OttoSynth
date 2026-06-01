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
#define StandaloneDir  "..\artifacts\standalone"
#define PluginDir      "..\src\OttoSynth.Plugin\bin\Release\net10.0-windows10.0.17763"
; Flat deployment directory under the VST3 root.
; AudioPlugSharp's GetPluginFactory strips exactly 6 chars ("Bridge") from its own
; filename to locate the managed DLL: "OttoSynthBridge.vst3" -> "OttoSynth" -> OttoSynth.dll.
; This naming only works when the bridge file keeps "Bridge" in its name.
; Flat deployment (all files in one subfolder, no Steinberg bundle wrapper) is the correct
; AudioPlugSharp deployment model — DAWs scan this folder and load OttoSynthBridge.vst3
; as a flat native VST3 DLL alongside its managed dependencies.
#define VST3Dir "{commoncf64}\VST3\" + AppName

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

; ── VST3 plugin — flat deployment in {commoncf64}\VST3\OttoSynth\ ──
; All files land in the same flat directory so the bridge can locate its
; siblings at runtime. OttoSynthBridge.vst3 is the C++/CLI entry point;
; it MUST keep "Bridge" in the name so GetPluginFactory can derive the
; managed assembly name by stripping those 6 chars: OttoSynth.dll.

; Native VST3 entry point (keep Bridge suffix — do NOT rename)
Source: "{#PluginDir}\OttoSynthBridge.vst3"; \
  DestDir: "{#VST3Dir}"; \
  Flags: ignoreversion; Components: vst3

; IJW CLR bootstrapper — must be alongside the native entry point
Source: "{#PluginDir}\Ijwhost.dll"; \
  DestDir: "{#VST3Dir}"; \
  Flags: ignoreversion; Components: vst3

; Bridge runtimeconfig — Ijwhost reads {bridge-basename}.runtimeconfig.json to boot the CLR.
; Custom file (not the NuGet-generated one) because we need LoadComponentInIsolatedContext=false.
; Without it AudioPlugSharp's ALC creates a second isolated context, causing a type-identity
; mismatch on IAudioPlugin and crashing the DAW scanner.
Source: "OttoSynth.bridge.runtimeconfig.json"; \
  DestDir: "{#VST3Dir}"; DestName: "OttoSynthBridge.runtimeconfig.json"; \
  Flags: ignoreversion; Components: vst3

; All managed DLLs (OttoSynth.dll, OttoSynth.Core.dll, OttoSynth.UI.dll, AudioPlugSharp*.dll, etc.)
Source: "{#PluginDir}\*.dll"; \
  DestDir: "{#VST3Dir}"; \
  Flags: ignoreversion; Components: vst3

; Managed assembly dependency map
Source: "{#PluginDir}\OttoSynth.deps.json"; \
  DestDir: "{#VST3Dir}"; \
  Flags: ignoreversion; Components: vst3

[Icons]
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExe}"; Components: standalone
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";    Filename: "{app}\{#AppExe}"; \
  Tasks: desktopicon; Components: standalone

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName} agora"; \
  Flags: nowait postinstall skipifsilent; Components: standalone

[InstallDelete]
; Remove old bundle-style deployment (installers before 1.0.3 used a Steinberg bundle
; structure which caused the bridge to derive the wrong managed assembly name).
Type: filesandordirs; Name: "{commoncf64}\VST3\{#AppName}.vst3"

[UninstallDelete]
Type: dirifempty; Name: "{commoncf64}\VST3\{#AppName}"
