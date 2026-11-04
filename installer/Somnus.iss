; Somnus installer (ADR 0003): per-user, no administrator rights. There is one installer per
; language (ADR 0009): Lang=en or Lang=pl picks the wizard language, the licence and the README.
; Somnus runs on the shared .NET Desktop Runtime, which Setup downloads from Microsoft and installs
; when it is missing (ADR 0010). Built by scripts/build.py; it passes AppVersion (for example 1.0.0
; or 0.9.0-rc.1), FileVersion (numeric, for example 1.0.0), Lang and RuntimeVersion, RuntimeUrl and
; RuntimeSha256 of the runtime installer. Paths are relative to this file.

#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
#ifndef FileVersion
  #define FileVersion "0.0.0"
#endif
#ifndef Lang
  #define Lang "en"
#endif
#ifndef RuntimeVersion
  #define RuntimeVersion "10.0"
#endif
#ifndef RuntimeUrl
  #define RuntimeUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
#endif
#ifndef RuntimeSha256
  #define RuntimeSha256 ""
#endif
#if Lang == "en"
  #define LanguageName "english"
  #define LanguageMessages "compiler:Default.isl"
  #define SetupDescription "Somnus Setup"
  #define RuntimeInfoTitle "Microsoft .NET Desktop Runtime"
  #define RuntimeInfoDescription "Somnus needs one more component from Microsoft."
  #define RuntimeInfoMessage "Somnus runs on Microsoft .NET Desktop Runtime 10, a free component from Microsoft that many apps share. This PC does not have it yet, so Setup will now download it from Microsoft (about 60 MB) and install it once for the whole PC.%n%nWindows will ask you to allow this installation; on a standard account it asks for an administrator's password. Somnus itself needs no administrator rights.%n%nClick Install to download it and install Somnus."
  #define RuntimeTitle "Installing Microsoft .NET Desktop Runtime"
  #define RuntimeDescription "Somnus needs Microsoft .NET Desktop Runtime " + RuntimeVersion + ". Setup gets it from Microsoft (about 60 MB)."
  #define RuntimeInstalling "Installing it now. Windows may ask you to allow this once."
  #define RuntimeFailed "Setup could not install Microsoft .NET Desktop Runtime 10, which Somnus needs. Check your internet connection and allow the installation when Windows asks (on a standard account this needs an administrator's password), then try again. You can also install it yourself from https://dotnet.microsoft.com/download/dotnet/10.0."
#elif Lang == "pl"
  #define LanguageName "polish"
  #define LanguageMessages "compiler:Languages\Polish.isl"
  #define SetupDescription "Instalator programu Somnus"
  #define RuntimeInfoTitle "Microsoft .NET Desktop Runtime"
  #define RuntimeInfoDescription "Somnus potrzebuje jeszcze jednego składnika od Microsoftu."
  #define RuntimeInfoMessage "Somnus działa na środowisku Microsoft .NET Desktop Runtime 10, bezpłatnym składniku Microsoftu, z którego korzysta wiele aplikacji. Na tym komputerze go jeszcze nie ma, więc instalator pobierze je teraz od Microsoftu (ok. 60 MB) i zainstaluje raz dla całego komputera.%n%nWindows poprosi o zgodę na tę instalację; na koncie standardowym poprosi o hasło administratora. Sam Somnus nie potrzebuje uprawnień administratora.%n%nKliknij Instaluj, aby je pobrać i zainstalować program Somnus."
  #define RuntimeTitle "Instalowanie Microsoft .NET Desktop Runtime"
  #define RuntimeDescription "Somnus potrzebuje środowiska Microsoft .NET Desktop Runtime " + RuntimeVersion + ". Instalator pobiera je od Microsoftu (ok. 60 MB)."
  #define RuntimeInstalling "Trwa instalacja. Windows może raz poprosić o zgodę."
  #define RuntimeFailed "Instalator nie mógł zainstalować środowiska Microsoft .NET Desktop Runtime 10, którego potrzebuje Somnus. Sprawdź połączenie z internetem i zezwól na instalację, gdy Windows zapyta (na koncie standardowym potrzebne jest hasło administratora), a potem spróbuj ponownie. Możesz też zainstalować je samodzielnie ze strony https://dotnet.microsoft.com/download/dotnet/10.0."
#else
  #error Lang must be en or pl.
#endif
#define PublishDir "..\artifacts\publish"
; README.txt and LICENSE.txt in the installer's language, written by build.py.
#define PackageDir "..\artifacts\package-" + Lang
#define ImagesDir "..\artifacts\installer-images"

