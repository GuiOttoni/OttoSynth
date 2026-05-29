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
#define PluginDir      "..\src\OttoSynth.Plugin\bin\Release\net10.0-windows"
; VST3 bundle path — use ISPP string concat so AppName is expanded at preprocess time,
; NOT as a runtime constant (which would cause "Unknown constant #AppName" at compile).
#define VST3BundleDir "{commoncf64}\VST3\" + AppName + ".vst3\Contents\x86_64-win"

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

; ── VST3 plugin — bundle structure required by Steinberg spec ────
; The AudioPlugSharpVst3 NuGet package provides AudioPlugSharpVst.vst3, a native
; C++/CLI mixed-mode DLL (576 KB) that exports GetPluginFactory and bootstraps
; the .NET CLR via Ijwhost.dll. MSBuild renames it to $(TargetName)Bridge.vst3.
; The installer must rename it again to OttoSynth.vst3 (matching the bundle name)
; and rename the accompanying runtimeconfig/deps to match the new DLL base name,
; so the IJW CLR host can locate the config files at load time.

; Native VST3 entry point
Source: "{#PluginDir}\OttoSynth.PluginBridge.vst3"; \
  DestDir: "{#VST3BundleDir}"; DestName: "OttoSynth.vst3"; \
  Flags: ignoreversion; Components: vst3

; IJW CLR bootstrapper — must be alongside the native entry point
Source: "{#PluginDir}\Ijwhost.dll"; \
  DestDir: "{#VST3BundleDir}"; \
  Flags: ignoreversion; Components: vst3

; Bridge runtime config — renamed to match the installed entry point DLL base name
Source: "{#PluginDir}\OttoSynth.PluginBridge.runtimeconfig.json"; \
  DestDir: "{#VST3BundleDir}"; DestName: "OttoSynth.runtimeconfig.json"; \
  Flags: ignoreversion; Components: vst3

; Bridge deps — renamed to match the installed entry point DLL base name
Source: "{#PluginDir}\OttoSynth.PluginBridge.deps.json"; \
  DestDir: "{#VST3BundleDir}"; DestName: "OttoSynth.deps.json"; \
  Flags: ignoreversion; Components: vst3

; All managed DLLs
Source: "{#PluginDir}\*.dll"; \
  DestDir: "{#VST3BundleDir}"; \
  Flags: ignoreversion; Components: vst3

; Plugin runtime config and deps (bridge files excluded — already handled above with DestName)
Source: "{#PluginDir}\*.json"; \
  Excludes: "OttoSynth.PluginBridge.*"; \
  DestDir: "{#VST3BundleDir}"; \
  Flags: ignoreversion; Components: vst3

[Icons]
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExe}"; Components: standalone
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";    Filename: "{app}\{#AppExe}"; \
  Tasks: desktopicon; Components: standalone

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName} agora"; \
  Flags: nowait postinstall skipifsilent; Components: standalone

[UninstallDelete]
Type: dirifempty; Name: "{commoncf64}\VST3\{#AppName}.vst3\Contents\x86_64-win"
Type: dirifempty; Name: "{commoncf64}\VST3\{#AppName}.vst3\Contents"
Type: dirifempty; Name: "{commoncf64}\VST3\{#AppName}.vst3"
