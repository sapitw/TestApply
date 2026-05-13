# CXMTCode V3.0 部署说明书

> 本文档说明如何在「开发机 / 测试环境 / 生产环境」三种场景下部署 CXMTCode，以及如何修改各类数据库连接配置。

---

## 一、部署模式速查

| 模式 | 宿主 | 系统 DB | 业务 DB | 适用场景 |
| --- | --- | --- | --- | --- |
| **A. 本地开发** | dotnet run + vite dev | SQLite（自动创建） | 可空 / 测试库 | 开发调试、UI 演示 |
| **B. Windows Server + IIS（SQLite）** | IIS 10+ + ASP.NET Core Module | SQLite（IIS 站点目录） | 测试 Oracle / MSSQL | 内网 POC、试运行 |
| **C. Windows Server + IIS（Oracle）** | IIS 10+ + ASP.NET Core Module | Oracle 19C Enterprise | Oracle / MSSQL / MySQL / DB2 | 企业正式生产部署 |

---

## 二、模式 A：本地开发（最快上手）

> 适合：开发调试、UI 演示。仅需 .NET 8 SDK + Node 20+，无需 IIS / Oracle。

### 2.1 环境准备

| 工具 | 版本 | 验证命令 |
| --- | --- | --- |
| .NET SDK | 8.0.x | `dotnet --version` |
| Node.js | 20+ | `node --version` |
| npm | 10+ | `npm --version` |
| git | 任意 | `git --version` |

### 2.2 启动后端

```bash
git clone <仓库地址> CXMTCode
cd CXMTCode

# （可选）复制环境变量
cp .env.example .env

# 启动 API（默认监听 5099）
dotnet run --project src/04-Web/CXMTCode.Web.Api
```

启动日志会显示：

```
Now listening on: http://localhost:5099
Application started. Press Ctrl+C to shut down.
```

首次启动会自动：
1. 在 `db/cxmtcode-dev.db` 创建 SQLite 文件
2. 建立 13 张 `CXMT_*` 表
3. 注入默认 `admin / admin@123` SysAdmin 账号

Swagger UI：<http://localhost:5099/swagger>

### 2.3 启动前端

```bash
cd src/04-Web/CXMTCode.Web.React
npm install
npm run dev
```

打开 <http://localhost:5173>，用 `admin / admin@123` 登录。

---

## 三、模式 B：Windows Server + IIS（SQLite 系统库）

> 适合：内网 POC、试运行、不依赖外部 DB 的轻量部署。

### 3.1 Windows Server 前置环境

| 组件 | 版本 | 安装方式 |
| --- | --- | --- |
| Windows Server | 2019 / 2022 | - |
| IIS | 10+ | 服务器管理器 → 添加角色 → Web 服务器 (IIS) |
| .NET 8 Hosting Bundle | 8.0+ | 下载 https://dotnet.microsoft.com/download/dotnet/8.0 → Hosting Bundle，**装完后必须重启 IIS（iisreset）** |
| URL Rewrite Module | 2.x | https://www.iis.net/downloads/microsoft/url-rewrite |
| Application Request Routing (ARR) | 3.0 | https://www.iis.net/downloads/microsoft/application-request-routing （仅当 IIS 反向代理 /api 时） |
| Node.js（可选） | 20+ | 仅在 Windows Server 上本地构建时需要；CI/构建机已构建可省略 |
| .NET 8 SDK（可选） | 8.0+ | 仅当现场构建时；CI 已 publish 可省略 |

ARR 安装后须打开：`IIS Manager → 服务器节点 → Application Request Routing Cache → Server Proxy Settings → ✅ Enable proxy`

### 3.2 构建发布包

在构建机或 Windows Server 本机：

```powershell
git clone <仓库地址> CXMTCode
cd CXMTCode
.\build\publish.ps1
# 产物：
#   publish\api\   - ASP.NET Core publish（含 web.config / appsettings.json / logs/）
#   publish\web\   - vite build（含 web.config / index.html / assets/）
```

`build/publish.ps1` 内部依次：`dotnet restore → build → test → publish -r win-x64 → npm run build`。

### 3.3 部署到 IIS（一键脚本）

把 `publish/` 复制到 Windows Server（例如 `C:\inetpub\cxmtcode\`），然后在**管理员 PowerShell**中：

```powershell
.\build\install-iis.ps1
# 默认行为：
#   - 创建 AppPool: CXMTCodeApiPool / CXMTCodeWebPool（均设为 No Managed Code）
#   - 创建 Site:    cxmtcode-api（端口 8080）→ publish\api
#                   cxmtcode-web（端口 80）  → publish\web
#   - 授予 IIS_IUSRS 对 publish\api 的 读/写 权限（日志 + SQLite 文件）
#   - 启动两个站点

