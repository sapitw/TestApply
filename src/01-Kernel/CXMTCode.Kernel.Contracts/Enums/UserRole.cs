using System.Text.Json.Serialization;

namespace CXMTCode.Kernel.Contracts.Enums;

/// <summary>RBAC 三角色：层级继承 SysAdmin(3) ⊃ DBA(2) ⊃ User(1)</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    User = 1,
    DBA = 2,
    SysAdmin = 3
}
