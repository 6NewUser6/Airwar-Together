[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$statePath = Join-Path $workspacePath '.tools\run\processes.json'
if (-not (Test-Path -LiteralPath $statePath)) {
    Write-Host 'No recorded Godot MCP session.'
    return
}
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
$editor = Get-CimInstance Win32_Process -Filter "ProcessId = $($state.editorPid)" -ErrorAction SilentlyContinue
if ($editor -and $editor.ExecutablePath -eq $state.editorPath -and $editor.CommandLine.Contains($state.projectPath)) {
    $process = Get-Process -Id $state.editorPid
    # Compatibility with sessions started through Godot's separate console launcher.
    if ($editor.ExecutablePath.EndsWith('_console.exe', [StringComparison]::OrdinalIgnoreCase)) {
        $directEditorPath = $editor.ExecutablePath.Substring(0, $editor.ExecutablePath.Length - '_console.exe'.Length) + '.exe'
        $child = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($state.editorPid)" |
            Where-Object { $_.ExecutablePath -eq $directEditorPath -and $_.CommandLine.Contains($state.projectPath) } |
            Select-Object -First 1
        if ($child) { $process = Get-Process -Id $child.ProcessId }
    }
    if ($state.headless) {
        Stop-Process -Id $process.Id
    } elseif ($process.CloseMainWindow()) {
        if (-not $process.WaitForExit(10000)) {
            throw 'Save your work and close Godot, then run this script again.'
        }
    } else {
        throw 'Save your work and close Godot, then run this script again.'
    }
}
$server = Get-CimInstance Win32_Process -Filter "ProcessId = $($state.serverPid)" -ErrorAction SilentlyContinue
if ($server -and $server.ExecutablePath -eq $state.serverPath) {
    Stop-Process -Id $state.serverPid
}
Remove-Item -LiteralPath $statePath
Write-Host 'Godot MCP session stopped.'