# 自定义路径与端口：
.\build\install-iis.ps1 -ApiPath D:\Apps\cxmt-api -WebPath D:\Apps\cxmt-web -ApiPort 8181 -WebPort 8080
```

完成后访问：
- 前端 UI：<http://localhost/>
- 后端 API + Swagger：<http://localhost:8080/swagger>
- 默认账号：`admin / admin@123`（首次登录后请立刻修改密码）

### 3.4 手动部署（不使用脚本）

如不想用 `install-iis.ps1`，等价的 IIS Manager 操作：

1. **应用程序池**
   - 名称：`CXMTCodeApiPool`，.NET CLR 版本：**无托管代码**，启动模式：**AlwaysRunning**
   - 名称：`CXMTCodeWebPool`，同上
2. **站点 - 后端**
   - 物理路径：`C:\inetpub\cxmtcode\api`
   - 应用程序池：`CXMTCodeApiPool`
   - 绑定：HTTP 端口 `8080`
3. **站点 - 前端**
   - 物理路径：`C:\inetpub\cxmtcode\web`
   - 应用程序池：`CXMTCodeWebPool`
   - 绑定：HTTP 端口 `80`
4. **权限**：右键 `publish\api` → 属性 → 安全 → 添加 `IIS_IUSRS`，授予「修改」（写 logs/ 与 db/）
5. **启动**：两个站点都点「启动」

### 3.5 修改配置

`publish\api\appsettings.json`（或新增 `appsettings.Production.json`）：

```json
{
  "ConnectionStrings": {
    "SystemDb": "Data Source=db\\cxmtcode-prod.db"
  },
  "JwtSettings": {
    "SecretKey": "请改成至少 32 字符的高熵随机串",
    "Issuer": "CXMTCode",
    "Audience": "CXMTCodeReact",
    "ExpirationMinutes": 480
  },
  "Smtp": {
    "Enabled": false
  }
}
```

修改后执行：

```powershell
Restart-WebAppPool -Name CXMTCodeApiPool
```

> SQLite 模式下 DB 文件位于 `publish\api\db\cxmtcode-prod.db`，备份只需复制该文件即可。

### 3.6 反向代理工作原理

`publish\web\web.config` 已内置 URL Rewrite 规则：

| 请求 | 行为 |
| --- | --- |
| `GET /api/foo` | ARR 反向代理到 `http://localhost:8080/api/foo` |
| `GET /swagger/*` | 反向代理到后端 swagger |
| `GET /change/list` 等 SPA 路由 | 回退到 `/index.html`（不存在的物理文件） |
| `GET /assets/*.js` | IIS 直接返回静态文件 |

这样前端站点（80）和后端站点（8080）通过 IIS 内部 ARR 串联，外部仅暴露 80 端口即可。

---

## 四、模式 C：Windows Server + IIS + Oracle 19C 系统库（生产推荐）

> 适合：等保 2.0 合规、企业正式部署。

### 4.1 步骤总览

```
1. 安装 / 准备 Oracle 19C Enterprise
2. 执行 db/oracle-init.sql 建库 + 建用户 CXMT_PLATFORM
3. 在 Windows Server 安装 IIS + .NET 8 Hosting Bundle + URL Rewrite + ARR
4. 在构建机执行 .\build\publish.ps1 得到 publish\api 与 publish\web
5. 复制到 Windows Server 后执行 .\build\install-iis.ps1
6. 修改 publish\api\appsettings.Production.json：
     - ConnectionStrings:SystemDb（指向 Oracle）
     - JwtSettings:SecretKey
     - Smtp（如启用）
7. 重启应用程序池：Restart-WebAppPool CXMTCodeApiPool
8. 配置 HTTPS（IIS 绑定证书）
9. 首次登录后立刻修改 admin 密码并创建生产 DBA / 用户账号
```

### 4.2 Oracle 19C 系统库初始化

```bash
sqlplus / as sysdba @db/oracle-init.sql
```

`oracle-init.sql` 会：
- 创建三个表空间：`CXMT_DATA` / `CXMT_IDX` / `CXMT_AUDIT`
- 创建平台用户 `CXMT_PLATFORM`（默认密码 `CXMTCode#2026`，**生产请修改**）
- 创建 13 张核心表 + 索引 + 审计季度分区 + 触发器
- 注入默认配置 `CURRENT_ENVIRONMENT=PROD`、`ALLOW_ENV_SWITCH=1`
- 注入 admin 账号占位

