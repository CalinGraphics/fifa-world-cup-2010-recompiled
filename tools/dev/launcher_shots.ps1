param([string]$Exe = "", [string]$Tag = "launcher")
# Opens the launcher, screenshots every tab (out\dev\<Tag>-<tab>.png) and closes it.
. "$PSScriptRoot\_paths.ps1"
if (-not $Exe) { $Exe = Join-Path $Repo "launcher\bin\Release\net10.0-windows\win-x64\Launcher.exe" }
Add-Type -AssemblyName UIAutomationClient
Get-Process Launcher -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit() }
$p = Start-Process $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
Start-Sleep 4
$root = [Windows.Automation.AutomationElement]::RootElement
$win = $root.FindFirst([Windows.Automation.TreeScope]::Children,
  (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)))
foreach ($tab in "PLAY", "DISPLAY", "GRAPHICS", "CONTROLS", "ABOUT") {
  $c = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $tab)
  $el = $win.FindAll([Windows.Automation.TreeScope]::Descendants, $c) |
    Where-Object { $_.Current.ControlType -eq [Windows.Automation.ControlType]::RadioButton } | Select -First 1
  if ($el) { $el.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
  Start-Sleep -Milliseconds 700
  & "$PSScriptRoot\screenshot.ps1" -Process "Launcher" -Out (Join-Path $Work "$Tag-$($tab.ToLower()).png") | Out-Null
}
$p.Kill(); $p.WaitForExit()
"done"
