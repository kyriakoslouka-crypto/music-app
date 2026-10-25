param([string]$Exe="",[string]$Report="",[string]$NvdaLog="")
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
$managed=Start-Process $Exe -ArgumentList @('--accessibility-audit',('"'+$Report+'"')) -PassThru
if(!$managed.WaitForExit(45000)){$managed.Kill($true);throw 'Managed accessibility audit timed out.'}
if($managed.ExitCode -ne 0 -or !(Test-Path $Report)){throw 'Managed accessibility audit failed.'}
$audit=Get-Content $Report -Raw | ConvertFrom-Json
if($audit.MainForm.Count -lt 40 -or $audit.PianoSetupDialog.Count -lt 8 -or $audit.PresentationRegression.Count -lt 28 -or $audit.ExamBoundaryRegression.Count -ne 6){throw 'Incomplete form audit.'}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class PetsInputWindow {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public Payload Data; }
 [StructLayout(LayoutKind.Explicit)] struct Payload { [FieldOffset(0)] public Keyboard Key; [FieldOffset(0)] public Mouse Mouse; }
 [StructLayout(LayoutKind.Sequential)] struct Keyboard { public ushort Vk,Scan; public uint Flags,Time; public UIntPtr Extra; }
 [StructLayout(LayoutKind.Sequential)] struct Mouse { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] inputs,int size);
 [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint mapType);
 static Keyboard NativeKey(ushort key,bool up)=>new Keyboard {Vk=key,Scan=(ushort)MapVirtualKey(key,0),Flags=(up?2u:0u)|((key>=0x21&&key<=0x28||key==0x2D||key==0x2E)?1u:0u)};
 public static void Key(ushort vk){Key(vk,false);}
 public static void Key(ushort vk,bool shift) {
  var inputs=new System.Collections.Generic.List<Input>();
  Action<ushort,bool> add=(key,up)=>inputs.Add(new Input {Type=1,Data=new Payload {Key=NativeKey(key,up)}});
  if(shift)add(0x10,false);add(vk,false);add(vk,true);if(shift)add(0x10,true);
  var batch=inputs.ToArray();if(SendInput((uint)batch.Length,batch,Marshal.SizeOf<Input>())!=batch.Length)
   throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
 }
 public static void TabChord(bool shift) {
  // Submit the complete chord, including modifier releases, in one native input batch.
  // WScript.SendKeys can return before modifier transitions are delivered on a busy CI desktop.
  var inputs=new System.Collections.Generic.List<Input>();
  Action<ushort,bool> add=(vk,up)=>inputs.Add(new Input {Type=1,Data=new Payload {Key=NativeKey(vk,up)}});
  add(0x11,false);if(shift)add(0x10,false);add(0x09,false);add(0x09,true);if(shift)add(0x10,true);add(0x11,true);
  var batch=inputs.ToArray();if(SendInput((uint)batch.Length,batch,Marshal.SizeOf<Input>())!=batch.Length)
   throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
 }
}
'@
function Wait-Foreground($targetWindow){
    for($attempt=0;$attempt -lt 50;$attempt++){
        if([PetsInputWindow]::GetForegroundWindow() -eq [System.IntPtr]::new([long]$targetWindow.Current.NativeWindowHandle)){return}
        Start-Sleep -Milliseconds 40
    }
    throw 'Application did not become the foreground keyboard input window.'
}
function Nvda-LogText {
    if(Test-Path $NvdaLog){return [string](Get-Content $NvdaLog -Raw -Encoding utf8)}
    return ''
}
function Wait-NvdaAnnouncement([string]$name,[int]$offset){
    for($attempt=0;$attempt -lt 100;$attempt++){
        $text=Nvda-LogText
        if($text.Length -ge $offset){
            $line=$text.Substring($offset).Split("`n") | Where-Object { $_.Contains('Speaking ') -and $_.Contains($name) } | Select-Object -First 1
            if($line){return [pscustomobject]@{Name=$name;Passed=$true;SpeechPipelineEvidence=$line.Trim()}}
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Actual NVDA did not produce a fresh speech announcement for $name"
}
function Wait-AccessibleValue($pattern,[string]$value,[bool]$changed=$false){
    for($attempt=0;$attempt -lt 50;$attempt++){
        $same=$pattern.Current.Value -eq $value
        if(($changed -and !$same) -or (!$changed -and $same)){return}
        Start-Sleep -Milliseconds 100
    }
    throw "Keyboard value transition did not complete; previous/expected=$value; actual=$($pattern.Current.Value); changed=$changed"
}
function Wait-SelectedTab($tab){
    for($attempt=0;$attempt -lt 40;$attempt++){
        if($tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected){return}
        Start-Sleep -Milliseconds 50
    }
    throw "Keyboard did not select $($tab.Current.Name)"
}
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
function Wait-ControlFocus($target){
    for($attempt=0;$attempt -lt 40;$attempt++){
        $node=[System.Windows.Automation.AutomationElement]::FocusedElement
        for($depth=0;$node -and $depth -lt 10;$depth++){
            if($node.Equals($target)){return}
            $node=[System.Windows.Automation.TreeWalker]::RawViewWalker.GetParent($node)
        }
        Start-Sleep -Milliseconds 50
    }
    $actual=[System.Windows.Automation.AutomationElement]::FocusedElement
    throw "Focus did not reach $($target.Current.Name); actual=$($actual.Current.Name), type=$($actual.Current.ControlType.ProgrammaticName)"
}
function Focus-ByKeyboard($tab,$target){
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$tab.SetFocus();Wait-Foreground $window;Wait-ControlFocus $tab
    for($attempt=0;$attempt -lt 100;$attempt++){
        $node=[System.Windows.Automation.AutomationElement]::FocusedElement
        for($depth=0;$node -and $depth -lt 10;$depth++){
            if($node.Equals($target)){return}
            $node=[System.Windows.Automation.TreeWalker]::RawViewWalker.GetParent($node)
        }
        [PetsInputWindow]::Key(0x09);Start-Sleep -Milliseconds 50
    }
    throw "Tab traversal did not reach $($target.Current.Name)"
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
    $tabs[0].SetFocus();Wait-Foreground $window;Wait-ControlFocus $tabs[0]
    $keyboardChecks=@();$nvdaChecks=@()
    for($i=1;$i -le 5;$i++){
        $speechOffset=if($NvdaLog){(Nvda-LogText).Length}else{0}
        Wait-Foreground $window;[PetsInputWindow]::TabChord($false);Start-Sleep -Milliseconds 150
        $wanted=$i%5
        Wait-SelectedTab $tabs[$wanted]
        if($NvdaLog){$nvdaChecks+=Wait-NvdaAnnouncement $expected[$wanted] $speechOffset}
        $keyboardChecks+="Ctrl+Tab: $($expected[$wanted])"
    }
    Wait-Foreground $window;[PetsInputWindow]::TabChord($true);Start-Sleep -Milliseconds 150
    Wait-SelectedTab $tabs[4]
    $keyboardChecks+='Ctrl+Shift+Tab: Οδηγίες'
    [PetsInputWindow]::Key(0x70);Start-Sleep -Milliseconds 150
    $focused=[System.Windows.Automation.AutomationElement]::FocusedElement
    if($focused.Current.ProcessId -ne $process.Id -or $focused.Current.Name -ne 'Οδηγίες και περιορισμοί έκδοσης'){throw 'F1 did not focus the help text.'}
    $keyboardChecks+='F1: help text focused'
    [PetsInputWindow]::Key(0x75)
    $audioStatus=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Κατάσταση και αποτέλεσμα'))
    Wait-ControlFocus $audioStatus
    $keyboardChecks+='F6: full audio diagnostics field focused without modal dialog'

    foreach($tab in $tabs){
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$tab.SetFocus()
        foreach($key in @('{TAB}','+{TAB}')){
            [PetsInputWindow]::Key(0x09,($key -eq '+{TAB}'));Start-Sleep -Milliseconds 100
            $focus=[System.Windows.Automation.AutomationElement]::FocusedElement
            if($focus.Current.ProcessId -ne $process.Id -or [string]::IsNullOrWhiteSpace($focus.Current.Name)){throw "Keyboard focus escaped or became unnamed on $($tab.Current.Name)"}
            $keyboardChecks+="$($tab.Current.Name): $key : $($focus.Current.Name)"
        }
    }
    $tabs[3].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $profile=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Εκπαιδευτικό εξεταστικό προφίλ'))
    $profileValue=$profile.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $beforeProfile=$profileValue.Current.Value;Focus-ByKeyboard $tabs[3] $profile;[PetsInputWindow]::Key(0x28);Wait-AccessibleValue $profileValue $beforeProfile $true
    if($profileValue.Current.Value -eq $beforeProfile){throw "Keyboard Down did not change exam profile value; before=$beforeProfile; after=$($profileValue.Current.Value); focused=$([System.Windows.Automation.AutomationElement]::FocusedElement.Current.Name)"}
    [PetsInputWindow]::Key(0x26);Wait-AccessibleValue $profileValue $beforeProfile
    if($profileValue.Current.Value -ne $beforeProfile){throw 'Keyboard Up did not restore the exam profile value.'}
    $keyboardChecks+='Exam profile: Down/Up changes and restores accessible selected value'
    $tabs[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $speedCondition=[System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Ταχύτητα BPM'),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Spinner))
    $speed=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$speedCondition)
    $speedEdit=$speed.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit))
    if(!$speedEdit){throw 'Numeric tempo edit is not exposed.'}
    $speedValue=$speedEdit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $beforeSpeed=[int]$speedValue.Current.Value;Focus-ByKeyboard $tabs[0] $speedEdit
    $delta=if($beforeSpeed -lt 240){1}else{-1}
    $changeKey=if($delta -eq 1){'{UP}'}else{'{DOWN}'};$restoreKey=if($delta -eq 1){'{DOWN}'}else{'{UP}'}
    [PetsInputWindow]::Key($(if($delta -eq 1){0x26}else{0x28}));Wait-AccessibleValue $speedValue ([string]($beforeSpeed+$delta))
    if([int]$speedValue.Current.Value -ne ($beforeSpeed+$delta)){throw 'Keyboard arrow did not change the accessible tempo edit value.'}
    [PetsInputWindow]::Key($(if($delta -eq 1){0x28}else{0x26}));Wait-AccessibleValue $speedValue ([string]$beforeSpeed)
    if([int]$speedValue.Current.Value -ne $beforeSpeed){throw 'Keyboard arrow did not restore the accessible tempo edit value.'}
    $keyboardChecks+='Tempo: Up/Down changes and restores accessible edit value' 
    if($NvdaLog){$audit | Add-Member -NotePropertyName NvdaSpeechChecks -NotePropertyValue $nvdaChecks -Force}
    $audit | Add-Member -NotePropertyName KeyboardChecks -NotePropertyValue $keyboardChecks -Force
    $audit | ConvertTo-Json -Depth 30 | Set-Content $Report -Encoding utf8
    $tabs[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $buttonCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Εγκατάσταση ή επισκευή πραγματικού πιάνου Maestro Concert Grand')
    $button=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$buttonCondition)
    if(!$button){throw 'Piano installation button not exposed.'}
    $invokePattern=$null
    if(!$button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern,[ref]$invokePattern)){throw 'Piano installation button lacks InvokePattern.'}
    Focus-ByKeyboard $tabs[0] $button;[PetsInputWindow]::Key(0x0D)
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
}finally{if(!$process.HasExited){$process.CloseMainWindow()|Out-Null;if(!$process.WaitForExit(3000)){$process.Kill()}}}

$audit=Get-Content $Report -Raw | ConvertFrom-Json
$audit | Add-Member -NotePropertyName UIAutomationPassed -NotePropertyValue $true -Force
$audit | ConvertTo-Json -Depth 30 | Set-Content $Report -Encoding utf8
} catch {
    if(Test-Path $Report){
        $audit=Get-Content $Report -Raw | ConvertFrom-Json
        if($nvdaChecks){$audit | Add-Member -NotePropertyName NvdaSpeechChecks -NotePropertyValue $nvdaChecks -Force}
        if($keyboardChecks){$audit | Add-Member -NotePropertyName KeyboardChecks -NotePropertyValue $keyboardChecks -Force}
        $audit | Add-Member -NotePropertyName UIAutomationPassed -NotePropertyValue $false -Force
        $audit | Add-Member -NotePropertyName UIAutomationError -NotePropertyValue $_.Exception.Message -Force
        $audit | ConvertTo-Json -Depth 30 | Set-Content $Report -Encoding utf8
    }
    throw
}
