<#
.SYNOPSIS
    CXMTCode 一键发布脚本（Windows）

.DESCRIPTION
    1) dotnet publish 生成 win-x64 自包含 / 框架依赖部署包
    2) npm run build 产生前端 dist/
    3) 输出到 publish/api/ 和 publish/web/，可直接复制到 IIS 站点目录

.EXAMPLE
    .\build\publish.ps1
    .\build\publish.ps1 -SelfContained
    .\build\publish.ps1 -OutputRoot D:\Deploy\CXMTCode
#>

[CmdletBinding()]
param(
    [switch]$SelfContained,
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\publish' -Resolve -ErrorAction SilentlyContinue)
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repoRoot 'publish'
}
if (-not (Test-Path $OutputRoot)) {
    New-Item -ItemType Directory -Path $OutputRoot | Out-Null
}

$apiOut = Join-Path $OutputRoot 'api'
$webOut = Join-Path $OutputRoot 'web'

Write-Host "==> dotnet build (Release)" -ForegroundColor Cyan
dotnet build CXMTCode.sln -c Release | Out-Host

Write-Host "==> dotnet test" -ForegroundColor Cyan
dotnet test CXMTCode.sln -c Release --no-build --logger "console;verbosity=minimal" | Out-Host

Write-Host "==> dotnet publish (API → $apiOut)" -ForegroundColor Cyan
$publishArgs = @(
    'publish', 'src/04-Web/CXMTCode.Web.Api/CXMTCode.Web.Api.csproj',
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $apiOut,
    '/p:PublishReadyToRun=true',
    '/p:PublishSingleFile=false',
    '/p:UseAppHost=false'
)
if ($SelfContained) { $publishArgs += '--self-contained' } else { $publishArgs += '--self-contained'; $publishArgs += 'false' }
dotnet @publishArgs | Out-Host

# 创建空的 logs 目录，避免 IIS 启动时 stdoutLogFile 路径不存在
$apiLogs = Join-Path $apiOut 'logs'
if (-not (Test-Path $apiLogs)) { New-Item -ItemType Directory -Path $apiLogs | Out-Null }

Write-Host "==> npm run build (前端 → $webOut)" -ForegroundColor Cyan
Push-Location (Join-Path $repoRoot 'src\04-Web\CXMTCode.Web.React')
try {
    if (-not (Test-Path node_modules)) {
        npm install --no-audit --no-fund | Out-Host
    }
    npm run build | Out-Host
    if (Test-Path $webOut) { Remove-Item -Recurse -Force $webOut }
    Copy-Item -Recurse dist $webOut
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "================== 发布完成 ==================" -ForegroundColor Green
Write-Host "  后端发布目录：$apiOut"
Write-Host "  前端发布目录：$webOut"
Write-Host ""
Write-Host "下一步：在 IIS 上分别创建两个站点指向上述目录。"
Write-Host "       或执行 build/install-iis.ps1（PowerShell 管理员）一键创建。"
