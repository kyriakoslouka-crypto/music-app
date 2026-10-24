param([string]$Artifacts="")
$ErrorActionPreference='Stop'
if(!$Artifacts){$Artifacts=Join-Path $PSScriptRoot '../artifacts'}
$Artifacts=(Resolve-Path $Artifacts).Path
[xml]$props=Get-Content (Join-Path $PSScriptRoot '../Directory.Build.props')
$version=$props.Project.PropertyGroup.Version
$base=Split-Path $PSScriptRoot -Parent
$archive=Join-Path $base "deliverables/releases/$version/ProfessionalEarTrainingStudio-$version-Source.zip"
$target=Join-Path $env:TEMP ('PETS-source-smoke-'+[guid]::NewGuid().ToString('N'))
$result=[ordered]@{Version=$version;Passed=$false;FrozenRestorePassed=$false;SourceBuildPassed=$false;TypedUiAProjectBuildPassed=$false}
try{
    Expand-Archive -Path $archive -DestinationPath $target
    Push-Location (Join-Path $target 'desktop')
    try{
        dotnet restore ProfessionalEarTrainingStudio.sln --locked-mode
        if($LASTEXITCODE -ne 0){throw 'Source ZIP frozen solution restore failed.'}
        dotnet restore tests/Studio.UIAutomation/Studio.UIAutomation.csproj --locked-mode
        if($LASTEXITCODE -ne 0){throw 'Source ZIP frozen UIA-project restore failed.'}
        $result.FrozenRestorePassed=$true
        dotnet build ProfessionalEarTrainingStudio.sln -c Release --no-restore
        if($LASTEXITCODE -ne 0){throw 'Source ZIP solution build failed.'}
        $result.SourceBuildPassed=$true
        dotnet build tests/Studio.UIAutomation/Studio.UIAutomation.csproj -c Release --no-restore
        if($LASTEXITCODE -ne 0){throw 'Source ZIP UIA-project build failed.'}
        $result.TypedUiAProjectBuildPassed=$true;$result.Passed=$true
    }finally{Pop-Location}
}catch{$result.Error=$_.Exception.Message;throw}
finally{$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Artifacts 'source-build-smoke.json') -Encoding utf8}
