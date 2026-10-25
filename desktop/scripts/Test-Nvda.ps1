param([string]$Exe="",[string]$Artifacts="")
$ErrorActionPreference='Stop'
# Do not replace, quit or reconfigure a user's existing NVDA session.
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Run this isolated NVDA test only on a disposable GitHub Actions Windows runner.'}
if(Get-Process nvda -ErrorAction SilentlyContinue){throw 'Existing NVDA process detected; refusing to interfere.'}
if(!$Artifacts){$Artifacts=Join-Path $PSScriptRoot '../artifacts'}
$Artifacts=(Resolve-Path $Artifacts).Path
if(!$Exe){$Exe=Join-Path $Artifacts 'win-x64/ProfessionalEarTrainingStudio.exe'}
$version='2026.2'
$expectedHash='f3f8d29974a88d687b3c4809be192219ec579c5bdabcda5aaf53635288bca824'
$privateRoot=Join-Path $env:RUNNER_TEMP ('PETS-NVDA-'+[guid]::NewGuid().ToString('N'))
$portable=Join-Path $privateRoot 'portable';$config=Join-Path $privateRoot 'config';$log=Join-Path $privateRoot 'nvda.log'
$result=[ordered]@{Passed=$false;SourceCommit=$env:GITHUB_SHA;NvdaVersion=$version;InstallerSha256=$expectedHash;SignatureValid=$false;ActualNvdaProcessTested=$false;SpeechAnnouncementsPassed=$false;Windows11UserTested=$false;HumanListeningTested=$false;Synthesizer='silence';Checks=@()}
$nvda=$null;$started=$false
function Run-Bounded([string]$file,[string[]]$arguments,[int]$milliseconds){
    $p=Start-Process $file -ArgumentList $arguments -PassThru
    if(!$p.WaitForExit($milliseconds)){$p.Kill($true);throw "Process timeout: $(Split-Path $file -Leaf)"}
    return $p.ExitCode
}
try{
    New-Item -ItemType Directory $privateRoot,$config -Force | Out-Null
    $installer=Join-Path $privateRoot "nvda_$version.exe"
    Invoke-WebRequest "https://github.com/nvaccess/nvda/releases/download/release-$version/nvda_$version.exe" -OutFile $installer
    if((Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash){throw 'Official NVDA installer SHA-256 mismatch.'}
    $signature=Get-AuthenticodeSignature $installer
    if($signature.Status -ne 'Valid'){throw "Official NVDA Authenticode signature invalid: $($signature.Status)"}
    $result.SignatureValid=$true
    if((Run-Bounded $installer @('--create-portable-silent',('--portable-path="'+$portable+'"'),'--minimal') 120000) -ne 0){throw 'Portable NVDA extraction failed.'}
    $nvda=Join-Path $portable 'nvda.exe'
    if(!(Test-Path $nvda)){throw 'NVDA portable executable missing.'}
    @'
[general]
language = el
showWelcomeDialogAtStartup = False
askToExit = False
playStartAndExitSounds = False
[speech]
synth = silence
[update]
autoCheck = False
startupNotification = False
'@ | Set-Content (Join-Path $config 'nvda.ini') -Encoding utf8
    Start-Process $nvda -ArgumentList @('--minimal','--disable-addons','--log-level=10',('--log-file="'+$log+'"'),('--config-path="'+$config+'"'),'--lang=el') | Out-Null
    $started=$true;$ready=$false
    for($i=0;$i -lt 30;$i++){
        Start-Sleep -Milliseconds 500
        if((Run-Bounded $nvda @('--check-running') 5000) -eq 0){$ready=$true;break}
    }
    if(!$ready){throw 'NVDA did not start within the bounded startup check.'}
    $result.ActualNvdaProcessTested=$true
    # Allow initial configuration and accessibility hooks to finish before sending test keys.
    Start-Sleep -Seconds 2
    $auditPath=Join-Path $Artifacts 'nvda-accessibility-audit.json'
    & "$PSScriptRoot/Test-Accessibility.ps1" -Exe $Exe -Report $auditPath -NvdaLog $log
    $audit=Get-Content $auditPath -Raw | ConvertFrom-Json
    $result.Checks=@($audit.NvdaSpeechChecks)
    if(!$audit.UIAutomationPassed -or $result.Checks.Count -ne 5 -or @($result.Checks | Where-Object {!$_.Passed}).Count){throw 'Incomplete real NVDA announcements/UIA keyboard evidence.'}
    $result.SpeechAnnouncementsPassed=$true;$result.Passed=$true
}catch{
    $result.Error=$_.Exception.Message
    # Raw logs stay private. Include only bounded failure diagnostics, never the runner's whole debug log.
    if(Test-Path $log){$result.NvdaErrorTail=@(Get-Content $log -Tail 50 -Encoding utf8 | Where-Object {$_ -match '^ERROR|^CRITICAL|Traceback|Error:|Exception:'})}
    throw
}finally{
    if($started -and $nvda -and (Test-Path $nvda)){
        try{[void](Run-Bounded $nvda @('--quit') 10000)}catch{$result.ShutdownError=$_.Exception.Message}
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $Artifacts 'nvda-smoke.json') -Encoding utf8
}
