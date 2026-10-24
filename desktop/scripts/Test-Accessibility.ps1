param([string]$Exe="",[string]$Report="")
if(!$Exe){
    $repo=Join-Path $PSScriptRoot '../artifacts/win-x64/ProfessionalEarTrainingStudio.exe'
    $installed=Join-Path $PSScriptRoot '../ProfessionalEarTrainingStudio.exe'
    $Exe=if(Test-Path $repo){$repo}else{$installed}
}
if(!$Report){$Report=Join-Path $env:TEMP 'ProfessionalEarTrainingStudio-accessibility-audit.json'}
$ErrorActionPreference='Stop'
try {
$Exe=(Resolve-Path $Exe).Path
# First audit the actual managed AccessibleObjects of every tab and the installer form.
if(Test-Path $Report){Move-Item $Report ($Report+".previous") -Force}
$managed=Start-Process $Exe -ArgumentList @('--accessibility-audit',('"'+$Report+'"')) -PassThru -Wait
if($managed.ExitCode -ne 0 -or !(Test-Path $Report)){throw 'Managed accessibility audit failed.'}
$audit=Get-Content $Report -Raw | ConvertFrom-Json
if($audit.MainForm.Count -lt 40 -or $audit.PianoSetupDialog.Count -lt 8 -or $audit.PresentationRegression.Count -lt 28){throw 'Incomplete form audit.'}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
function Find-Window($id){
    $processCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$id)
    $windowCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Window)
    $visibleCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::IsOffscreenProperty,$false)
    $condition=[System.Windows.Automation.AndCondition]::new($processCondition,$windowCondition,$visibleCondition)
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
$process=Start-Process $Exe -ArgumentList @('--uia-test-mode','--uia-state-path',('"'+$Report+'.live.json"')) -PassThru
try{
    $window=$null
    for($i=0;$i -lt 50 -and !$window;$i++){Start-Sleep -Milliseconds 200;$window=Find-Window $process.Id}
    if(!$window){throw 'No native UIA window found.'}
    $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::TabItem)
    $tabs=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    for($i=0;$i -lt 50 -and $tabs.Count -lt 5;$i++){
        Start-Sleep -Milliseconds 200
        $window=Find-Window $process.Id
        if($window){$tabs=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)}
    }
    if($tabs.Count -lt 5){
        $details=@($window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.ControlType.ProgrammaticName+': '+$_.Current.Name }) -join '; '
        $tabContainer=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Tab))
        $walker=[System.Windows.Automation.TreeWalker]::RawViewWalker
        $last=$walker.GetLastChild($tabContainer)
        $backwards=@();for($j=0;$last -and $j -lt 8;$j++){$backwards+=$last.Current.Name;$last=$walker.GetPreviousSibling($last)}
        $first=$walker.GetFirstChild($tabContainer);$forward=@();for($j=0;$first -and $j -lt 8;$j++){$forward+=$first.Current.Name;$first=$walker.GetNextSibling($first)}
        $childCount=$tabContainer.FindAll([System.Windows.Automation.TreeScope]::Children,$condition).Count
        $client=[System.Windows.Automation.AutomationElement].Assembly.Location
        $directCount=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition).Count
        throw "Client=$client; forward=$($forward -join ','); child count=$childCount; Direct count=$directCount; collection=$($tabs.GetType().FullName); reverse=$($backwards -join ','); Expected five application tabs; found $($tabs.Count) on $($window.Current.Name). Tree: $details"
    }
    $expected=@('Ρυθμίσεις','Εξάσκηση','Πρόοδος','Εξετάσεις και προσαρμογή','Οδηγίες')
    if($tabs.Count -ne $expected.Count){throw 'Unexpected tab count.'}
    for($i=0;$i -lt $expected.Count;$i++){if($tabs[$i].Current.Name -ne $expected[$i]){throw "Wrong tab order/name at $i"}}
    if(@($tabs | ForEach-Object {$_.Current.AutomationId} | Select-Object -Unique).Count -ne 5){throw 'Tab automation identifiers are not unique.'}
    foreach($tab in $tabs){
        $pattern=$tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $pattern.Select();Start-Sleep -Milliseconds 100
        Check-Controls $window $tab.Current.Name
        $sentinels=@{'Ρυθμίσεις'='Εγκατάσταση ή επισκευή πραγματικού πιάνου Maestro Concert Grand';'Εξάσκηση'='Οδηγία τρέχουσας άσκησης';'Πρόοδος'='Στατιστικά και ιστορικό';'Εξετάσεις και προσαρμογή'='Εκπαιδευτικό εξεταστικό προφίλ';'Οδηγίες'='Οδηγίες και περιορισμοί έκδοσης'}
        $sentinel=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$sentinels[$tab.Current.Name]))
        if(!$sentinel){throw "Selected page content is missing: $($tab.Current.Name)"}
    }
    $keyboard=New-Object -ComObject WScript.Shell
    if(!$keyboard.AppActivate($process.Id)){throw 'Cannot activate application for keyboard tests.'}
    $tabs[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $tabs[0].SetFocus()
    $keyboardChecks=@()
    for($i=1;$i -le 5;$i++){
        $keyboard.SendKeys('^{TAB}');Start-Sleep -Milliseconds 150
        $wanted=$i%5
        if(!$tabs[$wanted].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected){throw "Ctrl+Tab did not select $($expected[$wanted])"}
        $keyboardChecks+="Ctrl+Tab: $($expected[$wanted])"
    }
    $keyboard.SendKeys('^+{TAB}');Start-Sleep -Milliseconds 150
    if(!$tabs[4].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected){throw 'Ctrl+Shift+Tab failed.'}
    $keyboard.SendKeys('{F1}');Start-Sleep -Milliseconds 150
    $focused=[System.Windows.Automation.AutomationElement]::FocusedElement
    if($focused.Current.ProcessId -ne $process.Id -or $focused.Current.Name -ne 'Οδηγίες και περιορισμοί έκδοσης'){throw 'F1 did not focus the help text.'}
    $keyboardChecks+='F1: help text focused'
    foreach($tab in $tabs){
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$tab.SetFocus()
        foreach($key in @('{TAB}','+{TAB}')){
            $keyboard.SendKeys($key);Start-Sleep -Milliseconds 100
            $focus=[System.Windows.Automation.AutomationElement]::FocusedElement
            if($focus.Current.ProcessId -ne $process.Id -or [string]::IsNullOrWhiteSpace($focus.Current.Name)){throw "Keyboard focus escaped or became unnamed on $($tab.Current.Name)"}
            $keyboardChecks+="$($tab.Current.Name): $key : $($focus.Current.Name)"
        }
    }
    $audit | Add-Member -NotePropertyName KeyboardChecks -NotePropertyValue $keyboardChecks -Force
    $audit | ConvertTo-Json -Depth 30 | Set-Content $Report -Encoding utf8
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

$audit=Get-Content $Report -Raw | ConvertFrom-Json
$audit | Add-Member -NotePropertyName UIAutomationPassed -NotePropertyValue $true -Force
$audit | ConvertTo-Json -Depth 30 | Set-Content $Report -Encoding utf8
} catch {
    if(Test-Path $Report){
        $audit=Get-Content $Report -Raw | ConvertFrom-Json
        $audit | Add-Member -NotePropertyName UIAutomationPassed -NotePropertyValue $false -Force
        $audit | Add-Member -NotePropertyName UIAutomationError -NotePropertyValue $_.Exception.Message -Force
        $audit | ConvertTo-Json -Depth 30 | Set-Content $Report -Encoding utf8
    }
    throw
}
