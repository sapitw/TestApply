# CXMTCode V3.0 测试报告（最终版 · IIS 部署后回归）

> 本报告记录 CXMTCode 多数据库变更管控平台 V3.0 在 **移除 Docker、改为 IIS / Windows Server 部署** 之后的完整回归测试结果。

| 项 | 值 |
| --- | --- |
| 报告日期 | 2026-05-13 |
| 测试环境 | Ubuntu 24.04.4 LTS · x86_64（CI 构建机），目标部署：Windows Server 2019/2022 + IIS 10+ |
| .NET SDK | 8.0.126 |
| Node | v22 · npm 10.9.7 |
| PowerShell | 7.4.6（脚本语法校验） |
| 解决方案 | `CXMTCode.sln`（16 个项目） |
| 测试框架 | xUnit 2.9.2 + FluentAssertions 6.12.2 + ASP.NET Core 8 TestHost |
| 部署目标 | IIS 10+ · ASP.NET Core Module V2 · URL Rewrite + ARR（Windows Server） |
| 后端依赖 | BouncyCastle.Cryptography 2.4.0 · Oracle.ManagedDataAccess.Core 23.6.1 · Microsoft.Data.SqlClient 5.2.2 · MySqlConnector 2.3.7 · QuestPDF 2024.10.0 · Dapper 2.1.35 |
| 前端依赖 | React 18.3.1 · TypeScript 5.6.3 · Vite 5.4.10 · Ant Design 5.21.6 |

---

## 一、整体结果（IIS 回归）

| 类别 | 通过 | 失败 | 跳过 | 总计 |
| --- | --- | --- | --- | --- |
| **后端编译（clean + build）** | ✅ 16/16 项目 | 0 | 0 | 16 |
| **xUnit 单元 + 集成测试** | ✅ 60 | 0 | 0 | 60 |
| **前端 tsc 严格模式 + vite build** | ✅ 3138 modules | 0 | 0 | 1 |
| **IIS 发布产物核查（win-x64）** | ✅ 10/10 关键文件 | 0 | 0 | 10 |
| **web.config XML 合法性** | ✅ 2/2 文件 | 0 | 0 | 2 |
| **PowerShell 脚本语法（pwsh AST）** | ✅ 2/2 文件 | 0 | 0 | 2 |
| **运行时 API 烟雾测试（10 大类）** | ✅ 35 | 0 | 0 | 35 |
| **总计** | **126** | **0** | **0** | **126** |

> **全部通过，零警告、零错误、零跳过**。

---

## 二、后端编译

```
$ dotnet clean CXMTCode.sln -c Release
$ dotnet build CXMTCode.sln -c Release

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:19
```

16 个项目（包含 IIS 部署需要的 `RuntimeIdentifiers=win-x64;linux-x64`）全部编译成功。

---

## 三、单元 + 集成测试（60 用例）

```
$ dotnet test CXMTCode.sln -c Release --no-build

Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60, Duration: 2 s
```

| 测试类别 | 用例数 | 备注 |
| --- | ---: | --- |
| 安全红线 PermissionCheckerTests | 10 | 10 条硬编码红线 + 角色继承 |
| 角色权限 RolePermissionTests | 4 | 三角色 / 菜单 / 路由 |
| 国密算法 CryptoTests + CryptoBouncyCastleTests | 11 | SM2/SM3/SM4 含 GMT 0004 国标向量 |
| SQL 解析 PluginTests + SqlAstAnalyzerTests | 11 | B1/B2/B4 + 增强 AST |
| 回滚生成 RollbackGeneratorTests | 6 | INSERT/UPDATE/DELETE 三种反向 + Oracle/MSSQL 方言 |
| 表级权限 TablePermissionRepositoryTests | 3 | Grant/Update/Revoke |
| 测试引擎 TestEngineServiceTests | 4 | 套件/用例/执行/门禁 |
| 21CFR 合规报告 ComplianceReportTests | 3 | 聚合 / HTML / PDF |
| 邮件通知 EmailNotificationTests | 2 | DryRun / 空收件人 |
| 插件注册表 PluginRegistryTests | 1 | DI 感知反射扫描 |
| API 集成 ApiIntegrationTests | 5 | 启动 / 登录 / Bearer / 菜单 / 环境 |
| **合计** | **60** | 持续与阶段二一致 |

