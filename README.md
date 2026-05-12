# CXMTCode 多数据库变更管控平台 (V3.0)

> 半导体行业多数据库变更管控平台 · 微内核 + 插件化架构 · RBAC 三角色 · 国密合规 · TEST/PROD 双环境

## 技术栈

| 层 | 技术 |
| --- | --- |
| 后端 | C# 12 · .NET 8.0 LTS · ASP.NET Core · Dapper |
| 前端 | React 18 · TypeScript 5 · Ant Design X · dnd-kit · Zustand · Vite |
| 系统 DB | Oracle 19C Enterprise（生产）/ SQLite（开发） |
| 业务 DB | Oracle 19C / MSSQL 2019 / MySQL 8.0+ / DB2 11.5~12.1 |
| 安全 | JWT · 国密 SM2/SM3/SM4（占位实现）· 等保 2.0 三级 |
| 部署 | Docker · docker-compose · Nginx |

## 仓库布局

```
CXMTCode/
├── CXMTCode.sln                            # 解决方案
├── Directory.Build.props                   # 全局 MSBuild 属性
├── docker-compose.yml                      # 一键部署
├── .env.example                            # 环境变量示例
├── build/build.sh                          # 一键构建脚本
├── db/oracle-init.sql                      # Oracle 19C 初始化 DDL
├── docs/
│   ├── TEST_REPORT.md                      # 测试报告
│   └── DEPLOYMENT.md                       # 部署说明
├── src/
│   ├── 01-Kernel/
│   │   ├── CXMTCode.Kernel.Contracts/      # 接口、枚举、模型
│   │   └── CXMTCode.Kernel/                # 6 大中枢实现
│   ├── 02-Plugins/
│   │   ├── CXMTCode.Plugins.Common/        # B1-B5 安全 + D1-D7 执行 + 适配抽象
│   │   ├── CXMTCode.Plugins.Oracle19c/     # C1 Oracle 适配
│   │   ├── CXMTCode.Plugins.MSSQL2019/     # C2 MSSQL 适配
│   │   ├── CXMTCode.Plugins.MySQL80/       # C3 MySQL 适配
│   │   ├── CXMTCode.Plugins.DB2_115/       # C4 DB2 适配
│   │   ├── CXMTCode.Plugins.Audit/         # E1-E7 流程审计
│   │   └── CXMTCode.Plugins.TestEngine/    # F1-F5 测试引擎
│   ├── 03-Modules/
│   │   ├── CXMTCode.Modules.Schema/        # 用户/连接/变更/模板/规则
│   │   ├── CXMTCode.Modules.Audit/         # 审计查询
│   │   └── CXMTCode.Modules.TestEngine/    # 测试管理
│   ├── 04-Web/
│   │   ├── CXMTCode.Web.Api/               # ASP.NET Core Web API
│   │   └── CXMTCode.Web.React/             # React 前端
│   └── 05-Infrastructure/
│       ├── CXMTCode.Infrastructure/        # 国密 / 雪花算法
│       └── CXMTCode.Infrastructure.Db/     # Dapper / SystemDbContext
└── tests/
    └── CXMTCode.Tests/                     # xUnit 单元 + 集成测试
```

## 快速启动（本地开发）

### 后端

```bash
# 1) 复制环境变量
cp .env.example .env

# 2) 启动 API（默认 SQLite，自动建表 + 注入 admin 账号）
dotnet run --project src/04-Web/CXMTCode.Web.Api
#   API: http://localhost:5099
#   Swagger: http://localhost:5099/swagger
```

### 前端

```bash
cd src/04-Web/CXMTCode.Web.React
npm install
npm run dev
#   UI: http://localhost:5173
```

默认账号：`admin / admin@123`（首次登录会用 SM3 哈希存储真实密码）

## 一键构建

