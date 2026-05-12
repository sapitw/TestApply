using CXMTCode.Infrastructure.Db.SystemDb;
using Microsoft.Extensions.Configuration;

namespace CXMTCode.Tests;

/// <summary>
/// 测试用 SystemDb 帮助类 - 使用临时文件 SQLite，避免 :memory: 多次连接看不到同一份数据的问题。
/// </summary>
internal static class TestDbHelper
{
    public static async Task<ISystemDbContext> NewSqliteContextAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cxmt-test-{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SystemDb"] = $"Data Source={path}"
            })
            .Build();
        var ctx = new SqliteSystemDbContext(config);
        await DbSchemaInitializer.EnsureCreatedAsync(ctx);
        return ctx;
    }
}
