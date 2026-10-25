#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [Uri]$BaseUri='https://isokratis-nek.netlify.app/',
    [switch]$NoDiscovery,
    [switch]$OpenFolder
)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
function Save-Json($Value,[string]$Path){$Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding UTF8}
function Test-Media([string]$Path){
    $bytes=[IO.File]::ReadAllBytes($Path)
    if($bytes.Length -lt 128){return $false}
    $ext=[IO.Path]::GetExtension($Path).ToLowerInvariant()
    $magic=[Text.Encoding]::ASCII.GetString($bytes,0,[Math]::Min(12,$bytes.Length))
    if($ext -eq '.wav'){return $magic.StartsWith('RIFF') -and $magic.Substring(8,4) -eq 'WAVE'}
    if($ext -eq '.flac'){return $magic.StartsWith('fLaC')}
    if($ext -eq '.ogg'){return $magic.StartsWith('OggS')}
    if($ext -in @('.m4a','.mp4')){return $magic.Substring(4,4) -eq 'ftyp'}
    if($ext -eq '.caf'){return $magic.StartsWith('caff')}
    if($ext -eq '.webm'){return $bytes[0] -eq 0x1A -and $bytes[1] -eq 0x45 -and $bytes[2] -eq 0xDF -and $bytes[3] -eq 0xA3}
    if($ext -eq '.aac'){return $bytes[0] -eq 255 -and ($bytes[1] -band 0xF6) -eq 0xF0}
    if($ext -ne '.mp3'){return $false}
    $start=0
    if($magic.StartsWith('ID3')){
        if(($bytes[6] -bor $bytes[7] -bor $bytes[8] -bor $bytes[9]) -band 128){return $false}
        $start=10+($bytes[6]*2097152)+($bytes[7]*16384)+($bytes[8]*128)+$bytes[9]
        if(($bytes[5] -band 16) -ne 0){$start+=10}
    }
    $end=[Math]::Min($bytes.Length-4,$start+65536)
    for($i=$start;$i -lt $end;$i++){
        if($bytes[$i] -ne 255 -or ($bytes[$i+1] -band 224) -ne 224){continue}
        $version=($bytes[$i+1] -shr 3) -band 3;$layer=($bytes[$i+1] -shr 1) -band 3
        $rateIndex=($bytes[$i+2] -shr 2) -band 3;$bitIndex=($bytes[$i+2] -shr 4) -band 15
        if($version -eq 1 -or $layer -ne 1 -or $rateIndex -eq 3 -or $bitIndex -eq 0 -or $bitIndex -eq 15){continue}
        $rates=@(44100,48000,32000);$rate=$rates[$rateIndex]
        if($version -eq 2){$rate/=2}elseif($version -eq 0){$rate/=4}
        $bitrates=if($version -eq 3){@(0,32,40,48,56,64,80,96,112,128,160,192,224,256,320)}else{@(0,8,16,24,32,40,48,56,64,80,96,112,128,144,160)}
        $factor=if($version -eq 3){144000}else{72000}
        $length=[int][Math]::Floor($factor*$bitrates[$bitIndex]/$rate)+(($bytes[$i+2] -shr 1) -band 1)
        $next=$i+$length
        if($next+3 -lt $bytes.Length -and $bytes[$next] -eq 255 -and ($bytes[$next+1] -band 254) -eq ($bytes[$i+1] -band 254)){return $true}
    }
    return $false
}
$client=$null;$root=$null;$exitCode=1
try{
    if(!$BaseUri.IsAbsoluteUri -or $BaseUri.UserInfo -or $BaseUri.Query -or $BaseUri.Fragment -or $BaseUri.AbsolutePath -ne '/' -or !(($BaseUri.Scheme -eq 'https' -and $BaseUri.Host -eq 'isokratis-nek.netlify.app') -or ($BaseUri.Scheme -eq 'http' -and $BaseUri.IsLoopback))){throw 'Μη επιτρεπτή διεύθυνση. Χρησιμοποίησε τον επίσημο HTTPS ιστότοπο.'}
    if(!$OutputDirectory){$desktop=[Environment]::GetFolderPath('Desktop');$OutputDirectory=Join-Path $desktop ('Isokratis-Voices-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))}
    $root=[IO.Path]::GetFullPath($OutputDirectory)
    if(Test-Path -LiteralPath $root){throw 'Ο φάκελος υπάρχει ήδη. Χρησιμοποίησε νέο φάκελο για να μη χαθούν προηγούμενα αρχεία.'}
    if(Test-Path -LiteralPath ($root+'.part001.zip')){throw 'Υπάρχει ήδη ZIP με αυτό το όνομα.'}
    [IO.Directory]::CreateDirectory($root)|Out-Null
    $meta=Join-Path $root 'metadata';[IO.Directory]::CreateDirectory($meta)|Out-Null
    $handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false;$handler.AutomaticDecompression=[Net.DecompressionMethods]::GZip -bor [Net.DecompressionMethods]::Deflate
    $client=[Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds(45)
    $client.DefaultRequestHeaders.UserAgent.ParseAdd('ProfessionalEarTrainingStudio-VoiceDownloader/1.0')
    $files=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    $ignored=[Collections.Generic.List[string]]::new();$discoveryErrors=[Collections.Generic.List[string]]::new()
    function Add-Audio([string]$Reference,$Frequency,$SetName){
        try{
            if(!$Reference -or $Reference.Contains('\') -or $Reference.Length -gt 500){return}
            $uri=[Uri]::new($BaseUri,$Reference)
            if($uri.Scheme -ne $BaseUri.Scheme -or $uri.Authority -ne $BaseUri.Authority -or $uri.UserInfo){$ignored.Add($Reference);return}
            $relative=[Uri]::UnescapeDataString($uri.AbsolutePath).TrimStart('/')
            if($relative -notmatch '\.(mp3|wav|flac|ogg|m4a|aac|webm|mp4|caf)$'){return}
            foreach($segment in $relative.Split('/')){
                if(!$segment -or $segment -in @('.','..') -or $segment.Length -gt 220 -or $segment.TrimEnd(' ','.') -ne $segment -or $segment.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $segment -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)'){throw 'Μη ασφαλές όνομα αρχείου.'}
            }
            $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
            if(!$path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Διαδρομή εκτός φακέλου.'}
            if(!$files.ContainsKey($relative)){
                if($files.Count -ge 1000){throw 'Υπέρβαση 1000 αρχείων.'}
                $files.Add($relative,[pscustomobject]@{Path=$relative;Url=$uri.AbsoluteUri;DeclaredRootHz=$Frequency;Set=$SetName})
            }
        }catch{$ignored.Add($Reference)}
    }
    function Download-File([Uri]$Uri,[string]$Path,[long]$Maximum){
        $response=$null;$source=$null;$target=$null;$part=$Path+'.part'
        try{
            $response=$client.GetAsync($Uri,[Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
            if(!$response.IsSuccessStatusCode){throw ('HTTP '+[int]$response.StatusCode)}
            if($response.Content.Headers.ContentLength -gt $Maximum){throw 'Υπερβολικό μέγεθος αρχείου.'}
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null
            $source=$response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();$target=[IO.File]::Create($part)
            $buffer=New-Object byte[] 65536;[long]$total=0
            while($true){$read=$source.ReadAsync($buffer,0,$buffer.Length);if(!$read.Wait(45000)){throw 'Λήξη χρόνου ανάγνωσης.'};$count=$read.GetAwaiter().GetResult();if($count -le 0){break};$total+=$count;if($total -gt $Maximum){throw 'Υπέρβαση ορίου λήψης.'};$target.Write($buffer,0,$count)}
            $target.Dispose();$target=$null;Move-Item -LiteralPath $part -Destination $Path
        }finally{if($target){$target.Dispose()};if($source){$source.Dispose()};if($response){$response.Dispose()};if(Test-Path -LiteralPath $part){Remove-Item -LiteralPath $part -Force}}
    }
    $roots=@{g3=98.0;a3=110.0;b3=123.470825;c4=130.812783;d4=146.832384;e4=164.813778;f4=174.614116;g4=195.997718;a4=220.0}
    foreach($voice in @(1,2,3,5,6)){foreach($note in @('g3','a3','b3','c4','d4','e4','f4','g4','a4')){Add-Audio ('voices_pa3/'+$note+'_ison'+$voice+'.mp3') $roots[$note] ('PA voice '+$voice)}}
    foreach($note in @('g3','a3','b3','c4','d4','e4','f4','g4','a4')){Add-Audio ('voices_mmm/'+$note+'-hum.mp3') $roots[$note] 'Bouche fermee'}
    foreach($sample in @(@('d1',73.42),@('g1',98.0),@('c2',130.81),@('f2',174.61),@('bflat2',233.08))){Add-Audio ('voices_v63/allvoices_'+$sample[0]+'.mp3') $sample[1] 'Core choir'}
    $knownCount=$files.Count
    Write-Host 'Λήψη φωνών Ισοκράτη. Μην κλείσεις το παράθυρο μέχρι να ολοκληρωθεί.'
    Write-Host ('Φάκελος: '+$root)
    if(!$NoDiscovery){
        $sources=[Collections.Generic.List[string]]::new();foreach($name in @('','new-voices.js','voice-calibration.js','voice-playback-policy.js','service-worker.js')){$sources.Add($name)}
        for($index=0;$index -lt $sources.Count -and $index -lt 16;$index++){
            $name=$sources[$index];$sourcePath=Join-Path $meta ('source-'+$index+'.txt')
            try{
                Write-Host ('Αναζήτηση πρόσθετων φωνών: '+$(if($name){$name}else{'αρχική σελίδα'}))
                Download-File ([Uri]::new($BaseUri,$name)) $sourcePath 2097152
                $text=[IO.File]::ReadAllText($sourcePath)
                # Read references as data. No JavaScript, HTML or remote command is executed.
                foreach($match in [regex]::Matches($text,'["''](?<path>[^"''\r\n]{1,300}\.(?:mp3|wav|flac|ogg|m4a|aac|webm|mp4|caf)(?:\?[^"''\r\n]{0,100})?)["'']',[Text.RegularExpressions.RegexOptions]::IgnoreCase)){
                    $ref=$match.Groups['path'].Value
                    if($ref.Contains('/')){Add-Audio $ref $null 'Discovered public reference'}
                }
                foreach($match in [regex]::Matches($text,'\b(?:url|src)\s*:\s*["''](?<path>[^"''\r\n]{1,300}\.(?:mp3|wav|flac|ogg|m4a|aac|webm|mp4|caf))["'']',[Text.RegularExpressions.RegexOptions]::IgnoreCase)){Add-Audio $match.Groups['path'].Value $null 'Discovered public reference'}
                if($name -eq ''){foreach($match in [regex]::Matches($text,'<script[^>]+src=["''](?<path>[^"'']+\.js)["'']',[Text.RegularExpressions.RegexOptions]::IgnoreCase)){
                    $ref=$match.Groups['path'].Value.TrimStart([char[]]'./');if($ref -match '^[a-zA-Z0-9_-]*(voice|sound|cache)[a-zA-Z0-9_-]*\.js$' -and !$sources.Contains($ref)){$sources.Add($ref)}
                }}
            }catch{$discoveryErrors.Add($name+': '+$_.Exception.Message);Write-Host ('Δεν διαβάστηκε ένα αρχείο καταλόγου: '+$name)}
        }
    }
    $plan=@($files.Values|Sort-Object @{Expression={if($_.Set -like 'PA voice *'){0}elseif($_.Set -eq 'Core choir'){1}elseif($_.Set -eq 'Bouche fermee'){2}else{3}}},Path);Save-Json $plan (Join-Path $meta 'DOWNLOAD-PLAN.json')
    $results=[Collections.Generic.List[object]]::new();$failures=0;$consecutive=0;[long]$totalBytes=0;$number=0
    foreach($item in $plan){
        $number++;$path=Join-Path $root $item.Path;$status='Failed';$errorText=$null;$hash=$null;[long]$size=0
        if($consecutive -ge 5 -or $totalBytes -ge 536870912){$status='NotAttempted';$errorText='Η λήψη σταμάτησε μετά από πέντε συνεχόμενες αποτυχίες ή υπέρβαση συνολικού ορίου.'}
        else{
            Write-Host ("Αρχείο $number από $($plan.Count): "+$item.Path)
            for($retry=0;$retry -lt 2;$retry++){
                try{
                    Download-File ([Uri]$item.Url) $path 20971520
                    if(!(Test-Media $path)){Remove-Item -LiteralPath $path -Force;throw 'Το αρχείο δεν έχει αναγνωρίσιμη κεφαλίδα ήχου. Δεν περιλαμβάνεται στο ZIP.'}
                    $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();$size=(Get-Item -LiteralPath $path).Length;$totalBytes+=$size;$status='Downloaded';$consecutive=0;break
                }catch{if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force};$errorText=$_.Exception.Message;if($retry -eq 0){Start-Sleep -Milliseconds 400}}
            }
            if($status -ne 'Downloaded'){$failures++;$consecutive++;Write-Host ('Αποτυχία: '+$item.Path+' — '+$errorText)}
            Start-Sleep -Milliseconds 150
        }
        $results.Add([pscustomobject]@{Path=$item.Path;Url=$item.Url;Status=$status;Bytes=$size;SHA256=$hash;DeclaredRootHz=$item.DeclaredRootHz;Set=$item.Set;Error=$(if($status -eq 'Downloaded'){$null}else{$errorText})})
    }
    $downloaded=@($results|Where-Object Status -eq 'Downloaded').Count;$missing=$plan.Count-$downloaded
    $report=[ordered]@{ToolVersion='1.0.0';Source=$BaseUri.AbsoluteUri;CreatedAt=[DateTimeOffset]::Now.ToString('o');KnownFiles=$knownCount;Planned=$plan.Count;Downloaded=$downloaded;Missing=$missing;AllPlannedDownloaded=($missing -eq 0);DiscoveryErrors=@($discoveryErrors);IgnoredReferences=@($ignored);Coverage='59 known files plus explicit same-origin public audio references. Dynamically constructed additional paths may remain undiscovered.';Validation='Container headers/MPEG frame structure and locally calculated SHA-256, not full audio decoding or listening.';RedistributionPermissionGranted=$false;Files=@($results)}
    Save-Json $report (Join-Path $root 'DOWNLOAD-REPORT.json')
    $results|Export-Csv -LiteralPath (Join-Path $root 'DOWNLOAD-REPORT.csv') -NoTypeInformation -Encoding UTF8
    @('Προσωπική λήψη δημόσιων αρχείων από τον ισοκράτη Nektarios Kakolyris.','Το ZIP δεν αποτελεί άδεια ενσωμάτωσης/αναδιανομής. Διατηρούνται τα δικαιώματα των δημιουργών.','Οι βάσεις Hz είναι δηλώσεις του πηγαίου κώδικα, όχι μέτρηση κουρδίσματος.','Δες το DOWNLOAD-REPORT.json για επιτυχίες, αποτυχίες και όρια κάλυψης.','Τα metadata/source-*.txt είναι δημόσια αρχεία της σελίδας για έλεγχο αναφορών, όχι εκτελέσιμα.','Δεν γίνεται είσοδος σε λογαριασμό, ανάγνωση προσωπικών αρχείων, εγκατάσταση ή τηλεχειρισμός.')|Set-Content -LiteralPath (Join-Path $root 'README-GR.txt') -Encoding UTF8
    # Independent ZIP files stay below the upload limit; MP3 bytes are not converted.
    $archives=[Collections.Generic.List[object]]::new();$archive=$null;$partNumber=0;[long]$partBytes=0
    try{
        foreach($file in @(Get-ChildItem -LiteralPath $root -File -Recurse|Sort-Object FullName)){
            if($null -eq $archive -or $partBytes+$file.Length -gt 20971520){
                if($archive){$archive.Dispose();$archive=$null}
                $partNumber++;$zip=$root+('.part{0:000}.zip' -f $partNumber)
                if(Test-Path -LiteralPath $zip){throw 'Υπάρχει ήδη ZIP. Δεν αντικαθίσταται.'}
                $stream=[IO.File]::Open($zip,[IO.FileMode]::CreateNew)
                $archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
                $archives.Add([pscustomobject]@{Path=$zip;SHA256=$null;Bytes=0});$partBytes=0
            }
            $relative=$file.FullName.Substring($root.Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Fastest)|Out-Null
            $partBytes+=$file.Length
        }
    }finally{if($archive){$archive.Dispose()}}
    foreach($a in $archives){$a.SHA256=(Get-FileHash -LiteralPath $a.Path -Algorithm SHA256).Hash.ToLowerInvariant();$a.Bytes=(Get-Item -LiteralPath $a.Path).Length}
    Save-Json @($archives) ($root+'.archives.json')
    Write-Host '';Write-Host ("Ολοκληρώθηκε: $downloaded από $($plan.Count). Λείπουν: $missing.")
    foreach($a in $archives){Write-Host ('ZIP: '+$a.Path)}
    Write-Host ('Ανέβασε όλα τα '+$archives.Count+' ZIP, όχι τα αρχεία ένα ένα.')
    if($discoveryErrors.Count -gt 0){Write-Host 'Κάποιοι πρόσθετοι κατάλογοι δεν διαβάστηκαν. Η κάλυψη δεν είναι επιβεβαιωμένα πλήρης.'}
    if($OpenFolder -and $env:OS -eq 'Windows_NT'){Start-Process explorer.exe -ArgumentList ('/select,"'+$archives[0].Path+'"')}
    $exitCode=if($missing -eq 0){0}else{2}
}catch{Write-Host ('Η λήψη δεν ολοκληρώθηκε: '+$_.Exception.Message);$exitCode=1}
finally{if($client){$client.Dispose()}}
exit $exitCode
