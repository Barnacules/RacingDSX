#Requires -Version 5.1
<#
.SYNOPSIS
    Local build script for RacingDSX — Windows 11 x64.

.DESCRIPTION
    Verifies prerequisites, restores NuGet packages, then publishes a
    self-contained, single-file Release binary for win-x64.

    Output lands in:  .\build\win-x64\RacingDSX.exe

.PARAMETER Configuration
    Build configuration.  Default: Release.
    Use -Configuration Debug for a debug build (not self-contained).

.PARAMETER NoPause
    Skip the "Press any key" prompt at the end.  Useful when calling
    this script from another script or CI environment.

.EXAMPLE
    # Standard release build
    .\build.ps1

.EXAMPLE
    # Debug build (faster compile, no single-file bundling)
    .\build.ps1 -Configuration Debug

.EXAMPLE
    # Release build, no prompt at the end
    .\build.ps1 -NoPause
#>

[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Helper functions ──────────────────────────────────────────────────────────

function Write-Header([string]$Text) {
    $bar = '─' * 60
    Write-Host ""
    Write-Host $bar -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host $bar -ForegroundColor Cyan
}

function Write-Step([string]$Text) {
    Write-Host "  >> $Text" -ForegroundColor Yellow
}

function Write-OK([string]$Text) {
    Write-Host "  OK  $Text" -ForegroundColor Green
}

function Write-Fail([string]$Text) {
    Write-Host "  !! $Text" -ForegroundColor Red
}

# ── Script root ───────────────────────────────────────────────────────────────

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectFile = Join-Path $ScriptDir 'RacingDSX.csproj'
$OutputDir  = Join-Path $ScriptDir "build\win-x64"

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Header "RacingDSX — Local Build Script"
Write-Host "  Configuration : $Configuration"
Write-Host "  Output        : $OutputDir"
Write-Host "  Script dir    : $ScriptDir"

# ── 1. Check .NET SDK ─────────────────────────────────────────────────────────

Write-Header "Step 1 / 4 — Checking prerequisites"

Write-Step "Looking for dotnet CLI..."
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Fail "dotnet CLI not found on PATH."
    Write-Host ""
    Write-Host "  Install .NET 8 SDK (or newer) from:" -ForegroundColor White
    Write-Host "  https://dotnet.microsoft.com/en-us/download/dotnet/8.0" -ForegroundColor White
    Write-Host ""
    if (-not $NoPause) { Read-Host "Press Enter to exit" }
    exit 1
}

$sdkVersion = dotnet --version 2>&1
Write-OK "dotnet $sdkVersion found at: $($dotnetCmd.Source)"

# Require at least .NET 8
$major = [int]($sdkVersion -replace '\..*','')
if ($major -lt 8) {
    Write-Fail "RacingDSX requires .NET SDK 8.0 or later (found $sdkVersion)."
    Write-Host "  Download: https://dotnet.microsoft.com/en-us/download/dotnet/8.0" -ForegroundColor White
    if (-not $NoPause) { Read-Host "Press Enter to exit" }
    exit 1
}

# Check project file exists
Write-Step "Locating project file..."
if (-not (Test-Path -LiteralPath $ProjectFile)) {
    Write-Fail "Project file not found: $ProjectFile"
    Write-Host "  Make sure you are running this script from the repository root." -ForegroundColor White
    if (-not $NoPause) { Read-Host "Press Enter to exit" }
    exit 1
}
Write-OK "Project file found."

# ── 2. Restore NuGet packages ─────────────────────────────────────────────────

Write-Header "Step 2 / 4 — Restoring NuGet packages"
Write-Step "Running: dotnet restore"
Write-Host ""

dotnet restore $ProjectFile
if ($LASTEXITCODE -ne 0) {
    Write-Fail "dotnet restore failed (exit code $LASTEXITCODE)."
    if (-not $NoPause) { Read-Host "Press Enter to exit" }
    exit $LASTEXITCODE
}
Write-OK "Restore complete."

# ── 3. Publish ────────────────────────────────────────────────────────────────

Write-Header "Step 3 / 4 — Building ($Configuration)"

# Clean the output folder so we always get a fresh build
if (Test-Path -LiteralPath $OutputDir) {
    Write-Step "Cleaning previous output at $OutputDir ..."
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

if ($Configuration -eq 'Release') {
    # Self-contained, single-file exe — mirrors what the GitHub CI produces
    Write-Step "Running: dotnet publish -c Release -r win-x64 (self-contained, single-file)"
    Write-Host ""

    dotnet publish $ProjectFile `
        -c Release `
        -r win-x64 `
        -p:PublishSingleFile=true `
        -p:SelfContained=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $OutputDir
} else {
    # Debug build — framework-dependent, faster to compile, easier to attach a debugger
    Write-Step "Running: dotnet build -c Debug (framework-dependent)"
    Write-Host ""

    dotnet build $ProjectFile `
        -c Debug `
        -r win-x64 `
        --output $OutputDir
}

if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build failed (exit code $LASTEXITCODE)."
    if (-not $NoPause) { Read-Host "Press Enter to exit" }
    exit $LASTEXITCODE
}

# ── 4. Summary ────────────────────────────────────────────────────────────────

Write-Header "Step 4 / 4 — Build Summary"

$exe = Join-Path $OutputDir 'RacingDSX.exe'
if (Test-Path -LiteralPath $exe) {
    $sizeMB = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-OK "RacingDSX.exe — $sizeMB MB"
    Write-Host ""
    Write-Host "  Output folder: $OutputDir" -ForegroundColor White
    Write-Host ""
    Write-Host "  To run the app now:" -ForegroundColor White
    Write-Host "    & `"$exe`"" -ForegroundColor Cyan
} else {
    Write-Fail "RacingDSX.exe was not found in the output directory."
    Write-Host "  Check the build output above for errors." -ForegroundColor White
    if (-not $NoPause) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Host ""
Write-Host "  Build SUCCEEDED" -ForegroundColor Green
Write-Host ""

if (-not $NoPause) {
    Read-Host "Press Enter to exit"
}
