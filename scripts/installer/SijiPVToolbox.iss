; FourJTooLs AutoCAD bundle installer. Build through scripts/package-installer.ps1.

#ifndef PipelineInvocation
  #error This installer must be compiled by scripts/package-installer.ps1.
#endif
#if PipelineInvocation != "FOURJTOOLS_PACKAGE_PIPELINE_V1"
  #error Invalid installer packaging pipeline marker.
#endif

#ifndef AppVersion
  #define AppVersion "1.0.8"
#endif
#ifndef AssemblyVersion
  #define AssemblyVersion "1.0.8.0"
#endif
#ifndef InstallerAppId
  #define InstallerAppId "0DB1FAD5-EC98-434F-8AF6-E16B3462A678"
#endif
#ifndef ProductCode
  #define ProductCode "{9C42F595-68E0-4945-B8D7-62BE70975D5F}"
#endif
#ifndef UpgradeCode
  #define UpgradeCode "{7D31C7E3-7805-4611-9B3E-5946775A86EC}"
#endif
#ifndef LegacyProductCodes
  #define LegacyProductCodes "{5C2AE40B-D8F5-4025-B75E-13C0AEEB1251}"
#endif
#ifndef Publisher
  #define Publisher "FourJTooLs"
#endif
#ifndef AppDisplayName
  #define AppDisplayName "四季光伏工具箱"
#endif
#ifndef PayloadBundleDir
  #error PayloadBundleDir must point to a validated .bundle staging directory.
#endif
#ifndef VerifiedTargetLabel
  #error VerifiedTargetLabel must identify the verified AutoCAD target.
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "四季光伏工具箱_Setup_v1.0.8"
#endif
#ifndef VerifiedSeries
  #define VerifiedSeries "R23.1"
#endif
#ifndef CandidateLegacySeries
  #define CandidateLegacySeries "R22.0;R23.0;R24.0;R24.1;R24.2;R24.3"
#endif
#ifndef CandidateNet8Series
  #define CandidateNet8Series "R25.0;R25.1"
#endif
#ifndef TestBuild
  #define TestBuild "0"
#endif
#ifndef UnsignedDistributionBuild
  #define UnsignedDistributionBuild "0"
#endif
#ifndef BundleDirectoryName
  #define BundleDirectoryName "ZhStructureToolbox.bundle"
#endif

#if (TestBuild != "0") && (TestBuild != "1")
  #error TestBuild must be 0 or 1.
#endif
#if (UnsignedDistributionBuild != "0") && (UnsignedDistributionBuild != "1")
  #error UnsignedDistributionBuild must be 0 or 1.
#endif
#if (TestBuild == "1") && (UnsignedDistributionBuild == "1")
  #error TestBuild and UnsignedDistributionBuild cannot both be 1.
#endif
#if (TestBuild == "0") && (UnsignedDistributionBuild == "0")
  #ifndef SignToolName
    #error Signed builds must define SignToolName; use the controlled UNSIGNED pipeline when no certificate is available.
  #endif
#endif