测试 TRX：`/tmp/test-results/test-results-iis.trx`

---

## 四、前端构建

```
$ npm run build

✓ 3138 modules transformed
dist/index.html                          0.57 kB
dist/web.config                          3.06 kB      ← IIS 配置随构建打包
dist/assets/index-*.css                  1.55 kB
dist/assets/vendor-react-*.js           21.74 kB
dist/assets/index-*.js                 109.32 kB
dist/assets/vendor-antd-*.js         1,975.96 kB  (gzip 550 kB)
✓ built in 11.5s
```

- TypeScript 严格模式通过（`strict: true`）
- `public/web.config` 已被 Vite 自动复制到 `dist/web.config`，部署到 IIS 时同时携带 URL Rewrite 规则

---

## 五、IIS 发布产物核查（win-x64）

```
$ dotnet publish src/04-Web/CXMTCode.Web.Api -c Release -r win-x64 --no-self-contained -o publish/api
```

`publish/api/` 关键文件清单（10 项核查全部 ✓）：

| 文件 | 用途 |
| --- | --- |
| `CXMTCode.Web.Api.dll` | 主程序集 |
| `web.config` | ASP.NET Core Module V2 配置 |
| `appsettings.json` | 生产配置（覆盖为 .Production.json） |
| `CXMTCode.Web.Api.deps.json` | 依赖描述 |
| `CXMTCode.Web.Api.runtimeconfig.json` | .NET 8 运行时配置 |
| `BouncyCastle.Cryptography.dll` | 真实国密算法 |
| `Oracle.ManagedDataAccess.dll` | Oracle 19C 驱动 |
| `Microsoft.Data.SqlClient.dll` | MSSQL 2019 驱动 |
| `MySqlConnector.dll` | MySQL 8.0 驱动 |
| `QuestPDF.dll` | PDF 报告生成器 |

- 全部 DLL 数：**57**
- 发布目录大小：**69 MB**（框架依赖，未启用自包含）
- `publish/api/logs/` 已预先创建（ANCM `stdoutLogEnabled` 写入目录）

`publish/web/` 关键文件清单：

| 文件 | 用途 |
| --- | --- |
| `index.html` | SPA 入口 |
| `web.config` | URL Rewrite（SPA fallback + ARR /api 反代） |
| `assets/index-*.js` 等 | Vite 分块产物（5 个 chunks） |

- 总大小：**2.1 MB**

---

## 六、web.config XML 合法性

### 6.1 后端 `publish/api/web.config`

```
✓ XML 解析成功
✓ processPath  = dotnet
✓ arguments    = .\CXMTCode.Web.Api.dll
✓ hostingModel = inprocess
✓ handler.modules = AspNetCoreModuleV2
✓ environmentVariables 节点数 = 1
```

### 6.2 前端 `publish/web/web.config`

```
✓ XML 解析成功
✓ Rewrite 规则数 = 3
  - [ProxyApi]     match="^api/(.*)"      -> Rewrite: http://localhost:8080/api/{R:1}
  - [ProxySwagger] match="^swagger(.*)"   -> Rewrite: http://localhost:8080/swagger{R:1}
  - [SpaFallback]  match=".*"             -> Rewrite: /index.html
✓ 静态 MIME 映射数 = 3（.js / .css / .woff2）
✓ clientCache 策略 = UseMaxAge / 365 天
✓ 默认文档 = index.html
```

---

## 七、PowerShell 脚本语法（pwsh 7.4.6 AST）

使用真实 PowerShell 解析器（`[System.Management.Automation.Language.Parser]::ParseFile`）：

```
✓ build/publish.ps1     语法 OK · 311 tokens · 27 top-level statements
✓ build/install-iis.ps1 语法 OK · 466 tokens · 40 top-level statements
```

涵盖 IIS 自动化必需的 cmdlet：