**重要**：脚本中的 admin 账号 `PASSWORD_HASH` 为占位，首次启动 Web.Api 会重新写入真实 SM3 哈希。

### 4.3 配置生产连接串

新建 `src/04-Web/CXMTCode.Web.Api/appsettings.Production.json`（已加入 `.gitignore`）：

```json
{
  "ConnectionStrings": {
    "SystemDb": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle-prod.internal)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCLPDB1)));User Id=CXMT_PLATFORM;Password=YourStrongPasswordHere;"
  },
  "JwtSettings": {
    "SecretKey": "至少 32 字符的高熵随机串（生产环境请用 KMS 注入）",
    "Issuer": "CXMTCode",
    "Audience": "CXMTCodeReact",
    "ExpirationMinutes": 480
  },
  "Frontend": { "Url": "https://cxmtcode.your-domain.com" },
  "Smtp": {
    "Enabled": true,
    "Host": "smtp.your-domain.com",
    "Port": 465,
    "UseSsl": true,
    "Username": "alert@your-domain.com",
    "Password": "...",
    "From": "CXMTCode 通知 <alert@your-domain.com>"
  }
}
```

> **新增**：`Smtp` 节段配置 D7 邮件通知。`Enabled=false`（默认）时邮件以 DryRun 模式仅记入日志，便于联调。

> **接入 Oracle 提醒**：当前仓库内的 `SystemDbContext` 默认通过 `SqliteSystemDbContext` 实现，生产 Oracle 需补一个 `OracleSystemDbContext`（使用 `Oracle.ManagedDataAccess.Core`）并在 `Program.cs` 注册：
>
> ```csharp
> builder.Services.AddSingleton<ISystemDbContext, OracleSystemDbContext>();
> ```
>
> 这是规格里明确要求但当前仓库标记为 TODO 的部分。

### 4.4 后端发布到 IIS

```powershell
# 在构建机或本机
.\build\publish.ps1
# 把 publish\api 复制到 Windows Server，例如 C:\inetpub\cxmtcode\api

# Windows Server 管理员 PowerShell
.\build\install-iis.ps1 -ApiPath C:\inetpub\cxmtcode\api -WebPath C:\inetpub\cxmtcode\web
```

发布后产物目录关键文件：

```
publish\api\
├── CXMTCode.Web.Api.dll      ← 由 ASP.NET Core Module 启动
├── web.config                ← IIS 配置（已带 hostingModel=inprocess）
├── appsettings.json          ← 生产配置（请改为 Production 版本）
├── logs\                     ← stdout / stderr 日志输出目录
├── db\                       ← SQLite 系统库（Oracle 模式可忽略）
└── *.dll                     ← 全部依赖
```

### 4.5 前端发布到 IIS

构建生成的 `publish\web\` 已包含：

```
publish\web\
├── index.html
├── web.config                ← 已内置：URL Rewrite（SPA 回退 + /api 反向代理）
└── assets\                   ← vite 切分后的 vendor-react / vendor-antd / vendor-dnd / index 等 chunks
```

直接复制到 Windows Server 站点根目录即可（例如 `C:\inetpub\cxmtcode\web`）。

### 4.6 启用 HTTPS（生产必做）

在 IIS Manager 中：

1. 服务器证书 → 导入企业证书（PFX）
2. 选中 `cxmtcode-web` 站点 → 绑定 → 添加：
   - 类型：`https`
   - 端口：`443`
   - SSL 证书：选刚才导入的证书
3. 在 SSL 设置中勾选「要求 SSL」+「忽略客户端证书」（双向 TLS 需另行配置）
4. （可选）在 `web.config` 的 `<rewrite><rules>` 顶部加 HTTP → HTTPS 规则：

```xml
<rule name="ForceHttps" stopProcessing="true">
  <match url=".*" />
  <conditions>
    <add input="{HTTPS}" pattern="off" />
  </conditions>
  <action type="Redirect" url="https://{HTTP_HOST}/{R:0}" redirectType="Permanent" />