[Setup]
AppId={{{#InstallerAppId}}
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppVerName={#AppDisplayName} {#AppVersion}
AppPublisher={#Publisher}
AppCopyright=Copyright 2026 FourJTooLs
VersionInfoVersion={#AssemblyVersion}
VersionInfoCompany={#Publisher}
VersionInfoDescription={#AppDisplayName}安装程序
VersionInfoProductName={#AppDisplayName}
VersionInfoProductVersion={#AssemblyVersion}
DefaultDirName={code:GetProgramRoot}
DefaultGroupName={#AppDisplayName}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableWelcomePage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
UsePreviousAppDir=no
UsePreviousPrivileges=yes
Uninstallable=yes
UninstallDisplayName={#AppDisplayName} {#AppVersion}
UninstallFilesDir={app}\uninstall
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=no
RestartApplications=no
ChangesEnvironment=no
MinVersion=6.1sp1
#ifdef SignToolName
SignTool={#SignToolName}
SignedUninstaller=yes
#else
SignedUninstaller=no
#endif

[Languages]
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"

[Files]
Source: "{#PayloadBundleDir}\*"; DestDir: "{code:GetBundlePath}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
; {app} contains only installer-owned uninstall support files.
Type: filesandordirs; Name: "{app}"

[Code]
const
  CurrentProductCode = '{#ProductCode}';
  CurrentUpgradeCode = '{#UpgradeCode}';
  AcceptedLegacyProductCodes = '{#LegacyProductCodes}';
  VerifiedSeriesList = '{#VerifiedSeries}';
  CandidateLegacySeriesList = '{#CandidateLegacySeries}';
  CandidateNet8SeriesList = '{#CandidateNet8Series}';
  VerifiedTargetLabelText = '{#VerifiedTargetLabel}';
  CurrentVersion = '{#AssemblyVersion}';
  BundleFolderName = '{#BundleDirectoryName}';
  IsTestInstaller = '{#TestBuild}';
  InstallerAppIdText = '{#InstallerAppId}';

var
  CadPage: TOutputMsgWizardPage;
  CadDetectionText: String;
  HasVerifiedCad: Boolean;
  ProcessInspectionError: String;
  TargetBundlePath: String;
  TargetBackupPath: String;
  TargetWasBackedUp: Boolean;
  SecondaryBundlePath: String;
  SecondaryBackupPath: String;
  SecondaryWasBackedUp: Boolean;
  InstallSucceeded: Boolean;
  OwnedBundleAtUninstall: Boolean;
  UninstallBundlePath: String;

function IsTestBuild: Boolean;
begin
  Result := IsTestInstaller = '1';
end;

function GetProgramRoot(Param: String): String;
begin
  if IsTestBuild then
    Result := ExpandConstant('{localappdata}\FourJTools\InstallerTest\Program')
  else if IsAdminInstallMode then
    Result := ExpandConstant('{autopf}\FourJTools\SijiPVToolbox')
  else
    Result := ExpandConstant('{localappdata}\Programs\FourJTools\SijiPVToolbox');
end;

function GetUserBundlePath: String;
begin
  Result := AddBackslash(ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins')) +
    BundleFolderName;
end;

function GetSystemBundlePath: String;
begin
  Result := AddBackslash(ExpandConstant('{autopf}\Autodesk\ApplicationPlugins')) +
    BundleFolderName;
end;

function GetBundlePath(Param: String): String;
begin
  if IsTestBuild then
    Result := ExpandConstant('{localappdata}\FourJTools\InstallerTest\ApplicationPlugins\') +
      BundleFolderName
  else if IsAdminInstallMode then
    Result := GetSystemBundlePath
  else
    Result := GetUserBundlePath;
end;

function NormalizeGuid(Value: String): String;
begin
  Result := Uppercase(Trim(Value));
end;

function ListContains(const Values, Wanted: String): Boolean;
var
  Remaining: String;
  Item: String;
  Separator: Integer;
begin
  Result := False;
  Remaining := Values;
  while Remaining <> '' do
  begin
    Separator := Pos(';', Remaining);
    if Separator = 0 then
    begin
      Item := Remaining;
      Remaining := '';
    end
    else
    begin
      Item := Copy(Remaining, 1, Separator - 1);
      Delete(Remaining, 1, Separator);
    end;
    if CompareText(Trim(Item), Trim(Wanted)) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function IsAcceptedProductCode(const ProductCode: String): Boolean;
begin
  Result := (NormalizeGuid(ProductCode) = NormalizeGuid(CurrentProductCode)) or
    ListContains(AcceptedLegacyProductCodes, ProductCode);
end;

function TryReadBundleIdentity(const BundlePath: String;
  var ProductCode, UpgradeCode, AppVersion: String): Boolean;
var
  XmlDocument: Variant;
  RootNode: Variant;
begin
  Log('Reading bundle identity: ' + BundlePath);
  Result := False;
  ProductCode := '';
  UpgradeCode := '';
  AppVersion := '';
  if not FileExists(AddBackslash(BundlePath) + 'PackageContents.xml') then
    Exit;

  try
    Log('Creating MSXML DOMDocument.');
    XmlDocument := CreateOleObject('Msxml2.DOMDocument.6.0');
    XmlDocument.async := False;
    XmlDocument.resolveExternals := False;
    Log('Loading PackageContents.xml.');
    if not XmlDocument.load(AddBackslash(BundlePath) + 'PackageContents.xml') then
      Exit;
    Log('Reading ApplicationPackage attributes.');
    RootNode := XmlDocument.documentElement;
    if VarIsClear(RootNode) then
      Exit;
    ProductCode := String(RootNode.getAttribute('ProductCode'));
    UpgradeCode := String(RootNode.getAttribute('UpgradeCode'));
    AppVersion := String(RootNode.getAttribute('AppVersion'));
    Log('Bundle identity read: version ' + AppVersion + '.');
    Result := (ProductCode <> '') and (UpgradeCode <> '') and (AppVersion <> '');
  except
    Log('Bundle identity read failed: ' + GetExceptionMessage);
    Result := False;
  end;
end;

function IsOwnedBundle(const BundlePath: String; var AppVersion: String): Boolean;
var
  ProductCode: String;
  UpgradeCode: String;
begin
  Result := TryReadBundleIdentity(BundlePath, ProductCode, UpgradeCode, AppVersion) and
    IsAcceptedProductCode(ProductCode) and
    (NormalizeGuid(UpgradeCode) = NormalizeGuid(CurrentUpgradeCode));
end;

function GetVersionPart(const VersionText: String; WantedIndex: Integer): Integer;
var
  I: Integer;
  CurrentIndex: Integer;
  CurrentPart: String;
  Character: String;
begin
  CurrentIndex := 0;
  CurrentPart := '';
  for I := 1 to Length(VersionText) + 1 do
  begin
    if I <= Length(VersionText) then
      Character := VersionText[I]
    else
      Character := '.';

    if Character = '.' then
    begin
      if CurrentIndex = WantedIndex then
      begin
        Result := StrToIntDef(CurrentPart, 0);
        Exit;
      end;
      CurrentIndex := CurrentIndex + 1;
      CurrentPart := '';
    end
    else if (Character >= '0') and (Character <= '9') then
      CurrentPart := CurrentPart + Character
    else
      Break;
  end;
  Result := 0;
end;

function CompareVersionText(const LeftVersion, RightVersion: String): Integer;
var
  I: Integer;
  LeftPart: Integer;
  RightPart: Integer;
begin
  Result := 0;
  for I := 0 to 3 do
  begin
    LeftPart := GetVersionPart(LeftVersion, I);
    RightPart := GetVersionPart(RightVersion, I);
    if LeftPart < RightPart then
    begin
      Result := -1;
      Exit;
    end;
    if LeftPart > RightPart then
    begin
      Result := 1;
      Exit;
    end;
  end;
end;

function VersionMatchesSeries(const VersionText, Series: String): Boolean;
var
  NormalizedVersion: String;
  NormalizedSeries: String;
begin
  NormalizedVersion := Uppercase(Trim(VersionText));
  NormalizedSeries := Uppercase(Trim(Series));
  if Copy(NormalizedVersion, 1, 1) = 'R' then
    Delete(NormalizedVersion, 1, 1);
  if Copy(NormalizedSeries, 1, 1) = 'R' then
    Delete(NormalizedSeries, 1, 1);
  Result := (GetVersionPart(NormalizedVersion, 0) =
      GetVersionPart(NormalizedSeries, 0)) and
    (GetVersionPart(NormalizedVersion, 1) =
      GetVersionPart(NormalizedSeries, 1));
end;

function TryGetAutoCADRunning(var Running: Boolean): Boolean;
var
  Locator: Variant;
  Services: Variant;
  Processes: Variant;
begin
  Running := False;
  ProcessInspectionError := '';
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Services := Locator.ConnectServer('', 'root\CIMV2');
    Processes := Services.ExecQuery(
      'SELECT Name FROM Win32_Process WHERE Name="acad.exe" OR Name="accoreconsole.exe"');
    Running := Processes.Count > 0;
    Result := True;
  except
    ProcessInspectionError := GetExceptionMessage;
    Result := False;
  end;
end;

function AutoCADClosedOrExplain: String;
var
  Running: Boolean;
begin
  Result := '';
  if IsTestBuild and
    (ExpandConstant('{param:ALLOWRUNNINGCAD|0}') = '1') then
    Exit;
  if not TryGetAutoCADRunning(Running) then
  begin
    Result := '无法安全检查 AutoCAD 进程，安装已停止。' + Chr(13) + Chr(10) +
      ProcessInspectionError;
    Exit;
  end;
  if Running then
    Result := '检测到 AutoCAD 或 Core Console 正在运行。请保存图纸并完全关闭 AutoCAD，' +
      '然后重试。安装器不会强制结束进程。';
end;

function TryGetExecutableProductVersion(const FileName: String;
  var ProductVersion: String): Boolean;
var
  ShellApplication: Variant;
  ShellFolder: Variant;
  ShellItem: Variant;
begin
  Result := False;
  ProductVersion := '';
  if not FileExists(FileName) then
    Exit;
  try
    ShellApplication := CreateOleObject('Shell.Application');
    ShellFolder := ShellApplication.NameSpace(ExtractFileDir(FileName));
    if VarIsClear(ShellFolder) then
      Exit;
    ShellItem := ShellFolder.ParseName(ExtractFileName(FileName));
    if VarIsClear(ShellItem) then
      Exit;
    ProductVersion := Trim(String(ShellItem.ExtendedProperty(
      'System.Software.ProductVersion')));
    Result := ProductVersion <> '';
  except
    Log('Could not read acad.exe ProductVersion: ' + GetExceptionMessage);
    ProductVersion := '';
    Result := False;
  end;
end;

function CadSupportLabel(const Series: String): String;
begin
  if ListContains(VerifiedSeriesList, Series) then
    Result := '支持（核心运行已验证；完整 GUI 仍需验收，安装后会加载）'
  else if ListContains(CandidateLegacySeriesList, Series) then
    Result := '候选（.NET Framework，尚未验证；默认允许安装，能否加载需实测）'
  else if ListContains(CandidateNet8SeriesList, Series) then
    Result := '候选（.NET 8，尚未验证；默认允许安装，能否加载需实测）'
  else
    Result := '未验证（默认允许安装，能否加载需实测）';
end;

function AutoCADExecutableVersionMatches(const Series, FileVersion,
  ProductVersion: String): Boolean;
begin
  Result := VersionMatchesSeries(FileVersion, Series) or
    VersionMatchesSeries(ProductVersion, Series);
end;

function AutoCADIdentityMatches(const Series, ReleaseVersion, FileVersion,
  ProductVersion, ManagedApiVersion: String;
  ExecutableExists, ManagedApiExists: Boolean): Boolean;
begin
  Result := ExecutableExists and ManagedApiExists and
    VersionMatchesSeries(ReleaseVersion, Series) and
    AutoCADExecutableVersionMatches(Series, FileVersion, ProductVersion) and
    VersionMatchesSeries(ManagedApiVersion, Series);
end;

procedure AddAutoCADRegistryView(RootKey: Integer; const ViewLabel: String);
var
  BaseKey: String;
  SeriesKeys: TArrayOfString;
  ProductKeys: TArrayOfString;
  SeriesIndex: Integer;
  ProductIndex: Integer;
  Series: String;
  ProductKey: String;
  ProductName: String;
  AcadLocation: String;
  AcadExecutable: String;
  ManagedApiPath: String;
  FileVersion: String;
  ProductVersion: String;
  ManagedApiVersion: String;
  ReleaseVersion: String;
  ExecutableExists: Boolean;
  ManagedApiExists: Boolean;
  IdentityMatches: Boolean;
  CanInstall: Boolean;
  ConclusionText: String;
  StatusText: String;
begin
  BaseKey := 'SOFTWARE\Autodesk\AutoCAD';
  if not RegGetSubkeyNames(RootKey, BaseKey, SeriesKeys) then
    Exit;

  for SeriesIndex := 0 to GetArrayLength(SeriesKeys) - 1 do
  begin
    Series := SeriesKeys[SeriesIndex];
    if not RegGetSubkeyNames(RootKey, BaseKey + '\' + Series, ProductKeys) then
      Continue;

    for ProductIndex := 0 to GetArrayLength(ProductKeys) - 1 do
    begin
      if Pos('ACAD-', Uppercase(ProductKeys[ProductIndex])) <> 1 then
        Continue;
      ProductKey := BaseKey + '\' + Series + '\' + ProductKeys[ProductIndex];
      ProductName := '';
      AcadLocation := '';
      ReleaseVersion := '';
      RegQueryStringValue(RootKey, ProductKey, 'ProductName', ProductName);
      RegQueryStringValue(RootKey, ProductKey, 'AcadLocation', AcadLocation);
      RegQueryStringValue(RootKey, ProductKey, 'Release', ReleaseVersion);
      if AcadLocation = '' then
        Continue;
      if ProductName = '' then
        ProductName := 'AutoCAD ' + Series;
      AcadExecutable := AddBackslash(AcadLocation) + 'acad.exe';
      ManagedApiPath := AddBackslash(AcadLocation) + 'acdbmgd.dll';
      FileVersion := '';
      ProductVersion := '';
      ManagedApiVersion := '';
      ExecutableExists := FileExists(AcadExecutable);
      ManagedApiExists := FileExists(ManagedApiPath);
      if ExecutableExists then
      begin
        GetVersionNumbersString(AcadExecutable, FileVersion);
        TryGetExecutableProductVersion(AcadExecutable, ProductVersion);
      end;
      if ManagedApiExists then
        GetVersionNumbersString(ManagedApiPath, ManagedApiVersion);

      IdentityMatches := AutoCADIdentityMatches(Series, ReleaseVersion,
        FileVersion, ProductVersion, ManagedApiVersion,
        ExecutableExists, ManagedApiExists);
      CanInstall := IdentityMatches and ListContains(VerifiedSeriesList, Series);
      if IdentityMatches then
        StatusText := CadSupportLabel(Series)
      else
        StatusText := '检测异常（Series、Release、acad.exe程序版本或托管API版本不一致；不会安装）';

      if CanInstall then
        ConclusionText := '可以安装'
      else
        ConclusionText := '当前不支持';

      if CadDetectionText <> '' then
        CadDetectionText := CadDetectionText + Chr(13) + Chr(10);
      if CanInstall then
        CadDetectionText := CadDetectionText + '- ' + ProductName + '：可以安装'
      else if IdentityMatches then
        CadDetectionText := CadDetectionText + '- ' + ProductName +
          '：当前版本暂不支持'
      else
        CadDetectionText := CadDetectionText + '- ' + ProductName +
          '：检测异常，请检查 AutoCAD 安装';

      Log('AutoCAD detection: ' + ProductName + ' [' + Series + ']');
      Log('  结论：' + ConclusionText);
      Log('  状态说明：' + StatusText);
      if ReleaseVersion <> '' then
        Log('  注册版本：' + ReleaseVersion);
      if FileVersion <> '' then
        Log('  acad.exe固定文件版本：' + FileVersion);
      if ProductVersion <> '' then
        Log('  acad.exe产品版本：' + ProductVersion);
      if ManagedApiVersion <> '' then
        Log('  acdbmgd.dll版本：' + ManagedApiVersion);
      if not ExecutableExists then
        Log('  警告：注册表路径中的 acad.exe 不存在');
      if not ManagedApiExists then
        Log('  警告：注册表路径中的 acdbmgd.dll 不存在');
      Log('  注册表：' + ViewLabel);
      if CanInstall then
        HasVerifiedCad := True;
    end;
  end;
end;

procedure DetectAutoCADInstallations;
begin
  CadDetectionText := '';
  HasVerifiedCad := False;
  AddAutoCADRegistryView(HKLM64, '本机');
  AddAutoCADRegistryView(HKCU64, '当前用户');
  if CadDetectionText = '' then
    CadDetectionText := '- 未检测到 AutoCAD 2020，可以先安装工具箱，之后再安装 AutoCAD。';
  CadDetectionText := '主要功能' + Chr(13) + Chr(10) +
    '- 自动画导轨' + Chr(13) + Chr(10) +
    '- 自动布置配重' + Chr(13) + Chr(10) +
    '- 导轨长度和配重数量统计' +
    Chr(13) + Chr(10) + Chr(13) + Chr(10) +
    'AutoCAD 检测' + Chr(13) + Chr(10) + CadDetectionText +
    Chr(13) + Chr(10) + Chr(13) + Chr(10) +
    '安装位置' + Chr(13) + Chr(10) +
    '工具箱需要放在 AutoCAD 固定的插件目录中，因此不提供自定义安装地址。' +
    Chr(13) + Chr(10) +
    '- 仅为我安装：' + GetUserBundlePath + Chr(13) + Chr(10) +
    '- 为所有用户安装：' + GetSystemBundlePath + Chr(13) + Chr(10) +
    '下一步可以选择安装范围。已有版本会自动升级，请保持与上次相同的安装范围。';
  Log('Verified installer target: ' + VerifiedTargetLabelText);
end;

function GetCadPageSubCaption: String;
begin
  if HasVerifiedCad then
    Result := '已检测到支持的 AutoCAD，可以继续安装。'
  else
    Result := '当前未检测到支持的 AutoCAD，工具箱仍可预先安装。';
end;

function AllowNoCad: Boolean;
begin
  Result := True;
end;

function AllowDowngrade: Boolean;
begin
  Result := IsTestBuild and
    (ExpandConstant('{param:ALLOWDOWNGRADE|0}') = '1');
end;

function ValidateBundleAt(const BundlePath: String; var ExistingVersion: String): String;
begin
  Result := '';
  ExistingVersion := '';
  if not DirExists(BundlePath) then
    Exit;
  if not IsOwnedBundle(BundlePath, ExistingVersion) then
    Result := '发现同名插件目录，但 ProductCode/UpgradeCode 不属于四季光伏工具箱。' +
      Chr(13) + Chr(10) + BundlePath + Chr(13) + Chr(10) +
      '安装器不会覆盖或删除它。';
end;

function OtherInstallScopeMessage: String;
var
  UninstallKey: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' +
    InstallerAppIdText + '}_is1';
  if IsAdminInstallMode and RegKeyExists(HKCU64, UninstallKey) then
    Result := '检测到当前用户范围的EXE安装记录。请先从Windows“已安装的应用”' +
      '卸载该版本，再选择为所有用户安装。'
  else if (not IsAdminInstallMode) and RegKeyExists(HKLM64, UninstallKey) then
    Result := '检测到所有用户范围的EXE安装记录。请使用管理员账户升级该版本，' +
      '或先卸载后再改为当前用户安装。';
end;

function CreateBackupName(const BundlePath: String): String;
begin
  Result := BundlePath + '.backup.' +
    GetDateTimeString('yyyymmddhhnnsszzz', '-', '-');
end;

function BackupBundle(const BundlePath: String; var BackupPath: String): Boolean;
begin
  Result := True;
  BackupPath := '';
  if not DirExists(BundlePath) then
    Exit;
  BackupPath := CreateBackupName(BundlePath);
  Result := RenameFile(BundlePath, BackupPath);
end;

procedure RestoreBackup(const OriginalPath, BackupPath: String; WasBackedUp: Boolean);
begin
  if not WasBackedUp or not DirExists(BackupPath) then
    Exit;
  if DirExists(OriginalPath) then
    DelTree(OriginalPath, True, True, True);
  if not RenameFile(BackupPath, OriginalPath) then
    Log('ERROR: failed to restore bundle backup: ' + BackupPath);
end;

procedure RemoveBackup(const BackupPath: String; WasBackedUp: Boolean);
begin
  if WasBackedUp and DirExists(BackupPath) then
    if not DelTree(BackupPath, True, True, True) then
      Log('WARNING: installed successfully but could not delete backup: ' + BackupPath);
end;

procedure CleanupStaleUninstallers;
var
  RootKey: Integer;
  UninstallKey: String;
  CurrentUninstaller: String;
  CurrentDataFile: String;
  UninstallDirectory: String;
  CandidatePath: String;
  Extension: String;
  FindRec: TFindRec;
begin
  if IsAdminInstallMode then
    RootKey := HKLM64
  else
    RootKey := HKCU64;
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' +
    InstallerAppIdText + '}_is1';
  if not RegQueryStringValue(RootKey, UninstallKey, 'UninstallString',
    CurrentUninstaller) then
  begin
    Log('Could not resolve the current uninstaller; stale cleanup skipped.');
    Exit;
  end;

  CurrentUninstaller := RemoveQuotes(CurrentUninstaller);
  CurrentDataFile := ChangeFileExt(CurrentUninstaller, '.dat');
  UninstallDirectory := AddBackslash(ExtractFileDir(CurrentUninstaller));
  if not FindFirst(UninstallDirectory + 'unins*.*', FindRec) then
    Exit;
  try
    repeat
      CandidatePath := UninstallDirectory + FindRec.Name;
      Extension := Lowercase(ExtractFileExt(CandidatePath));
      if FileExists(CandidatePath) and
        ((Extension = '.exe') or (Extension = '.dat') or (Extension = '.msg')) and
        (CompareText(CandidatePath, CurrentUninstaller) <> 0) and
        (CompareText(CandidatePath, CurrentDataFile) <> 0) then
      begin
        if DeleteFile(CandidatePath) then
          Log('Removed stale uninstaller file: ' + CandidatePath)
        else
          Log('WARNING: could not remove stale uninstaller file: ' + CandidatePath);
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  UserVersion: String;
  SystemVersion: String;
  ProcessMessage: String;
  HighestVersion: String;
begin
  Log('PrepareToInstall started.');
  Result := '';
  NeedsRestart := False;

  ProcessMessage := AutoCADClosedOrExplain;
  if ProcessMessage <> '' then
  begin
    Result := ProcessMessage;
    Exit;
  end;
  if not HasVerifiedCad and not AllowNoCad then
  begin
    Result := '没有检测到本安装包已完成核心运行验证的 AutoCAD 版本。' +
      Chr(13) + Chr(10) +
      '候选版本不会被强行加载。';
    Exit;
  end;

  Log('Checking opposite-scope installer registration.');
  Result := OtherInstallScopeMessage;
  if Result <> '' then
    Exit;

  TargetBundlePath := GetBundlePath('');
  Log('Target bundle: ' + TargetBundlePath);
  SecondaryBundlePath := '';
  if not IsTestBuild then
  begin
    Result := ValidateBundleAt(GetUserBundlePath, UserVersion);
    if Result <> '' then Exit;
    Result := ValidateBundleAt(GetSystemBundlePath, SystemVersion);
    if Result <> '' then Exit;

    HighestVersion := UserVersion;
    if CompareVersionText(SystemVersion, HighestVersion) > 0 then
      HighestVersion := SystemVersion;

    if not IsAdminInstallMode and DirExists(GetSystemBundlePath) then
    begin
      Result := '检测到旧版位于系统插件目录：' + GetSystemBundlePath +
        Chr(13) + Chr(10) +
        '请返回“安装模式”选择“为所有用户安装”，以便安全迁移；' +
        '当前用户模式不会留下两份插件。';
      Exit;
    end;

    if IsAdminInstallMode and DirExists(GetUserBundlePath) then
      SecondaryBundlePath := GetUserBundlePath;
  end
  else
  begin
    Log('Validating existing test bundle.');
    Result := ValidateBundleAt(TargetBundlePath, UserVersion);
    if Result <> '' then Exit;
    HighestVersion := UserVersion;
  end;

  if (HighestVersion <> '') and
    (CompareVersionText(HighestVersion, CurrentVersion) > 0) and
    not AllowDowngrade then
  begin
    Result := '已安装版本 ' + HighestVersion + ' 高于当前安装包 ' +
      CurrentVersion + '。普通安装禁止降级。';
    Exit;
  end;

  Log('Backing up target bundle if present.');
  TargetWasBackedUp := BackupBundle(TargetBundlePath, TargetBackupPath);
  if DirExists(TargetBundlePath) and not TargetWasBackedUp then
  begin
    Result := '无法备份现有插件，安装已停止：' + TargetBundlePath;
    Exit;
  end;

  if SecondaryBundlePath <> '' then
  begin
    SecondaryWasBackedUp := BackupBundle(SecondaryBundlePath, SecondaryBackupPath);
    if DirExists(SecondaryBundlePath) and not SecondaryWasBackedUp then
    begin
      RestoreBackup(TargetBundlePath, TargetBackupPath, TargetWasBackedUp);
      TargetWasBackedUp := False;
      Result := '无法迁移另一份插件，安装已停止：' + SecondaryBundlePath;
      Exit;
    end;
  end;


  if IsTestBuild and
    (ExpandConstant('{param:FAILAFTERBACKUP|0}') = '1') then
  begin
    Result := 'TEST_ONLY: simulated failure after backup.';
    Exit;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then
  begin
    InstallSucceeded := True;
    RemoveBackup(TargetBackupPath, TargetWasBackedUp);
    RemoveBackup(SecondaryBackupPath, SecondaryWasBackedUp);
    CleanupStaleUninstallers;
  end;
end;

procedure DeinitializeSetup;
begin
  if not InstallSucceeded then
  begin
    RestoreBackup(TargetBundlePath, TargetBackupPath, TargetWasBackedUp);
    RestoreBackup(SecondaryBundlePath, SecondaryBackupPath, SecondaryWasBackedUp);
  end;
end;

function InitializeSetup: Boolean;
begin
  InstallSucceeded := False;
  TargetWasBackedUp := False;
  SecondaryWasBackedUp := False;
  DetectAutoCADInstallations;
  Result := True;
end;

procedure InitializeWizard;
begin
  CadPage := CreateOutputMsgPage(wpWelcome,
    '安装信息',
    '四季光伏工具箱将安装到 AutoCAD 的标准插件目录。' +
      Chr(13) + Chr(10) + GetCadPageSubCaption,
    CadDetectionText);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = CadPage.ID) and (not HasVerifiedCad) and
    (not AllowNoCad) then
  begin
    SuppressibleMsgBox(
      '当前没有检测到本安装包已验证的 AutoCAD 宿主，安装已阻止。' +
      Chr(13) + Chr(10) +
      '目标：' + VerifiedTargetLabelText + Chr(13) + Chr(10) +
      '请关闭本安装器，安装匹配的 AutoCAD 版本后重新运行。',
      mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function InitializeUninstall: Boolean;
var
  ProcessMessage: String;
  InstalledVersion: String;
begin
  Result := False;
  ProcessMessage := AutoCADClosedOrExplain;
  if ProcessMessage <> '' then
  begin
    SuppressibleMsgBox(ProcessMessage, mbError, MB_OK, IDOK);
    Exit;
  end;

  UninstallBundlePath := GetBundlePath('');
  OwnedBundleAtUninstall := IsOwnedBundle(UninstallBundlePath, InstalledVersion);
  if DirExists(UninstallBundlePath) and not OwnedBundleAtUninstall then
  begin
    SuppressibleMsgBox('插件目录的 ProductCode/UpgradeCode 与本产品不符，卸载已停止，' +
      '不会删除该目录。' + Chr(13) + Chr(10) + UninstallBundlePath,
      mbError, MB_OK, IDOK);
    Exit;
  end;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and OwnedBundleAtUninstall and
    DirExists(UninstallBundlePath) then
  begin
    if not DelTree(UninstallBundlePath, True, True, True) then
      SuppressibleMsgBox('程序文件未能完全删除，请在确认 AutoCAD 已关闭后手工检查：' +
        Chr(13) + Chr(10) + UninstallBundlePath, mbError, MB_OK, IDOK);
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption :=
      '安装已完成。' + Chr(13) + Chr(10) + Chr(13) + Chr(10) +
      '请先启动或重启 AutoCAD，再在命令行输入 JJJJ 打开四季光伏工具箱。';
end;
