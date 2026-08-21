[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'MutePilot.sln'
$projectPath = Join-Path $repositoryRoot 'src\MutePilot\MutePilot.csproj'
$publishPath = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
$installerScriptPath = Join-Path $repositoryRoot 'installer\MutePilot.iss'
$installerPath = Join-Path $repositoryRoot 'dist\MutePilot-Setup-v1.0.0.exe'

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "명령 실행에 실패했습니다 ($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

function Find-InnoSetupCompiler {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue

    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

$runningMutePilot = Get-Process -Name 'MutePilot' -ErrorAction SilentlyContinue

if ($runningMutePilot) {
    throw 'Release output을 정리하기 전에 실행 중인 MutePilot을 종료해 주세요.'
}

$dotnet = (Get-Command 'dotnet' -ErrorAction Stop).Source

Push-Location $repositoryRoot

try {
    Invoke-NativeCommand $dotnet @('clean', $solutionPath, '--configuration', 'Release')
    Invoke-NativeCommand $dotnet @('restore', $solutionPath)

    if (Test-Path -LiteralPath $publishPath) {
        Remove-Item -LiteralPath $publishPath -Recurse -Force
    }

    Invoke-NativeCommand $dotnet @(
        'publish',
        $projectPath,
        '--configuration', 'Release',
        '--runtime', 'win-x64',
        '--self-contained', 'true',
        '--output', $publishPath
    )

    $innoCompiler = Find-InnoSetupCompiler

    if (-not $innoCompiler) {
        $winget = Get-Command 'winget.exe' -ErrorAction SilentlyContinue

        if (-not $winget) {
            throw 'Inno Setup을 찾지 못했습니다. 공식 Inno Setup을 설치한 뒤 ISCC.exe를 PATH에 추가해 주세요.'
        }

        Write-Host 'Inno Setup이 없어 공식 winget 패키지 설치를 시도합니다.'
        Invoke-NativeCommand $winget.Source @(
            'install',
            '--id', 'JRSoftware.InnoSetup',
            '--exact',
            '--source', 'winget',
            '--silent',
            '--accept-package-agreements',
            '--accept-source-agreements'
        )
        $innoCompiler = Find-InnoSetupCompiler

        if (-not $innoCompiler) {
            throw 'Inno Setup 설치 뒤에도 ISCC.exe를 찾지 못했습니다. Inno Setup 6 또는 7 설치 상태를 확인해 주세요.'
        }
    }

    Invoke-NativeCommand $innoCompiler @($installerScriptPath)

    if (-not (Test-Path -LiteralPath $installerPath)) {
        throw "설치 프로그램이 생성되지 않았습니다: $installerPath"
    }

    $output = Get-Item -LiteralPath $installerPath
    Write-Host "설치 프로그램: $($output.FullName)"
    Write-Host "파일 크기: $($output.Length) bytes"
}
finally {
    Pop-Location
}
