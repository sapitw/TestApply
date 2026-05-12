namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>配置管理中枢 - 系统配置 + 插件配置</summary>
public interface ISystemConfigService
{
    Task<string?> GetAsync(string key);
    Task<T?> GetAsync<T>(string key) where T : class, new();
    Task SetAsync(string key, string value, string? updatedBy = null);
    Task<bool> ExistsAsync(string key);
    Task<IReadOnlyDictionary<string, string>> GetByGroupAsync(string group);
}
