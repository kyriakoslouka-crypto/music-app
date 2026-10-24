param([switch]$Installer,[switch]$Accessibility,[string]$SoundFont,[string]$Maestro)
$ErrorActionPreference = 'Stop'
[xml]$studioProps=Get-Content (Join-Path $PSScriptRoot '../Directory.Build.props')
$studioVersion=$studioProps.Project.PropertyGroup.Version
Set-Location (Split-Path $PSScriptRoot -Parent)
function Run-Dotnet { & dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $args" } }
Run-Dotnet restore ProfessionalEarTrainingStudio.sln --locked-mode
Run-Dotnet build ProfessionalEarTrainingStudio.sln -c Release --no-restore
New-Item -ItemType Directory artifacts -Force | Out-Null
$studioTestArgs=@('run','--project','tests/Studio.Tests','-c','Release','--no-build','--','--report',"artifacts/tests-$studioVersion.json")
if($SoundFont){$studioTestArgs+=@('--soundfont',$SoundFont)}
if($Maestro){$studioTestArgs+=@('--maestro',$Maestro)}
Run-Dotnet @studioTestArgs
Run-Dotnet publish src/Studio.Windows/Studio.Windows.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
Run-Dotnet publish src/Studio.Windows/Studio.Windows.csproj -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:EnableCompressionInSingleFile=true' '-p:DebugType=none' '-p:DebugSymbols=false' -o artifacts/standalone-win-x64
if($Accessibility){ & "$PSScriptRoot/Test-Accessibility.ps1" -Exe "$PWD/artifacts/win-x64/ProfessionalEarTrainingStudio.exe" -Report "$PWD/artifacts/accessibility-audit.json" }
Copy-Item README.md,THIRD-PARTY-NOTICES.md artifacts/win-x64/
Copy-Item docs artifacts/win-x64/ -Recurse -Force
Copy-Item src/Studio.Windows/Assets/ThirdPartyLicenses.txt artifacts/win-x64/SOFTWARE-LICENSES.txt
New-Item -ItemType Directory artifacts/win-x64/tools -Force | Out-Null
Copy-Item scripts/Test-Accessibility.ps1 artifacts/win-x64/tools/
Compress-Archive -Path artifacts/win-x64/* -DestinationPath "artifacts/ProfessionalEarTrainingStudio-$studioVersion-win-x64.zip" -Force
if ($Installer) {
    $nsis = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if (!$nsis) { throw 'Install NSIS 3 and add makensis.exe to PATH, then run again with -Installer.' }
    $publishRoot = (Resolve-Path artifacts/win-x64).Path
    $manifest = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem $publishRoot -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($publishRoot.Length + 1)
        $manifest.Add('Delete "$INSTDIR\' + $relative + '"')
    }
    Get-ChildItem $publishRoot -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
        $manifest.Add('RMDir "$INSTDIR\' + $_.FullName.Substring($publishRoot.Length + 1) + '"')
    }
    $manifest | Set-Content artifacts/uninstall-files.nsh -Encoding utf8
    & $nsis.Source "-DAPP_VERSION=$studioVersion" installer/Studio.nsi
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
}
Get-ChildItem artifacts -File | Get-FileHash -Algorithm SHA256 | Format-Table
