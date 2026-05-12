using System.Text.Json.Serialization;

namespace CXMTCode.Kernel.Contracts.Enums;

public enum SqlOperationType
{
    Select = 1,
    Insert = 2,
    Update = 3,
    Delete = 4,
    Ddl = 5,
    Unknown = 99
}

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SystemEnvironment
{
    PROD = 1,
    TEST = 2
}

public enum ChangeRequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approving = 2,
    Approved = 3,
    Rejected = 4,
    Executing = 5,
    Executed = 6,
    Failed = 7,
    RolledBack = 8,
    Cancelled = 9
}

public enum PluginStatus
{
    Unloaded = 0,
    Loaded = 1,
    Initialized = 2,
    Started = 3,
    Stopped = 4,
    Failed = 5
}