</rule>
```

### 4.7 Windows 防火墙

只放行入站 80 / 443（前端），后端 8080 仅本机 ARR 调用，无需对外开放：

```powershell
New-NetFirewallRule -DisplayName "CXMTCode HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
New-NetFirewallRule -DisplayName "CXMTCode HTTP"  -Direction Inbound -Protocol TCP -LocalPort 80  -Action Allow
# 不要放行 8080
```

---

## 五、数据库连接管理（核心运维）

### 5.1 两类连接

| 连接类型 | 存放位置 | 修改方式 |
| --- | --- | --- |
| **系统数据库连接** | `appsettings.json` 或 `appsettings.Production.json` 的 `ConnectionStrings:SystemDb` | 改配置 → 重启 API |
| **业务数据库连接** | 系统库的 `CXMT_DB_CONNECTIONS` 表 | DBA 在 UI「DB 连接配置」页或调 API 修改，无需重启 |

### 5.2 修改系统数据库连接

**SQLite → Oracle 切换**：

1. 准备好 Oracle 19C 并执行 `db/oracle-init.sql`
2. 编辑 `appsettings.Production.json`：
   ```json
   {
     "ConnectionStrings": {
       "SystemDb": "Data Source=(DESCRIPTION=...);User Id=CXMT_PLATFORM;Password=..."
     }
   }
   ```
3. （TODO）确保 `Program.cs` 注册的是 `OracleSystemDbContext` 而非 `SqliteSystemDbContext`
4. 重启后端应用程序池：`Restart-WebAppPool -Name CXMTCodeApiPool`

**修改 Oracle 密码 / 主机**：

1. 在 Oracle 中改密码：`ALTER USER CXMT_PLATFORM IDENTIFIED BY "新密码";`
2. 更新 `publish\api\appsettings.Production.json` 的连接串
3. 重启 API：`Restart-WebAppPool -Name CXMTCodeApiPool`

### 5.3 添加 / 修改业务数据库连接

> 任何业务 DB（Oracle 业务库、MSSQL 业务库等）的连接信息**不要写进配置文件**，而是通过 UI 维护，落入 `CXMT_DB_CONNECTIONS` 表，SM4 加密存储密码。

#### 方式 1：UI 操作（推荐）

1. 使用 DBA 或 SysAdmin 账号登录
2. 进入 **DBA 管理 → DB 连接配置**
3. 点击「新增连接」，填写：
   - **连接名称**：如 `MES 生产 Oracle 主`
   - **环境类型**：`PROD` 或 `TEST`（决定该连接归属哪个环境）
   - **数据库类型**：Oracle / MSSQL / MySQL / DB2
   - **主机 / 端口 / 服务名 / 账号 / 密码**
   - **备库主机 / 端口 / 服务名**（用于 DryRun 预演的只读副本）
4. 保存后点「测试」按钮验证连通性
5. 点「激活」将其设为该 DB 类型 + 环境下的当前激活连接

#### 方式 2：API（适合脚本化部署）

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"userName":"admin","password":"admin@123"}' \
  | jq -r .data.token)

curl -X POST http://localhost:8080/api/dba/db-connections \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{
    "connectionName": "MES 生产 Oracle 主",
    "databaseType": 1,
    "environmentType": "PROD",
    "host": "oracle-mes-prod.internal",
    "port": 1521,
    "serviceName": "MESPDB",
    "username": "mes_app",
    "password": "your_password",
    "standbyHost": "oracle-mes-prod-adg.internal",
    "standbyPort": 1521,
    "standbyService": "MESPDB_ADG",
    "description": "MES 生产库 - 含 ADG 只读备库"
  }'
```

#### 方式 3：直接改库（紧急运维）

```sql
-- 改密码（Oracle 系统库执行）
UPDATE CXMT_DB_CONNECTIONS
   SET PASSWORD_ENC = '密文Base64',  -- 必须先用 SM4 加密
       UPDATED_AT   = SYSTIMESTAMP
 WHERE CONNECTION_NAME = 'MES 生产 Oracle 主';
COMMIT;
```

> ⚠️ 直接改库不会触发审计日志，仅用于紧急运维。日常请走 UI / API。

### 5.4 TEST / PROD 环境切换

仅 **SysAdmin** 可见顶部切换开关：

1. 点击顶部「切换」开关（绿/红切换）
2. 系统会：
   - 更新 `CXMT_SYSTEM_CONFIG.CURRENT_ENVIRONMENT`
   - 写入 `CXMT_ENV_SWITCH_LOG`（操作人 / 来源 / 目标 / IP）
   - 刷新页面，所有用户看到新环境
3. 后续变更申请会自动过滤显示对应环境的 DB 连接

### 5.5 备库（DryRun 预演）连接要求

