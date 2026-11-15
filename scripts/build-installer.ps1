# Build on Windows using .NET 10 SDK and Inno Setup 6.
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '..'))

Write-Host '[1/2] Publishing self-contained Windows x64 executable...'
dotnet publish '.\src\GameBarCpuProfile.Service\GameBarCpuProfile.Service.csproj' `
    -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o '.\publish'
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

Write-Host '[2/2] Compiling Inno Setup installer...'
$iscc = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (-not $iscc) {
    foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )) {
        if (Test-Path -LiteralPath $candidate) { $iscc = $candidate; break }
    }
}
if (-not $iscc) { throw 'ISCC.exe not found. Install Inno Setup 6.' }
& $iscc '.\installer\GameBarCpuProfile.iss'
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed: $LASTEXITCODE" }
Write-Host 'Done. Installer is in .\installer\Output\'