[Setup]
; The AppId identifies Somnus for upgrades and uninstall; never change it.
AppId={{CD435C27-5F9E-4CCC-8176-0FA8F61360E4}
AppName=Somnus
AppVersion={#AppVersion}
AppVerName=Somnus {#AppVersion}
AppPublisher=Maqjack
AppPublisherURL=https://github.com/Maqjack/somnus
AppSupportURL=https://github.com/Maqjack/somnus/issues
AppUpdatesURL=https://github.com/Maqjack/somnus/releases
AppCopyright=Copyright (c) 2026 Mac (Maqjack)
VersionInfoVersion={#FileVersion}
VersionInfoProductVersion={#FileVersion}
VersionInfoDescription={#SetupDescription}
; Per user, never elevated (R-08): installs to %LOCALAPPDATA%\Programs\Somnus.
PrivilegesRequired=lowest
DefaultDirName={autopf}\Somnus
DisableProgramGroupPage=yes
DisableDirPage=auto
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 22H2 or later (SCOPE section 6).
MinVersion=10.0.19045
LicenseFile={#PackageDir}\LICENSE.txt
SetupIconFile=..\assets\Somnus.ico
UninstallDisplayIcon={app}\Somnus.exe
UninstallDisplayName=Somnus
; Black on every page and dialog, white text, the purple crescent on a transparent background (#52).
WizardStyle=modern dark hidebevels includetitlebar
WizardBackColor=#000000
WizardImageBackColor=#000000
WizardSmallImageBackColor=#000000
WizardImageFile={#ImagesDir}\wizard-large.png,{#ImagesDir}\wizard-large-2x.png
WizardSmallImageFile={#ImagesDir}\wizard-small-58.png,{#ImagesDir}\wizard-small-77.png,{#ImagesDir}\wizard-small-97.png,{#ImagesDir}\wizard-small-116.png,{#ImagesDir}\wizard-small-124.png,{#ImagesDir}\wizard-small-143.png,{#ImagesDir}\wizard-small-159.png
; The page with the desktop shortcut ends with Install; a summary page adds nothing here.
DisableReadyPage=yes
; Somnus must not run while its files are replaced or removed (same mutex as R-09).
AppMutex=Somnus.Instance
CloseApplications=yes
RestartApplications=no
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=Somnus-Setup-{#AppVersion}-{#Lang}

; One language only, so Setup never asks for one.
[Languages]
Name: "{#LanguageName}"; MessagesFile: "{#LanguageMessages}"

[CustomMessages]
RuntimeInfoTitle={#RuntimeInfoTitle}
RuntimeInfoDescription={#RuntimeInfoDescription}
RuntimeInfoMessage={#RuntimeInfoMessage}
RuntimeTitle={#RuntimeTitle}
RuntimeDescription={#RuntimeDescription}
RuntimeInstalling={#RuntimeInstalling}
RuntimeFailed={#RuntimeFailed}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion
; The purple progress bar (see [Code]); used by Setup only, never installed.
Source: "{#ImagesDir}\progress-fill.bmp"; Flags: dontcopy
Source: "{#ImagesDir}\progress-track.bmp"; Flags: dontcopy

[INI]
; Somnus starts in the installer's language until the user picks one (#53).
Filename: "{app}\Somnus.ini"; Section: "Installer"; Key: "Language"; String: "{language}"

[Registry]
; Somnus starts with Windows only when the user turns it on in its settings (#83); Setup writes
; nothing here (ValueType: none), and uninstalling removes the value and Task Manager's flag for it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Somnus"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: none; ValueName: "Somnus"; Flags: dontcreatekey uninsdeletevalue

[Icons]
Name: "{autoprograms}\Somnus"; Filename: "{app}\Somnus.exe"
Name: "{autodesktop}\Somnus"; Filename: "{app}\Somnus.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Somnus.exe"; Description: "{cm:LaunchProgram,Somnus}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Assumption A6: uninstalling also removes the settings and logs in %APPDATA%\Somnus.
Type: filesandordirs; Name: "{userappdata}\Somnus"
Type: files; Name: "{app}\Somnus.ini"

[Code]
const
  RuntimeFile = 'windowsdesktop-runtime.exe';

// The dark style paints the standard progress bar in its own colours, and a script cannot change
// them. Two stretched one-pixel bitmaps (track and purple fill) take its place on the Installing
// page and on the page that downloads the runtime.
var
  ProgressTrack, ProgressFill: TBitmapImage;
  DownloadTrack, DownloadFill: TBitmapImage;
  DownloadPage: TDownloadWizardPage;
  RuntimeInfoPage: TOutputMsgWizardPage;

function CreateProgressPart(const FileName: String; Gauge: TNewProgressBar): TBitmapImage;
begin
  ExtractTemporaryFile(FileName);
  Result := TBitmapImage.Create(WizardForm);
  Result.Parent := Gauge.Parent;
  Result.Left := Gauge.Left;
  Result.Top := Gauge.Top;
  Result.Height := Gauge.Height;
  Result.Stretch := True;
  Result.Bitmap.LoadFromFile(ExpandConstant('{tmp}\') + FileName);
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  DownloadPage.ProgressBar.Visible := False;
  if ProgressMax > 0 then
    DownloadFill.Width := MulDiv(DownloadTrack.Width, Integer(Progress div 1024), Integer(ProgressMax div 1024));
  Result := True;
end;

procedure InitializeWizard;
begin
  ProgressTrack := CreateProgressPart('progress-track.bmp', WizardForm.ProgressGauge);
  ProgressTrack.Width := WizardForm.ProgressGauge.Width;
  ProgressFill := CreateProgressPart('progress-fill.bmp', WizardForm.ProgressGauge);
  ProgressFill.Width := 0;
  WizardForm.ProgressGauge.Visible := False;

  // Mac (2026-11-04): before anything is downloaded, a page says what Setup installs and why.
  RuntimeInfoPage := CreateOutputMsgPage(wpSelectTasks, CustomMessage('RuntimeInfoTitle'), CustomMessage('RuntimeInfoDescription'), CustomMessage('RuntimeInfoMessage'));
  DownloadPage := CreateDownloadPage(CustomMessage('RuntimeTitle'), CustomMessage('RuntimeDescription'), @OnDownloadProgress);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  DownloadTrack := CreateProgressPart('progress-track.bmp', DownloadPage.ProgressBar);
  DownloadTrack.Width := DownloadPage.ProgressBar.Width;
  DownloadFill := CreateProgressPart('progress-fill.bmp', DownloadPage.ProgressBar);
  DownloadFill.Width := 0;
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
begin
  // The standard bar is a window and would cover the bitmaps, so it stays hidden.
  WizardForm.ProgressGauge.Visible := False;
  if MaxProgress > 0 then
    ProgressFill.Width := MulDiv(ProgressTrack.Width, CurProgress, MaxProgress);
end;

// The same search as Somnus.exe does: the registered dotnet folder, then the default one. Any 10.x
// will do, because the app rolls forward to the newest 10 it finds.
function IsRuntimeInstalled: Boolean;
var
  DotNetFolder: String;
  Found: TFindRec;
begin
  if not RegQueryStringValue(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64', 'InstallLocation', DotNetFolder) then
    DotNetFolder := ExpandConstant('{commonpf64}\dotnet');
  Result := FindFirst(AddBackslash(DotNetFolder) + 'shared\Microsoft.WindowsDesktop.App\10.*', Found);
  if Result then
    FindClose(Found);
end;

// Setup stays per-user: only Microsoft's own installer asks Windows for elevation, once.
function InstallRuntime: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  DownloadPage.Clear;
  DownloadPage.Add('{#RuntimeUrl}', RuntimeFile, '{#RuntimeSha256}');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      DownloadPage.AbortButton.Hide;
      DownloadPage.SetText(CustomMessage('RuntimeInstalling'), '');
      if not ShellExec('', ExpandConstant('{tmp}\') + RuntimeFile, '/install /passive /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then
        Log('The runtime installer did not start: ' + SysErrorMessage(ResultCode));
      Result := IsRuntimeInstalled;
    except
      Log('The runtime could not be installed: ' + GetExceptionMessage);
    end;
  finally
    DownloadPage.Hide;
  end;
end;

function NeedsRuntime: Boolean;
begin
#ifdef ForceRuntimeInstall
  // Only in scripts/test_installer.py: the CI runner already has the runtime.
  Result := True;
#else
  Result := not IsRuntimeInstalled;
#endif
end;

// The page about the runtime shows only on a PC that lacks it.
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = RuntimeInfoPage.ID) and not NeedsRuntime;
end;

// Mac (#52): with the Ready page off, the last page before the installation ends with Install, as
// the Ready page would: the page about the runtime, or the tasks page when the runtime is there.
procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = RuntimeInfoPage.ID) or ((CurPageID = wpSelectTasks) and not NeedsRuntime) then
    WizardForm.NextButton.Caption := SetupMessage(msgButtonInstall);
end;

// The page about the runtime is the last one before the installation starts, because the Ready
// page is off; its Install downloads and installs the runtime.
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = RuntimeInfoPage.ID) and NeedsRuntime then
  begin
    Result := InstallRuntime;
    if not Result then
      SuppressibleMsgBox(CustomMessage('RuntimeFailed'), mbCriticalError, MB_OK, IDOK);
  end;
end;

// Builds up to 1.1.0-rc.4 carried the runtime in the app folder. Its hostfxr.dll would make the new
// Somnus.exe look for the runtime there, so the old files go before the new ones arrive.
procedure RemoveBundledRuntime;
begin
  DelTree(ExpandConstant('{app}\*.dll'), False, True, False);
  DelTree(ExpandConstant('{app}\*.pdb'), False, True, False);
  DelTree(ExpandConstant('{app}\*.xml'), False, True, False);
  DeleteFile(ExpandConstant('{app}\createdump.exe'));
  DelTree(ExpandConstant('{app}\pl'), True, True, True);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssInstall) and FileExists(ExpandConstant('{app}\hostfxr.dll')) then
    RemoveBundledRuntime;
end;