| DB | 备库标识 | 配置示例 |
| --- | --- | --- |
| Oracle | 连接串需含 `STANDBY` 或 `READONLY` 关键字 | `...;STANDBY=true` |
| MSSQL | 连接串需含 `ApplicationIntent=ReadOnly` | `...;ApplicationIntent=ReadOnly` |
| MySQL | 节点 `@@read_only = 1` | Group Replication Secondary |
| DB2 | `mon_get_hadr().hadr_role = STANDBY` | HADR STANDBY 节点 |

DryRun 必须连备库，硬编码安全策略阻止预演访问主库。

---

## 六、首次登录后必做事项

| 步骤 | 命令 / 操作 |
| --- | --- |
| 1. 修改 admin 密码 | UI 进入「系统管理 → 用户管理」，禁用 / 重建 admin |
| 2. 创建 DBA 账号 | 同上，赋角色 DBA |
| 3. 创建业务 DB 连接 | 「DBA 管理 → DB 连接配置」按 TEST/PROD 分别录入 |
| 4. 录入表准入 | 「DBA 管理 → 表准入」对需要变更的业务表逐一审批 |
| 5. 录入 DELETE 模板 | 「DBA 管理 → DELETE 模板」为高频 DELETE 操作建模板 |
| 6. 录入白名单规则 | 可选，普通用户白名单细粒度控制 |
| 7. 修改 JWT SecretKey | 编辑 `appsettings.Production.json`，重启 API |

---

## 七、升级 / 回滚

### 7.1 升级

```powershell
# 1. 备份 SQLite DB（或 Oracle expdp）
Copy-Item C:\inetpub\cxmtcode\api\db\cxmtcode-prod.db `
          C:\backup\cxmtcode-$(Get-Date -Format yyyyMMdd).db

# 2. 在构建机重新构建
.\build\publish.ps1

# 3. 停止站点（避免文件占用）
Stop-WebAppPool  -Name CXMTCodeApiPool
Stop-WebAppPool  -Name CXMTCodeWebPool

# 4. 覆盖发布目录
robocopy .\publish\api C:\inetpub\cxmtcode\api /MIR /XD logs db
robocopy .\publish\web C:\inetpub\cxmtcode\web /MIR

# 5. 启动站点
Start-WebAppPool -Name CXMTCodeApiPool
Start-WebAppPool -Name CXMTCodeWebPool
```

如有 DDL 变更（Oracle）：

```sql
sqlplus / as sysdba @db/upgrade-2026Q3.sql   -- 由 DBA 提供升级脚本
```

### 7.2 回滚

```powershell
# 1. 切回旧版本 tag 重新构建
git checkout v3.0.0
.\build\publish.ps1

# 2. 停 / 覆盖 / 启
Stop-WebAppPool  -Name CXMTCodeApiPool
robocopy .\publish\api C:\inetpub\cxmtcode\api /MIR /XD logs db
robocopy .\publish\web C:\inetpub\cxmtcode\web /MIR

# 3. 还原 DB 备份（仅 SQLite 模式）
Copy-Item C:\backup\cxmtcode-YYYYMMDD.db `
          C:\inetpub\cxmtcode\api\db\cxmtcode-prod.db -Force

Start-WebAppPool -Name CXMTCodeApiPool
```

---

## 7.5 21CFR Part11 合规报告导出

SysAdmin 可在 UI「系统管理 → 自检报告」选择时间区间，下载 HTML 或 PDF 格式的合规自检报告。报告包含：

- 报告期内全部操作的聚合统计（按 OperationType / Result）
- SM3 哈希链抽样校验（每 50 条抽 1 条，最多 20 条）
- 操作明细前 500 条（HTML 全量、PDF 前 200 条）

也可直接调用 API：

```bash
# HTML
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/audit/compliance-report/html?from=2026-04-01T00:00:00Z&to=2026-05-01T00:00:00Z" \
  -o compliance.html

# PDF
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/audit/compliance-report/pdf?from=2026-04-01T00:00:00Z&to=2026-05-01T00:00:00Z" \
  -o compliance.pdf
```

建议定期（每月）由运维归档一份合规报告至独立存储。

## 7.6 C4 Db2 真实驱动接入（生产可选）

仓库默认的 `CXMTCode.Plugins.DB2_115.Db2Adapter` 是占位实现（保留 HADR STANDBY 安全校验逻辑）。如需接入真实 Db2，在 Windows Server 上：