```bash
./build/build.sh
# 会依次：
#   1. dotnet build CXMTCode.sln -c Release
#   2. dotnet test  CXMTCode.sln -c Release（27/27 全通过）
#   3. npm install + vite build
```

## Docker 部署

```bash
docker compose up --build -d
# 访问：
#   前端：http://localhost
#   后端：http://localhost:8080
#   Swagger：http://localhost:8080/swagger
```

详细生产部署与 Oracle 19C 连接配置见 [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)。

## 权限模型（RBAC 三角色）

| 角色 | 范围 |
| --- | --- |
| 普通用户 User (1) | 变更申请、预演、执行/回滚自己申请、查看自己审计 |
| DBA 管理员 (2) | 继承 User + 表准入 / DELETE 模板 / 白名单 / DB 连接配置 / 全部审计 |
| 系统管理员 (3) | 继承 DBA + 用户角色分配 / TEST↔PROD 环境切换 / 测试管理 / 系统配置 |

层级继承通过 `(int)current >= (int)required` 在 `UserContext.HasRole` 中实现。

## 10 条硬编码安全红线（不可关闭）

| 编号 | 规则 |
| --- | --- |
| RL001 | 禁止 DROP |
| RL002 | 禁止 TRUNCATE |
| RL003 | UPDATE 必须带 WHERE |
| RL004 | DELETE 必须带 WHERE |
| RL005 | 禁止访问审计日志表 |
| RL006 | 禁止 GRANT / REVOKE |
| RL007 | 禁止 CREATE USER |
| RL008 | 禁止 ALTER SYSTEM |
| RL009 | 禁止可疑注释注入 |
| RL010 | 禁止 UNION |

## V3.0 已完成（阶段二）

- [x] **真实 SM2/SM3/SM4 国密算法**（BouncyCastle 2.4.0，含 GMT 0004 国标向量验证）
- [x] **C1 Oracle 真实驱动**（Oracle.ManagedDataAccess.Core 23.6.1，ADG 备库强制）
- [x] **C2 MSSQL 真实驱动**（Microsoft.Data.SqlClient 5.2.2，AlwaysOn ReadOnly 强制）
- [x] **C3 MySQL 真实驱动**（MySqlConnector 2.3.7，GR Secondary 校验）
- [x] **D5 回滚 SQL 生成器**（INSERT→DELETE MINUS/EXCEPT、UPDATE→UPDATE SET FROM、DELETE→INSERT SELECT）
- [x] **增强 AST 分析器**（去引号注释、提取 JOIN 表 / SET 列 / INSERT 列 / VALUES 分组）
- [x] **PluginRegistry DI 感知反射扫描**（启动期自动注册插件）
- [x] **测试引擎 F1-F5**（3 表 + 套件/用例/执行 CRUD + 门禁 + 前端完整页）
- [x] **表级用户授权 CRUD**（后端 4 API + 前端 UserGrants 页）
- [x] **21CFR Part11 合规报告**（HTML + PDF via QuestPDF + 哈希链抽样验证）
- [x] **D7 邮件通知**（System.Net.Mail.SmtpClient，可配置 Smtp:Enabled 开关）

## 仍待生产环境补全

- [ ] **C4 DB2 真实驱动**：需要 IBM clidriver 原生库 + 许可证；Dockerfile 注释已写明 RUN 步骤
- [ ] **E3 真实证书签名**：当前 BouncyCastle SM2 占位密钥；接入企业 PKI（USBKey / HSM）需运维侧
- [ ] **测试 Runner 真实执行**：F1-F5 占位为「全部通过」；接入 dotnet test / k6 / NBomber 后替换 `TestSuiteService.TriggerRunAsync`

## 文档索引

- 系统架构总览：根据 KimiCode 规格 V3.0（已重命名为 CXMTCode）
- 测试报告：[docs/TEST_REPORT.md](docs/TEST_REPORT.md)
- 部署说明：[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)
- API 文档：启动后访问 `/swagger`
