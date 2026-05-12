using System.Text;
using CXMTCode.Infrastructure;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Audit;
using CXMTCode.Kernel.Configuration;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Hosting;
using CXMTCode.Kernel.PluginLoading;
using CXMTCode.Kernel.Scheduling;
using CXMTCode.Kernel.Security;
using CXMTCode.Kernel.Transaction;
using CXMTCode.Modules.Schema.Repositories;
using CXMTCode.Modules.Schema.Services;
using CXMTCode.Plugins.Common.Adapters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ============ JWT 认证 ============
var jwt    = builder.Configuration.GetSection("JwtSettings");
var secret = Encoding.UTF8.GetBytes(jwt["SecretKey"] ?? "DevSecretKeyMustBeAtLeast32CharsLong!");
builder.Services.AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwt["Issuer"]   ?? "CXMTCode",
        ValidAudience            = jwt["Audience"] ?? "CXMTCodeReact",
        IssuerSigningKey         = new SymmetricSecurityKey(secret),
        ClockSkew                = TimeSpan.FromMinutes(5)
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());
    o.AddPolicy("DBA",      p => p.RequireRole(nameof(UserRole.DBA), nameof(UserRole.SysAdmin)));
    o.AddPolicy("SysAdmin", p => p.RequireRole(nameof(UserRole.SysAdmin)));
});

// ============ CORS ============
builder.Services.AddCors(o => o.AddPolicy("AllowReactApp", p => p
    .WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ============ 基础设施 ============
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ISystemDbContext, SqliteSystemDbContext>();
builder.Services.AddSingleton<SnowflakeIdGenerator>(_ => new SnowflakeIdGenerator(1));

// ============ 内核中枢 ============
builder.Services.AddSingleton<IAuditLogger, AuditLogger>();
builder.Services.AddSingleton<IRolePermissionProvider, RolePermissionProvider>();
builder.Services.AddSingleton<IKimiTaskScheduler, KimiTaskScheduler>();
builder.Services.AddSingleton<IDistributedTransaction, DistributedTransactionManager>();
builder.Services.AddSingleton<ISystemConfigService, SystemConfigService>();
builder.Services.AddScoped<IPermissionChecker, PermissionChecker>();
builder.Services.AddScoped<IUserContext>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var ip   = http?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return http?.User?.Identity?.IsAuthenticated == true
        ? UserContext.FromClaimsPrincipal(http.User, ip)
        : UserContext.Anonymous(ip);
});
builder.Services.AddSingleton<PluginContextFactory>();
builder.Services.AddSingleton<IPluginHost, PluginHost>();
builder.Services.AddSingleton<PluginRegistry>();

// ============ 数据库适配工厂 ============
builder.Services.AddSingleton(_ => new DatabaseAdapterFactory()
    .Register(DatabaseType.Oracle19c, () => new CXMTCode.Plugins.Oracle19c.OracleAdapter())
    .Register(DatabaseType.MsSql2019, () => new CXMTCode.Plugins.MSSQL2019.MsSqlAdapter())
    .Register(DatabaseType.MySql80,   () => new CXMTCode.Plugins.MySQL80.MySqlAdapter())
    .Register(DatabaseType.Db2_115,   () => new CXMTCode.Plugins.DB2_115.Db2Adapter()));

// ============ Repository ============
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<DbConnectionRepository>();
builder.Services.AddSingleton<ChangeRequestRepository>();
builder.Services.AddSingleton<TableAccessRepository>();
builder.Services.AddSingleton<DeleteTemplateRepository>();
builder.Services.AddSingleton<WhitelistRuleRepository>();

// ============ 业务服务 ============
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<ChangeRequestService>();
builder.Services.AddSingleton<EnvironmentService>();

// ============ 控制器 + Swagger ============
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy   = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v3.0", new OpenApiInfo { Title = "CXMTCode API", Version = "v3.0" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT 鉴权：Bearer {token}",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [ new OpenApiSecurityScheme { Reference = new() { Id = "Bearer", Type = ReferenceType.SecurityScheme } } ] = Array.Empty<string>()
    });
});

var app = builder.Build();

// ============ 启动期初始化 ============
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ISystemDbContext>();
    await DbSchemaInitializer.EnsureCreatedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Json(new
{
    name    = "CXMTCode API",
    version = "3.0.0",
    docs    = "/swagger"
}));

app.Run();

/// <summary>Public partial Program 类 - 用于 WebApplicationFactory&lt;Program&gt; 集成测试</summary>
public partial class Program { }