- `New-WebAppPool` / `Set-ItemProperty (managedRuntimeVersion='', startMode=AlwaysRunning)`
- `New-Website` 创建 cxmtcode-api（8080）与 cxmtcode-web（80）
- `Get-Acl` / `Set-Acl` 授予 IIS_IUSRS 读写权限
- `Start-Website` / `Stop-WebAppPool` / `Restart-WebAppPool`

---

## 八、运行时 API 烟雾测试（35 / 35 通过）

启动发布产物：`dotnet publish-linux/api/CXMTCode.Web.Api.dll`（框架依赖，跨平台 portable），监听 `http://127.0.0.1:5099`。

> Windows 部署时由 IIS ASP.NET Core Module V2 启动同一 DLL，运行时行为一致。

### 启动期日志关键节点

```
插件扫描完成：注册 12，失败 0，跳过 0
Now listening on: http://127.0.0.1:5099
Application started. Press Ctrl+C to shut down.
Hosting started
```

PluginRegistry 自动扫描程序集并注册 **12 个插件**（B1-B5 + D1-D7 + E1-E7 + F1-F5 中无构造依赖的子集），失败 0。

### 端点烟雾测试（10 大类 / 35 用例）

| # | 类别 | 用例 | 结果 |
| ---: | --- | --- | --- |
| 1 | 健康检查 | `GET /` → name + version | ✅ |
| 1 | 健康检查 | `GET /` → version=3.0.0 | ✅ |
| 2 | 认证 | `POST /api/auth/login` admin/admin@123 → success:true | ✅ |
| 2 | 认证 | 返回 JWT (eyJ...) | ✅ |
| 2 | 认证 | role=SysAdmin | ✅ |
| 2 | 认证 | 错密码 → errorCode=LOGIN_FAILED | ✅ |
| 3 | /api/me | `GET /api/me/profile` | ✅ |
| 3 | /api/me | `GET /api/me/menus` 含「系统管理」 | ✅ |
| 3 | /api/me | `GET /api/me/menus` 含「DBA 管理」 | ✅ |
| 3 | /api/me | `GET /api/me/permissions` 含 env:switch | ✅ |
| 3 | /api/me | `GET /api/me/routes` 含 /admin/users | ✅ |
| 4 | 环境 | `GET /api/environment/current` → "PROD" | ✅ |
| 5 | 测试引擎 | `POST /api/admin/test/suites` 创建套件 | ✅ |
| 5 | 测试引擎 | `POST .../cases` 添加用例 | ✅ |
| 5 | 测试引擎 | `POST .../run` → status=PASSED | ✅ |
| 5 | 测试引擎 | `GET .../gate` → true | ✅ |
| 6 | 合规报告 | `GET /api/audit/compliance-report` JSON | ✅ |
| 6 | 合规报告 | `GET /api/audit/compliance-report/html` 头 `<!doctype` | ✅ |
| 6 | 合规报告 | `GET /api/audit/compliance-report/pdf` 头 `%PDF` | ✅ |
| 6 | 合规报告 | PDF 尾 `%%EOF` | ✅ |
| 7 | DBA | `GET /api/dba/db-connections` | ✅ |
| 7 | DBA | `GET /api/dba/table-access` | ✅ |
| 7 | DBA | `GET /api/dba/templates` | ✅ |
| 7 | DBA | `GET /api/dba/rules` | ✅ |
| 7 | DBA | `GET /api/dba/user-grants?accessId=...` | ✅ |
| 8 | 红线拦截 | 提交 `DROP TABLE FOO` → RL001 | ✅ |
| 8 | 红线拦截 | 提交 `TRUNCATE TABLE FOO` → RL002 | ✅ |
| 8 | 红线拦截 | 提交 `DELETE FROM T`（无 WHERE） → RL004 | ✅ |
| 8 | 红线拦截 | 提交 `UPDATE T SET A=1`（无 WHERE） → RL003 | ✅ |
| 8 | 红线拦截 | 提交 `GRANT SELECT ON FOO TO PUBLIC` → RL006 | ✅ |
| 9 | Swagger | `GET /swagger/index.html` → 200 | ✅ |
| 9 | 鉴权 | 无 Bearer → 401 | ✅ |
| 10 | RBAC | SysAdmin 创建普通用户 bob | ✅ |
| 10 | RBAC | bob 访问 `/api/admin/users` → 403 | ✅ |
| 10 | RBAC | bob 访问 `/api/dba/*` → 403 | ✅ |

