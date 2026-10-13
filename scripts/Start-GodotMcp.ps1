[CmdletBinding()]
param(
    [string]$EditorPath = 'D:\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe',
    [switch]$Headless
)

$ErrorActionPreference = 'Stop'
# The console executable is a launcher; track the editor itself for clean shutdown.
if ($EditorPath.EndsWith('_console.exe', [StringComparison]::OrdinalIgnoreCase)) {
    $directEditorPath = $EditorPath.Substring(0, $EditorPath.Length - '_console.exe'.Length) + '.exe'
    if (Test-Path -LiteralPath $directEditorPath) { $EditorPath = $directEditorPath }
}
$workspacePath = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspacePath 'airwar-together'
$serverDirectory = Join-Path $workspacePath '.tools\mcp-server\9.2.10'
$serverPath = Join-Path $serverDirectory 'gamedev-mcp-server.exe'
$runDirectory = Join-Path $workspacePath '.tools\run'
$statePath = Join-Path $runDirectory 'processes.json'

foreach ($requiredPath in @($EditorPath, $serverPath, (Join-Path $projectPath 'project.godot'))) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing required file: $requiredPath"
    }
}

New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$envPath = Join-Path $projectPath '.env'
if (-not (Test-Path -LiteralPath $envPath)) {
    Copy-Item -LiteralPath (Join-Path $projectPath '.env.example') -Destination $envPath
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet build (Join-Path $projectPath 'airwar-together.csproj') --configuration Debug --nologo --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Godot C# project build failed.' }

$serverProcess = $null
$listener = Get-NetTCPConnection -LocalPort 5300 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    $serverProcess = Get-Process -Id $listener.OwningProcess
    if ($serverProcess.Path -ne $serverPath) {
        throw "Port 5300 is occupied by another application (PID $($serverProcess.Id))."
    }
} else {
    $serverProcess = Start-Process -FilePath $serverPath `
        -ArgumentList @('--client-transport', 'streamableHttp', '--port', '5300', '--bind', '127.0.0.1', '--auth', 'none') `
        -WorkingDirectory $serverDirectory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runDirectory 'server.stdout.log') `
        -RedirectStandardError (Join-Path $runDirectory 'server.stderr.log')
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $serverProcess.Refresh()
        if ($serverProcess.HasExited) { throw 'MCP server exited. Check .tools\run\server.stderr.log.' }
        $listener = Get-NetTCPConnection -LocalPort 5300 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($listener) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $listener -or $listener.OwningProcess -ne $serverProcess.Id) {
        throw 'MCP server did not start listening on port 5300.'
    }
}

$editorProcess = $null
if (Test-Path -LiteralPath $statePath) {
    $previousState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $candidate = Get-CimInstance Win32_Process -Filter "ProcessId = $($previousState.editorPid)" -ErrorAction SilentlyContinue
    if ($candidate -and $candidate.ExecutablePath -eq $EditorPath -and $candidate.CommandLine.Contains($projectPath)) {
        if ([bool]$previousState.headless -ne [bool]$Headless) {
            throw 'The editor is already running in a different mode. Run Stop-GodotMcp.ps1 first.'
        }
        $editorProcess = Get-Process -Id $candidate.ProcessId
    }
}

if (-not $editorProcess) {
    $editorArguments = @('--editor', '--path', ('"' + $projectPath + '"'))
    $windowStyle = 'Normal'
    if ($Headless) {
        $editorArguments += '--headless'
        $windowStyle = 'Hidden'
    }
    $editorProcess = Start-Process -FilePath $EditorPath -ArgumentList $editorArguments `
        -WorkingDirectory $projectPath -WindowStyle $windowStyle -PassThru `
        -RedirectStandardOutput (Join-Path $runDirectory 'editor.stdout.log') `
        -RedirectStandardError (Join-Path $runDirectory 'editor.stderr.log')
}

@{
    serverPid = $serverProcess.Id
    serverPath = $serverPath
    editorPid = $editorProcess.Id
    editorPath = $EditorPath
    projectPath = $projectPath
    headless = [bool]$Headless
} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8

Write-Host "Godot editor PID: $($editorProcess.Id)"
Write-Host "MCP server PID: $($serverProcess.Id)"
Write-Host 'Codex MCP endpoint: http://127.0.0.1:5300/mcp'
