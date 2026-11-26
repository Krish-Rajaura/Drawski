; Built by build-installer.ps1, which publishes a self-contained build to ..\publish first

#define AppName "Drawski"
#define AppExe "Drawski.exe"
#define PublishDir "..\publish"
#define AppVersion GetVersionNumbersString(PublishDir + "\" + AppExe)

[Setup]
; Never change AppId, upgrades and uninstall rely on it
AppId={{B73FD649-AA60-4B21-ADA2-42AC142C6107}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Krish-Rajaura
AppPublisherURL=https://github.com/Krish-Rajaura/Drawski
AppSupportURL=https://github.com/Krish-Rajaura/Drawski/issues
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=Output
OutputBaseFilename=DrawskiSetup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\Drawski.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
AppMutex=Drawski.SingleInstance
CloseApplications=yes
ChangesAssociations=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "pdfopenwith"; Description: "Add Drawski to ""Open with"" for PDF files"; GroupDescription: "File types:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Shows up under "Open with" for pdfs, doesn't take over the default pdf app
Root: HKA; Subkey: "Software\Classes\Drawski.pdf"; ValueType: string; ValueData: "PDF Document (Drawski)"; Flags: uninsdeletekey; Tasks: pdfopenwith
Root: HKA; Subkey: "Software\Classes\Drawski.pdf\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExe}"",0"; Tasks: pdfopenwith
Root: HKA; Subkey: "Software\Classes\Drawski.pdf\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: pdfopenwith
Root: HKA; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "Drawski.pdf"; ValueData: ""; Flags: uninsdeletevalue; Tasks: pdfopenwith
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey; Tasks: pdfopenwith
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""; Tasks: pdfopenwith

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
