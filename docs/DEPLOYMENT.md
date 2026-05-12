# CXMTCode V3.0 部署说明书

> 本文档说明如何在「开发机 / 测试环境 / 生产环境」三种场景下部署 CXMTCode，以及如何修改各类数据库连接配置。

---

## 一、部署模式速查

| 模式 | 系统 DB | 业务 DB | 适用场景 |
| --- | --- | --- | --- |
| **A. 本地开发** | SQLite（自动创建） | 可空 / 测试库 | 单机调试、UI 演示 |
| **B. Docker 单节点** | SQLite（容器卷） | 测试 Oracle / MSSQL | POC、内网试运行 |
| **C. 生产 - Oracle 系统库** | Oracle 19C Enterprise | Oracle / MSSQL / MySQL / DB2 | 企业正式部署 |

---

## 二、模式 A：本地开发（最快上手）

> 适合：开发调试、UI 演示。无需 Docker，无需 Oracle。

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

## 三、模式 B：Docker 单节点

> 适合：内网试运行、POC。一键起前后端 + SQLite 系统库。

### 3.1 启动

```bash
git clone <仓库地址> CXMTCode
cd CXMTCode
docker compose up --build -d
```

访问：
- 前端：<http://localhost>
- 后端：<http://localhost:8080>
- Swagger：<http://localhost:8080/swagger>

### 3.2 修改配置

`docker-compose.yml` 中 `cxmtcode-api.environment` 节：

```yaml
environment:
  ConnectionStrings__SystemDb: "Data Source=/data/cxmtcode-prod.db"
  JwtSettings__SecretKey: "请修改成 32 字符以上的随机串"
  JwtSettings__Issuer: "CXMTCode"
  JwtSettings__Audience: "CXMTCodeReact"
  JwtSettings__ExpirationMinutes: "480"
```

修改后：

```bash
docker compose down && docker compose up -d
```

### 3.3 持久化

`cxmtcode_db` 命名卷持久化 SQLite 文件。备份：

```bash
docker run --rm -v cxmtcode_db:/data -v $(pwd):/backup alpine \
  tar czf /backup/cxmtcode-db-$(date +%Y%m%d).tar.gz -C /data .
```

---

## 四、模式 C：生产部署（Oracle 19C 系统库）

> 适合：等保 2.0 合规、企业正式部署。

### 4.1 步骤总览

```
1. 安装 / 准备 Oracle 19C Enterprise
2. 执行 db/oracle-init.sql 建库 + 建用户 CXMT_PLATFORM
3. 修改 appsettings.Production.json 的 ConnectionStrings:SystemDb
4. 修改 SecretKey + Issuer
5. 后端：dotnet publish / docker
6. 前端：npm run build → Nginx 静态托管 / Docker
7. 反向代理（Nginx / 网关）配置
8. 首次登录后立刻修改 admin 密码
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
  "Frontend": { "Url": "https://cxmtcode.your-domain.com" }
}
```

> **接入 Oracle 提醒**：当前仓库内的 `SystemDbContext` 默认通过 `SqliteSystemDbContext` 实现，生产 Oracle 需补一个 `OracleSystemDbContext`（使用 `Oracle.ManagedDataAccess.Core`）并在 `Program.cs` 注册：
>
> ```csharp
> builder.Services.AddSingleton<ISystemDbContext, OracleSystemDbContext>();
> ```
>
> 这是规格里明确要求但当前仓库标记为 TODO 的部分。

### 4.4 后端发布

```bash
dotnet publish src/04-Web/CXMTCode.Web.Api -c Release -o /opt/cxmtcode/api --no-restore
sudo systemctl edit --force --full cxmtcode-api  # 写入 systemd unit
sudo systemctl enable --now cxmtcode-api
```

systemd unit 示例：

```ini
[Unit]
Description=CXMTCode API
After=network.target

[Service]
WorkingDirectory=/opt/cxmtcode/api
ExecStart=/usr/bin/dotnet /opt/cxmtcode/api/CXMTCode.Web.Api.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://+:8080
Restart=always

[Install]
WantedBy=multi-user.target
```

### 4.5 前端发布

```bash
cd src/04-Web/CXMTCode.Web.React
npm install
npm run build
sudo cp -r dist/* /var/www/cxmtcode/
```

Nginx 配置：

```nginx
server {
  listen 443 ssl http2;
  server_name cxmtcode.your-domain.com;

  ssl_certificate     /etc/letsencrypt/live/cxmtcode/fullchain.pem;
  ssl_certificate_key /etc/letsencrypt/live/cxmtcode/privkey.pem;

  root /var/www/cxmtcode;
  index index.html;

  location / {
    try_files $uri /index.html;
  }

  location /api/ {
    proxy_pass http://127.0.0.1:8080;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Authorization $http_authorization;
  }
}
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
4. 重启 API：`systemctl restart cxmtcode-api` 或 `docker compose restart cxmtcode-api`

**修改 Oracle 密码 / 主机**：

1. 在 Oracle 中改密码：`ALTER USER CXMT_PLATFORM IDENTIFIED BY "新密码";`
2. 更新 `appsettings.Production.json` 的连接串
3. 重启 API

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

```bash
# 备份
docker run --rm -v cxmtcode_db:/data -v $(pwd):/b alpine tar czf /b/db-$(date +%F).tar.gz -C /data .
git pull
docker compose up --build -d
```

如有 DDL 变更：

```bash
sqlplus / as sysdba @db/upgrade-2026Q3.sql  # 由 DBA 提供升级脚本
```

### 7.2 回滚

```bash
git checkout <旧版本 tag>
docker compose up --build -d
# 必要时还原 DB 备份
docker run --rm -v cxmtcode_db:/data -v $(pwd):/b alpine tar xzf /b/db-YYYY-MM-DD.tar.gz -C /data
```

---

## 八、监控 / 健康检查

| 项 | 端点 / 命令 |
| --- | --- |
| 健康检查 | `GET /`（HTTP 200 + JSON） |
| Swagger | `GET /swagger`（仅 Development） |
| 日志 | `journalctl -u cxmtcode-api -f` 或 `docker compose logs -f cxmtcode-api` |
| 数据库 | 直接查询 Oracle 系统库 `SELECT COUNT(*) FROM CXMT_AUDIT_LOGS` |

---

## 九、故障速查

| 现象 | 可能原因 | 处理 |
| --- | --- | --- |
| 启动失败 `Could not open Sqlite database` | `db/` 目录无写权限 | `chmod 770 db/` 或检查 Docker 卷权限 |
| 登录返回 401 即使密码正确 | JwtSettings.SecretKey 在不同节点不一致 | 多节点部署需共享同一 SecretKey |
| API 报「角色不足」 | 当前账号角色不达接口要求 | 检查 `/api/me/profile` 返回的 role |
| 顶部横幅显示但 SysAdmin 看不到切换开关 | 当前角色非 SysAdmin | 在 `CXMT_USERS` 中确认 ROLE=3 |
| 切换环境后页面没反应 | 浏览器缓存 | Hard reload 或 `localStorage.removeItem('cxmtcode-env')` |
| DryRun 报「必须连接 ADG 只读备库」 | 备库连接串没含 STANDBY/READONLY 标识 | 在 DB 连接配置里补「备库主机 / 端口 / 服务名」 |

---

## 十、安全建议

1. JwtSettings.SecretKey 至少 32 字符，建议 64 字符以上随机串
2. Oracle 系统库 `CXMT_PLATFORM` 用户密码定期轮换
3. 所有连接走内网；外暴 Nginx 强制 TLS 1.2+
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
