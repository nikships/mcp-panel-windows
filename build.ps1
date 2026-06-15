$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "WindowsMcpPanel\Program.cs"
$dist = Join-Path $root "dist"
$output = Join-Path $dist "McpPanelWindows.exe"
$windowsRoot = if ($env:WINDIR) { $env:WINDIR } elseif ($env:SystemRoot) { $env:SystemRoot } else { "C:\Windows" }
$compiler = Join-Path $windowsRoot "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $compiler)) {
    throw "C# compiler not found at $compiler"
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:x64 `
    /out:$output `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Web.Extensions.dll `
    $source

Write-Host "Built $output"
