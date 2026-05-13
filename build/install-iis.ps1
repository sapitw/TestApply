<#
.SYNOPSIS
    CXMTCode IIS 站点 + 应用程序池 一键创建（Windows Server / IIS 10+）

.DESCRIPTION
    需以 **管理员** 身份运行 PowerShell。
    本脚本会：
      1. 检查 IIS / .NET 8 Hosting Bundle / URL Rewrite 是否安装
      2. 创建两个应用程序池：CXMTCodeApiPool（No Managed Code）和 CXMTCodeWebPool
      3. 创建两个站点：cxmtcode-api（端口 8080，指向 ApiPath）和 cxmtcode-web（端口 80，指向 WebPath）
      4. 给两个目录授予 IIS_IUSRS 的读取权限；ApiPath 额外授予写入（写 logs 与 db）
      5. 启动站点并打开浏览器到首页

.PARAMETER ApiPath
    后端发布目录，默认 C:\inetpub\cxmtcode\api

.PARAMETER WebPath
    前端发布目录，默认 C:\inetpub\cxmtcode\web

.PARAMETER ApiPort
    后端站点端口，默认 8080

.PARAMETER WebPort
    前端站点端口，默认 80

.EXAMPLE
    # 默认安装
    .\build\install-iis.ps1

    # 自定义路径与端口
    .\build\install-iis.ps1 -ApiPath D:\Apps\cxmt-api -WebPath D:\Apps\cxmt-web -ApiPort 8181 -WebPort 8080
#>

[CmdletBinding()]
param(
    [string]$ApiPath = 'C:\inetpub\cxmtcode\api',
    [string]$WebPath = 'C:\inetpub\cxmtcode\web',
    [int]   $ApiPort = 8080,
    [int]   $WebPort = 80
)

$ErrorActionPreference = 'Stop'

# 管理员检查
$current = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($current)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '请以管理员身份运行 PowerShell'
}

# 1. 检查依赖
Write-Host '==> 检查依赖' -ForegroundColor Cyan
Import-Module WebAdministration -ErrorAction Stop

$ancm = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\IIS AspNetCore Module V2' -ErrorAction SilentlyContinue
if (-not $ancm) {
    Write-Warning 'ASP.NET Core Module V2 未安装。请先安装 .NET 8 Hosting Bundle：'
    Write-Warning 'https://dotnet.microsoft.com/download/dotnet/8.0  ->  Hosting Bundle'
}

$rewrite = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\URL Rewrite' -ErrorAction SilentlyContinue
if (-not $rewrite) {
    Write-Warning 'URL Rewrite Module 未检测到。前端 SPA 回退与 /api 反向代理依赖此模块。'
    Write-Warning '下载：https://www.iis.net/downloads/microsoft/url-rewrite'
}

# 2. 目录存在性检查
if (-not (Test-Path $ApiPath)) { throw "API 发布目录不存在：$ApiPath  请先运行 build/publish.ps1" }
if (-not (Test-Path $WebPath)) { throw "Web 发布目录不存在：$WebPath  请先运行 build/publish.ps1" }

# 3. 应用程序池
$apiPool = 'CXMTCodeApiPool'
$webPool = 'CXMTCodeWebPool'

foreach ($pool in @($apiPool, $webPool)) {
    if (Test-Path "IIS:\AppPools\$pool") {
        Write-Host "   AppPool 已存在：$pool" -ForegroundColor DarkGray
    } else {
        New-WebAppPool -Name $pool | Out-Null
        Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value '' # No Managed Code
        Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'
        Write-Host "   已创建 AppPool: $pool" -ForegroundColor Green
    }
}

# 4. 后端站点
$apiSite = 'cxmtcode-api'
if (Test-Path "IIS:\Sites\$apiSite") {
    Stop-Website -Name $apiSite -ErrorAction SilentlyContinue
    Remove-Website -Name $apiSite
}
New-Website -Name $apiSite -PhysicalPath $ApiPath -ApplicationPool $apiPool -Port $ApiPort -Force | Out-Null
Set-ItemProperty "IIS:\Sites\$apiSite" -Name preloadEnabled -Value $true
Write-Host "   已创建后端站点 $apiSite : 端口 $ApiPort -> $ApiPath" -ForegroundColor Green

# 5. 前端站点
$webSite = 'cxmtcode-web'
if (Test-Path "IIS:\Sites\$webSite") {
    Stop-Website -Name $webSite -ErrorAction SilentlyContinue
    Remove-Website -Name $webSite
}
New-Website -Name $webSite -PhysicalPath $WebPath -ApplicationPool $webPool -Port $WebPort -Force | Out-Null
Write-Host "   已创建前端站点 $webSite : 端口 $WebPort -> $WebPath" -ForegroundColor Green

# 6. 目录权限
function Grant-Permission {
    param($Path, $Identity, $Right)
    $acl = Get-Acl $Path
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $Identity, $Right, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.SetAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
}

Write-Host '==> 授予 IIS_IUSRS 权限' -ForegroundColor Cyan
Grant-Permission -Path $ApiPath -Identity 'IIS_IUSRS' -Right 'ReadAndExecute,Modify'   # 写 logs/ + db/
Grant-Permission -Path $WebPath -Identity 'IIS_IUSRS' -Right 'ReadAndExecute'

# 7. 启动站点
Start-Website -Name $apiSite
Start-Website -Name $webSite

Write-Host ''
Write-Host '================== IIS 部署完成 ==================' -ForegroundColor Green
Write-Host "  后端 API:  http://localhost:$ApiPort/swagger"
Write-Host "  前端 UI :  http://localhost:$WebPort/"
Write-Host '  默认账号：admin / admin@123（首次登录后请立刻修改）'
Write-Host ''
Write-Host '常见后续操作：'
Write-Host '  - 编辑 appsettings.Production.json 修改数据库连接 / JWT 密钥 / SMTP'
Write-Host '  - iisreset                               重启 IIS'
Write-Host "  - Restart-WebAppPool -Name $apiPool      只重启后端"
