param([Parameter(Mandatory)][string]$Target,[Parameter(Mandatory)][string]$Branch)
$ErrorActionPreference='Stop'
if($Target -notmatch '^downloads/windows-checks/[a-zA-Z0-9-]+$' -or $Branch -notmatch '^codex/pets-[a-zA-Z0-9-]+$'){throw 'Unexpected diagnostic target or branch.'}
if(!(Test-Path $Target)){throw 'Diagnostic folder missing.'}
if(Get-ChildItem $Target -File -Recurse | Where-Object {$_.Extension -notin '.json','.txt'}){throw 'Only bounded diagnostic JSON/text may be published.'}
$previousIndex=$env:GIT_INDEX_FILE
$temporaryIndex=Join-Path $env:RUNNER_TEMP ('pets-diagnostics-index-'+[guid]::NewGuid().ToString('N'))
function Invoke-CheckedGit { & git @args; if($LASTEXITCODE -ne 0){throw "Diagnostic git operation failed: $($args[0])"} }
try{
    # A new root tree contains reports only. Never re-publish a workflow snapshot with a
    # contents-only GITHUB_TOKEN, and never mutate the checkout's normal index or HEAD.
    $env:GIT_INDEX_FILE=$temporaryIndex
    Invoke-CheckedGit config user.name 'PETS Windows checks'
    Invoke-CheckedGit config user.email 'pets-build@users.noreply.github.com'
    Invoke-CheckedGit read-tree --empty
    Invoke-CheckedGit add -- $Target
    $tree=Invoke-CheckedGit write-tree
    $commit=Invoke-CheckedGit commit-tree $tree -m "Record isolated Windows checks: $Branch"
    Invoke-CheckedGit push origin "${commit}:refs/heads/$Branch"
}finally{
    if($null -eq $previousIndex){Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue}else{$env:GIT_INDEX_FILE=$previousIndex}
    Remove-Item $temporaryIndex -ErrorAction SilentlyContinue
}