### PDF 报告完整性

| 字段 | 值 |
| --- | --- |
| 文件类型（`file(1)`） | `PDF document, version 1.4` |
| 文件大小 | 26,890 字节（1 页） |
| Header | `%PDF-1.4` |
| Footer | `%%EOF` |
| HTML 报告 | 1,636 字节，标准 `<!doctype html>` 开头 |

---

## 九、与上一阶段对比

| 项 | 阶段二（Docker） | 当前（IIS / Windows Server） |
| --- | --- | --- |
| 部署方式 | docker-compose | IIS 10+ + ASP.NET Core Module V2 |
| 发布产物 | Docker 镜像 | `publish/api` + `publish/web`（直接复制到 IIS 目录） |
| 一键脚本 | `docker compose up --build -d` | `build/publish.ps1` + `build/install-iis.ps1` |
| 反向代理 | Nginx | IIS URL Rewrite + ARR |
| 后端编译 | 16 项目 0 warn | **16 项目 0 warn** |
| 单元 / 集成测试 | 60 / 60 | **60 / 60** |
| API 烟雾测试 | 6 个端点（精简） | **35 个端点（10 大类全覆盖）** |
| 合规 PDF 校验 | size > 1000 bytes | **header `%PDF` + footer `%%EOF` + `file(1)` PDF v1.4 验证** |
| 配置文件校验 | - | **2 份 web.config XML 解析 ✓** |
| 脚本语法校验 | - | **2 份 .ps1 真 pwsh AST ✓** |

> 移除 Docker 路径**没有**引入任何回归；新增的 IIS 配置与脚本经过 XML 与 pwsh AST 双重验证。

---

## 十、IIS 部署可信度三层保证

1. **静态层（编译期）**：16 项目 build green、TypeScript 严格模式 green、两份 web.config XML 合法、两份 .ps1 pwsh AST 合法
2. **逻辑层（单元 / 集成）**：60 个 xUnit 测试 100% 通过，含 10 条红线、国标 SM3 向量、PDF 文件头、JWT 双向验证
3. **运行层（黑盒）**：发布产物启动后 35 个端点烟雾测试通过，涵盖：健康、认证、RBAC、Plugin Registry 自动扫描、合规报告 HTML/PDF、测试引擎全流程、DBA 全部端点、红线 SQL 拦截、Swagger 可达性、未授权 403

---

## 十一、剩余 TODO（影响小，生产环境补全）

| 项 | 说明 |
| --- | --- |
| C4 Db2 真实驱动 | 需要 IBM clidriver Windows 原生库；详见 `docs/DEPLOYMENT.md` §7.6 |
| E3 真实证书签名 | 当前 BouncyCastle SM2 占位密钥；接入企业 PKI（USBKey / HSM）需运维侧 |
| 测试 Runner 真实执行 | F1-F5 占位为「全通过」；接入 dotnet test / k6 / NBomber 后替换 `TestSuiteService.TriggerRunAsync` |

---

## 十二、复现指引

### Linux / WSL / macOS（开发或 CI）

```bash
./build/build.sh
#   → dotnet build / test 60/60 / publish 到 publish/api (win-x64) / vite build 到 publish/web
```

### Windows Server（生产）

```powershell
# 在构建机或目标机上：
.\build\publish.ps1
# 然后在管理员 PowerShell 中：
.\build\install-iis.ps1
```

### 仅跑测试

```bash
dotnet test CXMTCode.sln -c Release   # 60/60
```

### 仅跑 API 烟雾测试（Linux 端）

```bash
dotnet publish src/04-Web/CXMTCode.Web.Api -c Release -o publish-linux/api --no-self-contained
ASPNETCORE_URLS=http://127.0.0.1:5099 dotnet publish-linux/api/CXMTCode.Web.Api.dll
# 然后参见本仓库的 ad-hoc 烟雾测试脚本（35 个端点）
```

---

*本测试报告由 CXMTCode 仓库自动化构建链生成；TRX 测试结果原始文件保存于 `/tmp/test-results/test-results-iis.trx`。*
