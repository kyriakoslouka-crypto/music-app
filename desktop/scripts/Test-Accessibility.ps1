param([string]$Exe="",[string]$Report="")
if(!$Exe){
    $repo=Join-Path $PSScriptRoot '../artifacts/win-x64/ProfessionalEarTrainingStudio.exe'
    $installed=Join-Path $PSScriptRoot '../ProfessionalEarTrainingStudio.exe'
    $Exe=if(Test-Path $repo){$repo}else{$installed}
}
if(!$Report){$Report=Join-Path $env:TEMP 'ProfessionalEarTrainingStudio-accessibility-audit.json'}
$ErrorActionPreference='Stop'
$Exe=(Resolve-Path $Exe).Path
# First audit the actual managed AccessibleObjects of every tab and the installer form.
if(Test-Path $Report){Move-Item $Report ($Report+".previous") -Force}
$managed=Start-Process $Exe -ArgumentList @('--accessibility-audit',('"'+$Report+'"')) -PassThru -Wait
if($managed.ExitCode -ne 0 -or !(Test-Path $Report)){throw 'Managed accessibility audit failed.'}
$audit=Get-Content $Report -Raw | ConvertFrom-Json
if($audit.MainForm.Count -lt 40 -or $audit.PianoSetupDialog.Count -lt 8){throw 'Incomplete form audit.'}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
function Find-Window($id){
    $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$id)
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
}
function Check-Controls($window,$label){
    $all=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    $named=0
    foreach($c in $all){
        if($c.Current.IsKeyboardFocusable -and !$c.Current.IsOffscreen){
            if([string]::IsNullOrWhiteSpace($c.Current.Name)){throw "Unnamed focusable UIA control on $label : $($c.Current.ControlType.ProgrammaticName)"}
            $named++
        }
    }
    if($named -lt 1){throw "No accessible controls: $label"}
    Write-Output "PASS UIA $label : $named named focusable controls"
}
$process=Start-Process $Exe -ArgumentList '--uia-test-mode' -PassThru
try{
    $window=$null
    for($i=0;$i -lt 50 -and !$window;$i++){Start-Sleep -Milliseconds 200;$window=Find-Window $process.Id}
    if(!$window){throw 'No native UIA window found.'}
    $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::TabItem)
    $tabs=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if($tabs.Count -lt 5){throw 'Expected all five application tabs including examinations.'}
    foreach($tab in $tabs){
        $pattern=$tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $pattern.Select();Start-Sleep -Milliseconds 100
        Check-Controls $window $tab.Current.Name
    }
    $tabs[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $buttonCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Εγκατάσταση ή επισκευή πραγματικού πιάνου Maestro Concert Grand')
    $button=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$buttonCondition)
    if(!$button){throw 'Piano installation button not exposed.'}
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 300
    $title=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Εγκατάσταση πραγματικού πιάνου')
    $dialog=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$title)
    if(!$dialog){
        $dialog=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$title)
    }
    if(!$dialog){throw 'Piano setup dialog not exposed.'}
    Check-Controls $dialog 'PianoSetupDialog'
    $dialog.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Write-Output 'PASS all application tabs and piano installer UIA coverage. No download was started.'
    Write-Output 'NOT TESTED: NVDA speech, physical keyboard traversal, Windows FLAC codec, audio hardware or listening quality.'
}finally{if(!$process.HasExited){$process.CloseMainWindow()|Out-Null}}
