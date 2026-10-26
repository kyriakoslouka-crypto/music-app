$ErrorActionPreference='Stop'
$base=Split-Path $PSScriptRoot -Parent
[xml]$props=Get-Content "$base/Directory.Build.props"
$version=$props.Project.PropertyGroup.Version
$artifacts=Join-Path $base 'artifacts'
$root=Join-Path $env:TEMP ('Isokratis-package-'+[guid]::NewGuid().ToString('N'))
$result=[ordered]@{Version=$version;SourceCommit=$env:GITHUB_SHA;Passed=$false;InstallerPassed=$false;PortablePassed=$false;StandalonePassed=$false;TrainingHasFiveTabs=$false;IsokratisHasThreeTabs=$false;UninstallPreservesUserFile=$false}
try{
    $exe=Join-Path $artifacts 'isokratis-win-x64/ProfessionalIsokratis.exe'
    dotnet run --project "$base/tests/Studio.UIAutomation" -c Release --no-build -- $exe "$artifacts/isokratis-typed-uia.json" --isokratis
    if($LASTEXITCODE -ne 0){throw 'Independent typed UIA checks failed'}
    $training=Get-Content "$artifacts/accessibility-audit.json" -Raw | ConvertFrom-Json
    if(!$training.ApplicationSplit.Passed -or $training.ApplicationSplit.TabNames.Count -ne 5){throw 'Training tabs were not reduced to five'}
    $result.TrainingHasFiveTabs=$true
    python "$PSScriptRoot/check-portable.py" "$artifacts/ProfessionalIsokratis-$version-win-x64.zip" --report "$artifacts/isokratis-portable-integrity.json" --exe ProfessionalIsokratis.exe
    if($LASTEXITCODE -ne 0){throw 'Independent portable failed'}
    Expand-Archive "$artifacts/ProfessionalIsokratis-$version-win-x64.zip" "$root/portable"
    $installer=Start-Process "$artifacts/ProfessionalIsokratis-$version-Setup.exe" -ArgumentList @('/S',"/D=$root/installed") -PassThru -Wait
    if($installer.ExitCode -ne 0){throw 'Independent installer failed'}
    foreach($file in Get-ChildItem "$artifacts/isokratis-win-x64" -File -Recurse){
        $relative=$file.FullName.Substring((Resolve-Path "$artifacts/isokratis-win-x64").Path.Length+1)
        if((Get-FileHash $file.FullName).Hash -ne (Get-FileHash (Join-Path "$root/installed" $relative)).Hash){throw "Installed isokratis differs: $relative"}
    }
    foreach($variant in @('installed','portable','standalone')){
        $launch=if($variant -eq 'standalone'){"$artifacts/isokratis-standalone/ProfessionalIsokratis.exe"}else{"$root/$variant/ProfessionalIsokratis.exe"}
        $report="$artifacts/isokratis-$variant-audit.json"
        $process=Start-Process $launch -ArgumentList @('--accessibility-audit',('"'+$report+'"')) -PassThru
        if(!$process.WaitForExit(45000)){$process.Kill($true);throw 'Independent audit timed out'}
        if($process.ExitCode -ne 0){throw "Independent $variant startup failed"}
        $audit=Get-Content $report -Raw | ConvertFrom-Json
        if(!$audit.ApplicationSplit.Passed -or !$audit.ApplicationSplit.IsokratisOnly -or $audit.ApplicationSplit.TabNames.Count -ne 3 -or !$audit.ApplicationSplit.NoPianoSetup){throw 'Independent application contains training tabs'}
        $result.IsokratisHasThreeTabs=$true
        switch($variant){'installed'{$result.InstallerPassed=$true};'portable'{$result.PortablePassed=$true};'standalone'{$result.StandalonePassed=$true}}
    }
    'preserve user file' | Set-Content "$root/installed/personal.txt"
    $uninstaller=Start-Process "$root/installed/Uninstall.exe" -ArgumentList '/S' -PassThru -Wait
    for($i=0;$i -lt 50 -and (Test-Path "$root/installed/ProfessionalIsokratis.exe");$i++){Start-Sleep -Milliseconds 100}
    if($uninstaller.ExitCode -ne 0 -or (Test-Path "$root/installed/ProfessionalIsokratis.exe") -or !(Test-Path "$root/installed/personal.txt")){throw 'Independent uninstall was not safe'}
    $result.UninstallPreservesUserFile=$true;$result.Passed=$true
}catch{$result.Error=$_.Exception.Message;throw}
finally{$result | ConvertTo-Json -Depth 8 | Set-Content "$artifacts/isokratis-package-check.json" -Encoding utf8}
