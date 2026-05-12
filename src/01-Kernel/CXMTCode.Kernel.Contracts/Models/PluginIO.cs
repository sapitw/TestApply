namespace CXMTCode.Kernel.Contracts.Models;

/// <summary>插件输入</summary>
public class PluginInput
{
    public Dictionary<string, object?> Parameters { get; set; } = new();

    public T? GetParameter<T>(string key)
    {
        if (!Parameters.TryGetValue(key, out var v) || v is null) return default;
        if (v is T t) return t;
        try { return (T)Convert.ChangeType(v, typeof(T)); }
        catch { return default; }
    }

    public PluginInput Set(string key, object? value)
    {
        Parameters[key] = value;
        return this;
    }
}

/// <summary>插件输出</summary>
public class PluginOutput
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }

    public static PluginOutput Ok(object? data = null) => new() { Success = true, Data = data };
    public static PluginOutput Fail(string message) => new() { Success = false, ErrorMessage = message };
}
