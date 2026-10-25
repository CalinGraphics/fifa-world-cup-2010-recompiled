# Clicks the launcher's PLAY button through UI Automation.
Add-Type -AssemblyName UIAutomationClient
$p = Get-Process Launcher -ErrorAction SilentlyContinue | Select -First 1
if (-not $p) { "launcher not running"; exit 1 }
$root = [Windows.Automation.AutomationElement]::RootElement
$c = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $root.FindFirst([Windows.Automation.TreeScope]::Children, $c)
$bc = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, "PLAY")
$btn = $win.FindAll([Windows.Automation.TreeScope]::Descendants, $bc) |
  Where-Object { $_.Current.ControlType -eq [Windows.Automation.ControlType]::Button } | Select -First 1
if (-not $btn) { "PLAY not found"; exit 1 }
if (-not $btn.Current.IsEnabled) { "PLAY is disabled"; exit 1 }
$btn.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
"clicked PLAY"
