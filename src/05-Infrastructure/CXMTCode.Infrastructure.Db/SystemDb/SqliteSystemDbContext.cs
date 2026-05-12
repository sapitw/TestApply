using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CXMTCode.Infrastructure.Db.SystemDb;

/// <summary>
/// 默认的 SQLite 系统 DB 实现 - 用于本地开发和 CI。
/// 连接串读取自 <c>ConnectionStrings:SystemDb</c>，缺省为 <c>Data Source=db/kimicode-dev.db</c>。
/// </summary>
public sealed class SqliteSystemDbContext : ISystemDbContext
{
    public string Provider => "Sqlite";
    public DialectAdapter Dialect { get; } = new("Sqlite");

    private readonly string _connectionString;

    public SqliteSystemDbContext(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SystemDb")
                            ?? "Data Source=db/kimicode-dev.db";
        EnsureDirectory();
    }

    public DbConnection CreateOpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureDirectory()
    {
        // Data Source=...; 形式
        var parts = _connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var kv = p.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                var path = kv[1].Trim();
                if (path == ":memory:" || string.IsNullOrEmpty(path)) return;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return;
            }
        }
    }
}
