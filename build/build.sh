#!/usr/bin/env bash
# ----------------------------------------------------------------------------
# CXMTCode 一键构建脚本 - 后端 + 前端 + 测试
# ----------------------------------------------------------------------------
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"

echo "==> dotnet build (Release)"
dotnet build CXMTCode.sln -c Release

echo "==> dotnet test"
dotnet test CXMTCode.sln -c Release --no-build --logger "console;verbosity=minimal"

echo "==> 前端 npm install + vite build"
cd src/04-Web/CXMTCode.Web.React
npm install --no-audit --no-fund
npm run build
cd "$ROOT"

echo "==> 全部构建完成"
echo "  后端 DLL: src/04-Web/CXMTCode.Web.Api/bin/Release/net8.0/CXMTCode.Web.Api.dll"
echo "  前端产物: src/04-Web/CXMTCode.Web.React/dist/"
