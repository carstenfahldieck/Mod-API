param(
    [string]$Downloads = "$env:USERPROFILE\Downloads",
    [string]$Source = "G:\CS1_DEV\LaneBalancer",
    [string]$BuildBat = "G:\CS1_DEV\LaneBalancer\Zwei_A11_build_all_cs.bat"
)

$ErrorActionPreference = "Stop"

function Info($msg)  { Write-Host "[INFO]  $msg" -ForegroundColor Cyan }
function Good($msg)  { Write-Host "[ OK ]  $msg" -ForegroundColor Green }
function Warn($msg)  { Write-Host "[WARN]  $msg" -ForegroundColor Yellow }
function Fail($msg)  { Write-Host "[FAIL]  $msg" -ForegroundColor Red }

try {
    Write-Host "==========================================" -ForegroundColor White
    Write-Host "  CS1 LaneBalancer - UPDATE + BUILD" -ForegroundColor White
    Write-Host "==========================================" -ForegroundColor White
    Write-Host ("Downloads : ""{0}""" -f $Downloads)
    Write-Host ("Source    : ""{0}""" -f $Source)
    Write-Host ("BuildBat  : ""{0}""" -f $BuildBat)
    Write-Host ""

    if (-not (Test-Path $Downloads)) { throw "Downloads folder not found: $Downloads" }
    if (-not (Test-Path $Source))    { throw "Source folder not found: $Source" }
    if (-not (Test-Path $BuildBat))  { throw "Build BAT not found: $BuildBat" }

    $instr = Get-ChildItem -Path $Downloads -Filter "A11_INSTR_*.json" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $instr) {
        throw "No instruction file found in Downloads. Expected: A11_INSTR_*.json"
    }

    Info ("Using instruction file: {0}" -f $instr.Name)
    $raw = Get-Content -Path $instr.FullName -Raw -Encoding UTF8
    $data = $raw | ConvertFrom-Json

    if ($null -eq $data.actions -or $data.actions.Count -eq 0) {
        throw "Instruction file contains no actions."
    }

    $backupRoot = Join-Path $Source "_A11_backup"
    if (-not (Test-Path $backupRoot)) {
        New-Item -ItemType Directory -Path $backupRoot | Out-Null
    }

    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupDir = Join-Path $backupRoot $stamp
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    Info ("Backup dir: {0}" -f $backupDir)
    Write-Host ""

    $changed = 0

    foreach ($act in $data.actions) {
        $kind = [string]$act.action

        if ($kind -eq "replace_file" -or $kind -eq "add_file") {
            $srcName = [string]$act.source_download
            $targetName = [string]$act.target

            if ([string]::IsNullOrWhiteSpace($srcName) -or [string]::IsNullOrWhiteSpace($targetName)) {
                throw "$kind action requires source_download and target"
            }

            $srcPath = Join-Path $Downloads $srcName
            $dstPath = Join-Path $Source $targetName

            if (-not (Test-Path $srcPath)) {
                throw "Code file not found in Downloads: $srcName"
            }

            if (Test-Path $dstPath) {
                $backupPath = Join-Path $backupDir $targetName
                Copy-Item -Path $dstPath -Destination $backupPath -Force
                Info ("Backup created: {0}" -f $backupPath)
            }

            Move-Item -Path $srcPath -Destination $dstPath -Force
            Good ("{0}: {1}" -f $kind, $targetName)
            $changed++
        }
        elseif ($kind -eq "delete_file") {
            $targetName = [string]$act.target
            if ([string]::IsNullOrWhiteSpace($targetName)) {
                throw "delete_file action requires target"
            }

            $dstPath = Join-Path $Source $targetName
            if (Test-Path $dstPath) {
                $backupPath = Join-Path $backupDir $targetName
                Copy-Item -Path $dstPath -Destination $backupPath -Force
                Remove-Item -Path $dstPath -Force
                Good ("delete_file: {0}" -f $targetName)
                $changed++
            }
            else {
                Warn ("Skip delete (not found): {0}" -f $targetName)
            }
        }
        else {
            throw "Unknown action: $kind"
        }
    }

    if ($changed -eq 0) {
        Warn "No source files were changed."
    }

    Remove-Item -Path $instr.FullName -Force
    Info "Instruction file removed from Downloads."

    Write-Host ""
    Write-Host "==========================================" -ForegroundColor White
    Write-Host "  START BUILD" -ForegroundColor White
    Write-Host "==========================================" -ForegroundColor White
    Write-Host ""

    & $BuildBat
    $buildExit = $LASTEXITCODE

    Write-Host ""
    if ($buildExit -eq 0) {
        Write-Host "==========================================" -ForegroundColor Green
        Write-Host "  UPDATE + BUILD SUCCESS" -ForegroundColor Green
        Write-Host "==========================================" -ForegroundColor Green
        Good ("Changed files: {0}" -f $changed)
        Good ("Backup dir   : {0}" -f $backupDir)
    }
    else {
        Write-Host "==========================================" -ForegroundColor Red
        Write-Host "  BUILD FAILED" -ForegroundColor Red
        Write-Host "==========================================" -ForegroundColor Red
        Fail ("Build exit code: {0}" -f $buildExit)
        Info ("Backup dir: {0}" -f $backupDir)
        exit $buildExit
    }
}
catch {
    Write-Host ""
    Fail $_.Exception.Message
    Write-Host ""
    Write-Host "UPDATE FAILED" -ForegroundColor Red
    pause
    exit 1
}

pause
