[Setup]
AppName=Video Transcoder Utility
AppVersion=1.0.0
AppPublisher=Anze Umek
DefaultDirName={autopf}\VideoTranscoder
DefaultGroupName=Video Transcoder
OutputDir=Installer
OutputBaseFilename=VideoTranscoderSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\VideoTranscoder.GUI.exe
SetupIconFile=favicon.ico
WizardStyle=modern

[Files]
; GUI Application - adjust the path to match your .NET version
Source: "VideoTranscoder.GUI\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Service Application - adjust the path to match your .NET version
Source: "VideoTranscoder.Service\bin\Release\net10.0\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Video Transcoder"; Filename: "{app}\VideoTranscoder.GUI.exe"
Name: "{group}\Uninstall Video Transcoder"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Video Transcoder"; Filename: "{app}\VideoTranscoder.GUI.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
; Install and start the service
Filename: "sc.exe"; Parameters: "create VideoTranscoderService binPath=""{app}\Service\VideoTranscoder.Service.exe"" start=auto DisplayName=""Video Transcoder Service"""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "description VideoTranscoderService ""Automatically transcodes video files on schedule"""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start VideoTranscoderService"; Flags: runhidden waituntilterminated

; Optionally launch the GUI after install
Filename: "{app}\VideoTranscoder.GUI.exe"; Description: "Launch Video Transcoder Settings"; Flags: postinstall nowait skipifsilent

[UninstallRun]
; Stop and remove the service
Filename: "sc.exe"; Parameters: "stop VideoTranscoderService"; Flags: runhidden waituntilterminated; RunOnceId: "Cleanup1"
Filename: "sc.exe"; Parameters: "delete VideoTranscoderService"; Flags: runhidden waituntilterminated; RunOnceId: "Cleanup2"

[Code]
function ServiceExists(ServiceName: String): Boolean;
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'query "' + ServiceName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  if ServiceExists('VideoTranscoderService') then
  begin
    if MsgBox('Video Transcoder Service is already installed. Do you want to stop and reinstall it?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec('sc.exe', 'stop VideoTranscoderService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(2000);
      Exec('sc.exe', 'delete VideoTranscoderService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(2000);
    end
    else
    begin
      Result := False;
    end;
  end;
end;