#!/usr/bin/env bash
# ----------------------------------------------------------------------------
# CXMTCode 一键构建脚本（Linux / WSL / macOS）
#   - 构建 + 测试 + 发布到 publish/ 下两个子目录：
#       publish/api/   - dotnet publish 输出（含 web.config，可直接复制到 IIS）
#       publish/web/   - vite build 输出（含 web.config，可直接复制到 IIS）
# ----------------------------------------------------------------------------
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"

PUBLISH_ROOT="${PUBLISH_ROOT:-$ROOT/publish}"
API_OUT="$PUBLISH_ROOT/api"
WEB_OUT="$PUBLISH_ROOT/web"

echo "==> dotnet build (Release)"
dotnet build CXMTCode.sln -c Release

echo "==> dotnet test"
dotnet test CXMTCode.sln -c Release --no-build --logger "console;verbosity=minimal"

echo "==> dotnet publish (API → $API_OUT)"
rm -rf "$API_OUT"
dotnet publish src/04-Web/CXMTCode.Web.Api/CXMTCode.Web.Api.csproj \
    -c Release -r win-x64 --no-self-contained \
    -o "$API_OUT" \
    /p:PublishReadyToRun=true /p:UseAppHost=false
mkdir -p "$API_OUT/logs"

echo "==> 前端 npm install + vite build (→ $WEB_OUT)"
cd src/04-Web/CXMTCode.Web.React
npm install --no-audit --no-fund
npm run build
rm -rf "$WEB_OUT"
cp -r dist "$WEB_OUT"
cd "$ROOT"

echo
echo "================== 发布完成 =================="
echo "  后端发布：$API_OUT       （含 web.config / appsettings.json）"
echo "  前端发布：$WEB_OUT       （含 web.config / index.html / assets）"
echo
echo "下一步："
echo "  1. 把 publish/api 与 publish/web 复制到 Windows Server 的 IIS 目录"
echo "  2. 在 Windows Server PowerShell（管理员）中执行 build\\install-iis.ps1"
