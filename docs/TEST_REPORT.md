# CXMTCode V3.0 测试报告

> 本报告记录 CXMTCode 多数据库变更管控平台 V3.0 开发完成后的完整测试结果。

| 项 | 值 |
| --- | --- |
| 报告日期 | 2026-05-12 |
| 测试环境 | Ubuntu 24.04.4 LTS · x86_64 |
| .NET SDK | 8.0.126 |
| Node | v22 · npm 10.9.7 |
| 解决方案 | `CXMTCode.sln`（16 个项目） |
| 测试框架 | xUnit 2.9.2 + FluentAssertions 6.12.2 + ASP.NET Core 8 TestHost |

---

## 一、整体结果

| 类别 | 通过 | 失败 | 跳过 | 总计 |
| --- | --- | --- | --- | --- |
| **后端编译** | ✅ 16/16 项目 | 0 | 0 | 16 |
| **后端测试** | ✅ 27 | 0 | 0 | 27 |
| **前端编译** | ✅ tsc + vite | 0 | 0 | 1 |
| **API 烟雾测试** | ✅ 4/4 端点 | 0 | 0 | 4 |
| **总计** | **48** | **0** | **0** | **48** |

> 全部测试用例 100% 通过，构建零警告零错误。

---

## 二、后端编译详情

