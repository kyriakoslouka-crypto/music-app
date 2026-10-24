param([string]$Artifacts="")
$ErrorActionPreference='Stop'
if(!$Artifacts){$Artifacts=Join-Path $PSScriptRoot '../artifacts'}
$Artifacts=(Resolve-Path $Artifacts).Path
[xml]$props=Get-Content (Join-Path $PSScriptRoot '../Directory.Build.props')
$version=$props.Project.PropertyGroup.Version
$result=[ordered]@{Version=$version;Passed=$false;InstallerProcessPassed=$false;InstalledFilesMatch=$false;InstalledManagedAuditPassed=$false;StandaloneManagedAuditPassed=$false;UninstallPreservesUserFile=$false}
$installRoot=Join-Path $env:TEMP ('PETS-install-smoke-'+[guid]::NewGuid().ToString('N'))
try{
    python "$PSScriptRoot/check-portable.py" "$Artifacts/ProfessionalEarTrainingStudio-$version-win-x64.zip" --report "$Artifacts/portable-integrity.json"
    if($LASTEXITCODE -ne 0){throw 'Portable integrity failed.'}
    $installer=Start-Process "$Artifacts/ProfessionalEarTrainingStudio-$version-Setup.exe" -ArgumentList @('/S',"/D=$installRoot") -PassThru -Wait
    if($installer.ExitCode -ne 0){throw "Silent installation failed: $($installer.ExitCode)"}
    $result.InstallerProcessPassed=$true
    $publish=(Resolve-Path "$Artifacts/win-x64").Path
    $files=@(Get-ChildItem $publish -File -Recurse)
    foreach($file in $files){
        $relative=$file.FullName.Substring($publish.Length+1)
        $installed=Join-Path $installRoot $relative
        if(!(Test-Path $installed) -or (Get-FileHash $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $installed -Algorithm SHA256).Hash){throw "Installed file differs: $relative"}
    }
    $result.InstalledFilesMatch=$true;$result.InstalledFileCount=$files.Count
    foreach($variant in @('installed','standalone')){
        $exe=if($variant -eq 'installed'){Join-Path $installRoot 'ProfessionalEarTrainingStudio.exe'}else{"$Artifacts/standalone-win-x64/ProfessionalEarTrainingStudio.exe"}
        $auditPath=Join-Path $Artifacts ("package-$variant-accessibility.json")
        $process=Start-Process $exe -ArgumentList @('--accessibility-audit',('"'+$auditPath+'"')) -PassThru -Wait
        if($process.ExitCode -ne 0 -or !(Test-Path $auditPath)){throw "$variant EXE could not execute its native managed audit."}
        $audit=Get-Content $auditPath -Raw | ConvertFrom-Json
        if($audit.PresentationRegression.Count -ne 28){throw "$variant EXE did not verify all 28 exercise presentations."}
        if($variant -eq 'installed'){$result.InstalledManagedAuditPassed=$true}else{$result.StandaloneManagedAuditPassed=$true}
    }
    $personal=Join-Path $installRoot 'personal-smoke-note.txt'
    'Owned smoke-test marker: preserve user additions.' | Set-Content $personal -Encoding utf8
    $uninstall=Start-Process (Join-Path $installRoot 'Uninstall.exe') -ArgumentList '/S' -PassThru -Wait
    if($uninstall.ExitCode -ne 0){throw 'Silent uninstallation failed.'}
    for($i=0;$i -lt 50 -and (Test-Path (Join-Path $installRoot 'ProfessionalEarTrainingStudio.exe'));$i++){Start-Sleep -Milliseconds 100}
    if((Test-Path (Join-Path $installRoot 'ProfessionalEarTrainingStudio.exe')) -or !(Test-Path $personal)){throw 'Uninstaller failed to remove application or erased user-added file.'}
    $result.UninstallPreservesUserFile=$true;$result.Passed=$true
} catch{$result.Error=$_.Exception.Message;throw}
finally{$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Artifacts 'windows-package-smoke.json') -Encoding utf8}
Write-Output 'PASS native installer, exact installed-file hashes, installed/standalone startup and user-file-preserving uninstall.'