1. 下载并安装 IBM Data Server Driver Package（`clidriver`）
2. 设置系统环境变量 `IBM_DB_HOME = C:\Program Files\IBM\IBM DATA SERVER DRIVER`
3. 在 `CXMTCode.Plugins.DB2_115.csproj` 添加：
   ```xml
   <PackageReference Include="Net.IBM.Data.Db2" Version="8.0.0.300" />
   ```
4. 在 `Db2Adapter.cs` 中按 `OracleAdapter.cs` 的实现模式调用 `DB2Connection`
5. 重新执行 `.\build\publish.ps1` + 重启 `CXMTCodeApiPool`

## 八、监控 / 健康检查

| 项 | 端点 / 命令 |
| --- | --- |
| 健康检查 | `GET http://localhost:8080/`（HTTP 200 + JSON） |
| Swagger | `GET /swagger` |
| 标准输出日志 | `publish\api\logs\stdout_*.log`（IIS ANCM stdoutLogEnabled=true） |
| 事件查看器 | `eventvwr → 应用程序`，事件源 `IIS AspNetCore Module V2` |
| 应用程序池状态 | `Get-WebAppPoolState CXMTCodeApiPool` |
| 实时回收监控 | `Get-WebsiteState cxmtcode-api` |
| 数据库 | 直接查询 Oracle 系统库 `SELECT COUNT(*) FROM CXMT_AUDIT_LOGS` |

---

## 九、故障速查

| 现象 | 可能原因 | 处理 |
| --- | --- | --- |
| 启动失败 `Could not open Sqlite database` | `publish\api\db\` 目录无写权限 | 给 `IIS_IUSRS` 授「修改」权限（或重新跑 `install-iis.ps1`） |
| HTTP 500.30 / 500.31 / 502.5 | ASP.NET Core Module 启动失败 | 看 `logs\stdout_*.log`；检查 .NET 8 Hosting Bundle 已装并重启 IIS |
| `Could not load file or assembly 'CXMTCode.Plugins.Oracle19c'` | publish 目录不完整 | 重跑 `publish.ps1`，覆盖完整 publish\api |
| /api 返回 502.3（坏网关） | URL Rewrite + ARR 未启用 / 端口被占 | IIS Manager → ARR → Server Proxy Settings 勾 Enable proxy；`netstat -ano | findstr 8080` |
| 登录返回 401 即使密码正确 | JwtSettings.SecretKey 在不同节点不一致 | 多节点部署需共享同一 SecretKey |
| API 报「角色不足」 | 当前账号角色不达接口要求 | 检查 `/api/me/profile` 返回的 role |
| 顶部横幅显示但 SysAdmin 看不到切换开关 | 当前角色非 SysAdmin | 在 `CXMT_USERS` 中确认 ROLE=3 |
| 切换环境后页面没反应 | 浏览器缓存 | Hard reload 或 `localStorage.removeItem('cxmtcode-env')` |
| DryRun 报「必须连接 ADG 只读备库」 | 备库连接串没含 STANDBY/READONLY 标识 | 在 DB 连接配置里补「备库主机 / 端口 / 服务名」 |

---

## 十、安全建议

1. JwtSettings.SecretKey 至少 32 字符，建议 64 字符以上随机串
2. Oracle 系统库 `CXMT_PLATFORM` 用户密码定期轮换
3. 所有连接走内网；IIS 站点强制 HTTPS（TLS 1.2+）；不要把后端 8080 直接暴露到公网
4. 审计日志 `CXMT_AUDIT_LOGS` 留存 ≥ 3 年（已按季度分区）
5. SM3 哈希链可用 `POST /api/audit/verify/{logId}`（SysAdmin）周期性校验
6. 国密占位实现替换为真实 GMSSL 后，须重新做安全评估

---

## 附：配置变更影响速查

| 配置 | 修改方法 | 是否需要重启 API | 是否影响其他人 |
| --- | --- | --- | --- |
| 系统 DB 连接 (SystemDb) | `appsettings.Production.json` | ✅ 需要 | ✅ 全员 |
| JWT SecretKey | `appsettings.Production.json` | ✅ 需要 | ⚠️ 全员会被强制重新登录 |
| 业务 DB 连接（增/改/删/激活） | UI / API | ❌ 不需要 | ⚠️ 该环境下当前激活连接的使用者 |
| TEST/PROD 全局切换 | UI 顶部开关（SysAdmin） | ❌ 不需要 | ✅ 全员（页面刷新） |
| 用户角色 | UI「用户管理」 | ❌ 不需要 | 仅该用户 |
| 系统配置 KV | UI「系统配置」 | ❌ 不需要（动态读） | 视 KEY 而定 |
