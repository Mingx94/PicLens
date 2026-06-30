#ifndef AppVersion
#error AppVersion must be passed by the Tasks.cs installer task.
#endif

#ifndef RootDir
#error RootDir must be passed by the Tasks.cs installer task.
#endif

#ifndef PayloadDir
#error PayloadDir must be passed by the Tasks.cs installer task.
#endif

#ifndef OutputDir
#error OutputDir must be passed by the Tasks.cs installer task.
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename "PicLens-Setup"
#endif

#define AppName "PicLens"

[Setup]
AppId={{4B3899A4-2E9E-4B4F-9CF5-36F8D8D6767D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=PicLens
DefaultDirName={localappdata}\Programs\PicLens
DefaultGroupName=PicLens
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#RootDir}\PicLens\Assets\AppIcon.ico
UninstallDisplayIcon={app}\PicLens.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\PicLens"; Filename: "{app}\PicLens.exe"
Name: "{autodesktop}\PicLens"; Filename: "{app}\PicLens.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PicLens.exe"; Description: "{cm:LaunchProgram,PicLens}"; Flags: nowait postinstall skipifsilent