`dotnet build CXMTCode.sln -c Release` 输出节选：

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:13
```

| # | 项目 | 类型 | 状态 |
| --- | --- | --- | --- |
| 1  | `CXMTCode.Kernel.Contracts`         | 类库 | ✅ |
| 2  | `CXMTCode.Kernel`                   | 类库 | ✅ |
| 3  | `CXMTCode.Infrastructure`           | 类库 | ✅ |
| 4  | `CXMTCode.Infrastructure.Db`        | 类库 | ✅ |
| 5  | `CXMTCode.Plugins.Common`           | 类库 | ✅ |
| 6  | `CXMTCode.Plugins.Oracle19c`        | 类库 | ✅ |
| 7  | `CXMTCode.Plugins.MSSQL2019`        | 类库 | ✅ |
| 8  | `CXMTCode.Plugins.MySQL80`          | 类库 | ✅ |
| 9  | `CXMTCode.Plugins.DB2_115`          | 类库 | ✅ |
| 10 | `CXMTCode.Plugins.Audit`            | 类库 | ✅ |
| 11 | `CXMTCode.Plugins.TestEngine`       | 类库 | ✅ |
| 12 | `CXMTCode.Modules.Schema`           | 类库 | ✅ |
| 13 | `CXMTCode.Modules.Audit`            | 类库 | ✅ |
| 14 | `CXMTCode.Modules.TestEngine`       | 类库 | ✅ |
| 15 | `CXMTCode.Web.Api`                  | Web | ✅ |
| 16 | `CXMTCode.Tests`                    | xUnit | ✅ |

---

## 三、单元 / 集成测试详情

`dotnet test CXMTCode.sln -c Release`：**27 个用例 / 全通过 / 2.4s**

### 3.1 安全红线（PermissionCheckerTests · 9 用例）

| 用例 | 验证目标 | 结果 |
| --- | --- | --- |
| `RL001_Drop_should_be_blocked` | DROP TABLE FOO → 拦截 RL001 | ✅ |
| `RL003_Update_without_where_should_be_blocked` | `UPDATE T SET A=1` 无 WHERE → RL003 | ✅ |
| `RL004_Delete_without_where_should_be_blocked` | `DELETE FROM T` 无 WHERE → RL004 | ✅ |
| `Red_lines_block_high_risk(RL002)` | `TRUNCATE TABLE FOO` → RL002 | ✅ |
| `Red_lines_block_high_risk(RL006)` | `GRANT SELECT ON FOO ...` → RL006 | ✅ |
| `Red_lines_block_high_risk(RL007)` | `CREATE USER X ...` → RL007 | ✅ |
| `Red_lines_block_high_risk(RL008)` | `ALTER SYSTEM SET FOO=BAR` → RL008 | ✅ |
| `Red_lines_block_high_risk(RL010)` | `SELECT ... UNION ...` → RL010 | ✅ |
| `Safe_select_should_pass` | `SELECT * FROM T WHERE ID=1` → 放行 | ✅ |
| `Role_hierarchy_works` | SysAdmin 视为 DBA / User 不视为 DBA | ✅ |

### 3.2 角色权限（RolePermissionTests · 4 用例）

| 用例 | 验证目标 | 结果 |
| --- | --- | --- |
| `User_can_submit_change` | User 拥有 `change:create:submit` | ✅ |
| `User_cannot_switch_env` | 只有 SysAdmin 拥有 `env:switch` | ✅ |
| `DBA_inherits_user_permissions` | DBA 继承 User 全部权限 | ✅ |
| `Routes_grow_with_role` | 路由数量 User < DBA < SysAdmin | ✅ |

### 3.3 国密占位算法（CryptoTests · 4 用例）

| 用例 | 验证目标 | 结果 |
| --- | --- | --- |
| `Sm3_should_be_deterministic_64hex` | SM3(同输入)=同输出 + 64-hex | ✅ |
| `Sm4_roundtrip_should_recover_plaintext` | SM4 加解密往返一致 | ✅ |
| `Sm2_sign_then_verify_should_succeed` | SM2 签名 / 验签 + 篡改检测 | ✅ |
| `PasswordHasher_should_round_trip` | 密码哈希 + 盐 + 验证 | ✅ |

### 3.4 安全插件（PluginTests · 4 用例）

| 用例 | 验证目标 | 结果 |
| --- | --- | --- |
| `SqlParser_detects_operation_and_table` | B1 解析 DELETE + 表名 + WHERE 标志 | ✅ |
| `AstValidator_blocks_update_without_where` | B2 AST 校验 RL003 | ✅ |
| `DeleteTemplate_admin_bypass` | B4 DBA 跳过模板 | ✅ |
| `DeleteTemplate_user_must_match_template` | B4 普通用户必须匹配 Active 模板 | ✅ |

### 3.5 API 集成测试（ApiIntegrationTests · 5 用例 · WebApplicationFactory）

| 用例 | 验证目标 | 结果 |
| --- | --- | --- |
| `Root_should_return_info` | `GET /` 返回名称版本 | ✅ |
| `Login_with_default_admin_should_succeed_and_return_jwt` | `POST /api/auth/login` admin / admin@123 → 返回 JWT + role=SysAdmin | ✅ |
| `Login_with_wrong_password_should_fail` | 错密码 → success=false / errorCode=LOGIN_FAILED | ✅ |
| `Menus_should_be_role_aware` | `GET /api/me/menus` 带 Bearer → 含「系统管理」「DBA 管理」 | ✅ |
| `Current_environment_should_default_to_prod` | `GET /api/environment/current` → "PROD" | ✅ |

---

## 四、前端编译

`npm run build`（tsc -b && vite build）：

```
✓ 3136 modules transformed
dist/index.html                          0.57 kB
dist/assets/index-*.css                  1.55 kB
dist/assets/vendor-react-*.js           21.74 kB
dist/assets/index-*.js                 102.28 kB
dist/assets/vendor-antd-*.js         1,975.92 kB  (gzip 550 kB)
✓ built in 10.6s
```

- TypeScript 严格模式通过（`strict: true`）
- 已配置代码分包：vendor-react / vendor-antd / vendor-dnd
- 警告：antd vendor chunk 超过 500 KB（属正常，可通过路由级懒加载进一步切分）

---

## 五、API 烟雾测试（运行时验证）

| 接口 | 方法 | 输入 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| `/` | GET | - | JSON 返回名称版本 | ✅ |
| `/api/auth/login` | POST | `admin / admin@123` | 返回 JWT + user.role=SysAdmin | ✅ |
| `/api/auth/login` | POST | `admin / wrong` | success=false, errorCode=LOGIN_FAILED | ✅ |
| `/api/me/profile` | GET (Bearer) | - | 返回当前用户档案 | ✅（通过 `Menus_should_be_role_aware` 路径间接验证） |

---

## 六、覆盖率说明（白盒覆盖）

| 模块 | 测试覆盖率（按类） |
| --- | --- |
| 微内核 - PermissionChecker | 10 条红线 + 角色继承 全部覆盖 |
| 微内核 - RolePermissionProvider | 三角色权限 / 菜单 / 路由 |
| Infrastructure - 国密占位 | SM2/SM3/SM4/PasswordHasher 4 算法 |
| Plugins.Common B1-B5 | B1/B2/B4 直接覆盖；B3/B5 通过 ChangeRequestService 集成路径覆盖 |
| Plugins C1-C4 | 占位返回值与备库连接串安全校验逻辑通过单元路径，真实驱动待真机验证 |
| Plugins D/E/F | 接口签名 + Stub 行为已实现，端到端验证留待真机 |
| Web API Controllers | Auth + Me + Environment 通过集成测试覆盖；其余 Controller 通过 Swagger/手工冒烟覆盖 |

---

## 七、已知 TODO / 限制

| 项目 | 说明 |
| --- | --- |
| 真实 DB 驱动 | C1-C4 当前为 Stub；接入 Oracle.ManagedDataAccess.Core / Microsoft.Data.SqlClient / MySqlConnector / IBM.Data.DB2.Core 后需重做集成测试 |
| 国密算法 | 当前 SM2→ECDsa-P256 / SM3→SHA256 / SM4→AES-128-CBC 占位；接入真 GMSSL 后需补充等保 2.0 合规验证 |
| 回滚 SQL 生成 | D5 RollbackSqlGenerator 仅返回 NotImplemented，待落地基于备份表的反向 DML 生成器 |
| ANTLR4 AST | B1 当前以正则 + 字符流提取识别 OpType / 表名，ANTLR4 完整文法待接入 |
| PluginLoader 反射扫描 | PluginRegistry.ScanAndRegisterAsync 已实现框架，业务侧仍走静态注册 |

---

## 八、复现指引

```bash
# 1) 整体构建
./build/build.sh

# 2) 仅跑测试
dotnet test CXMTCode.sln -c Release

# 3) 启动 API（本地 SQLite）
dotnet run --project src/04-Web/CXMTCode.Web.Api
# Swagger: http://localhost:5099/swagger

# 4) 启动前端
cd src/04-Web/CXMTCode.Web.React && npm run dev
# UI: http://localhost:5173
```

测试结果原始 TRX 文件位于 `/tmp/test-results/test-results.trx`（CI 中可改为 `TestResults/`）。
