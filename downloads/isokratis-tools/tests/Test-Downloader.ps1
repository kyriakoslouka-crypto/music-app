param([string]$Report)
$ErrorActionPreference='Stop'
$root=Join-Path $env:TEMP ('pets-downloader-tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root|Out-Null
$tool=Join-Path $PSScriptRoot '../Download-Isokratis-Voices.ps1'
$fixture=Join-Path $PSScriptRoot 'fixture.py'
$checks=[Collections.Generic.List[object]]::new()
function Check($ok,$name){if(!$ok){throw $name};$checks.Add([pscustomobject]@{Name=$name;Passed=$true});Write-Host ('PASS '+$name)}
$server=$null;$failure=$null
try{
 $tokens=$null;$errors=$null
 [Management.Automation.Language.Parser]::ParseFile($tool,[ref]$tokens,[ref]$errors)|Out-Null
 Check (@($errors).Count -eq 0) 'Actual Windows PowerShell parser accepts script'
 Check ($PSVersionTable.PSVersion.Major -eq 5) 'Tests execute under Windows PowerShell 5.1'
 $portFile=Join-Path $root 'port.txt';$requests=Join-Path $root 'requests.txt'
 $server=Start-Process python -ArgumentList @('-u',('"'+$fixture+'"'),('"'+$portFile+'"'),('"'+$requests+'"')) -PassThru -WindowStyle Hidden
 for($i=0;$i -lt 100 -and !(Test-Path $portFile);$i++){Start-Sleep -Milliseconds 100}
 Check (Test-Path $portFile) 'Offline HTTP fixture started'
 $base='http://127.0.0.1:'+(Get-Content $portFile -Raw)+'/'
 $destination=Join-Path $root 'voices with spaces'
 & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $tool -BaseUri $base -OutputDirectory $destination *> (Join-Path $root 'run.log')
 $status=$LASTEXITCODE
 Check ($status -eq 2) 'HTML pretending to be MP3 produces explicit partial-success exit code'
 $result=Get-Content (Join-Path $destination 'DOWNLOAD-REPORT.json') -Raw|ConvertFrom-Json
 Check ($result.KnownFiles -eq 59 -and $result.Downloaded -eq 60 -and $result.Missing -eq 1 -and !$result.AllPlannedDownloaded) 'All 59 known samples and one discovered sample downloaded, bad file excluded'
 Check (!(Test-Path (Join-Path $destination 'audio/not-audio.mp3'))) 'Rejected media is absent from output'
 Check (@($result.IgnoredReferences).Count -ge 2) 'External and metadata-host URLs are never followed'
 Check (!(Get-Content $requests|Where-Object {$_ -match 'secret|DO.NOT.EXECUTE'})) 'Remote JavaScript is never executed and private URL is not requested'
 $record=$result.Files|Where-Object Path -eq 'voices_pa3/g3_ison1.mp3'
 Check ($record.DeclaredRootHz -eq 98 -and $record.SHA256 -eq (Get-FileHash (Join-Path $destination $record.Path) -Algorithm SHA256).Hash.ToLowerInvariant()) 'Original sample bytes and declared base frequency preserved with SHA-256'
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $parts=@(Get-ChildItem -LiteralPath $root -Filter 'voices with spaces.part*.zip');Check ($parts.Count -ge 2) 'Large downloads split into independent upload-sized ZIP files';Check (@($parts|Where-Object Length -gt 25165824).Count -eq 0) 'Every ZIP is below 24 MiB';
 $archivePath=$destination+'.part001.zip';$zip=[IO.Compression.ZipFile]::OpenRead($archivePath)
 try{
  Check ($null -ne $zip.GetEntry('voices_pa3/g3_ison1.mp3') -and $null -ne $zip.GetEntry('DOWNLOAD-REPORT.json')) 'ZIP contains audio and truthful report'
  Check ($null -eq $zip.GetEntry('audio/not-audio.mp3')) 'ZIP excludes rejected HTML payload'
 }finally{$zip.Dispose()}
 $before=(Get-FileHash (Join-Path $destination 'DOWNLOAD-REPORT.json')).Hash
 & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $tool -BaseUri $base -OutputDirectory $destination -NoDiscovery *> (Join-Path $root 'repeat.log')
 Check ($LASTEXITCODE -eq 1 -and $before -eq (Get-FileHash (Join-Path $destination 'DOWNLOAD-REPORT.json')).Hash) 'Existing user output is never overwritten'
 $clean=Join-Path $root 'known-only'
 & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $tool -BaseUri $base -OutputDirectory $clean -NoDiscovery *> (Join-Path $root 'clean.log')
 Check ($LASTEXITCODE -eq 0) 'Known-only complete download succeeds'
 $r=Get-Content (Join-Path $clean 'DOWNLOAD-REPORT.json') -Raw|ConvertFrom-Json
 Check ($r.Downloaded -eq 59 -and $r.AllPlannedDownloaded) 'Complete fixture reports exact success count'
 $unreachable=Join-Path $root 'unreachable'
 & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $tool -BaseUri 'http://127.0.0.1:1/' -OutputDirectory $unreachable -NoDiscovery *> (Join-Path $root 'unreachable.log')
 Check ($LASTEXITCODE -eq 2) 'Unavailable host creates partial report instead of fake success'
 $r=Get-Content (Join-Path $unreachable 'DOWNLOAD-REPORT.json') -Raw|ConvertFrom-Json
 Check ($r.Downloaded -eq 0 -and $r.Missing -eq 59 -and @($r.Files|Where-Object Status -eq 'NotAttempted').Count -eq 54) 'Circuit breaker stops after five failures and records unattempted files'
}catch{$failure=$_.Exception.ToString();Write-Host $failure}
finally{if($server -and !$server.HasExited){$server.Kill()}}
$result=[ordered]@{Passed=($null -eq $failure);Executed=$checks.Count;PowerShell=$PSVersionTable.PSVersion.ToString();Windows=[Environment]::OSVersion.VersionString;ExternalServiceTested=$false;FixtureUsesCopyrightedSamples=$false;Error=$failure;Checks=@($checks);SourceCommit=$env:GITHUB_SHA}
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $Report -Encoding UTF8
if($failure){Get-ChildItem $root -Filter '*.log'|ForEach-Object {Write-Host $_.Name;Get-Content $_.FullName|Select-Object -Last 10};exit 1}
