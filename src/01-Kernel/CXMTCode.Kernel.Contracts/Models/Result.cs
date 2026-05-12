namespace CXMTCode.Kernel.Contracts.Models;

/// <summary>统一 API 返回结果（泛型版）</summary>
public class Result<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };
    public static Result<T> Fail(string message, string? code = null) =>
        new() { Success = false, ErrorMessage = message, ErrorCode = code };
}

/// <summary>统一返回结果（无数据版）</summary>
public class Result
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static Result Ok() => new() { Success = true };
    public static Result Fail(string message, string? code = null) =>
        new() { Success = false, ErrorMessage = message, ErrorCode = code };
}
