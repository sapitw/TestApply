using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Kernel.Contracts.Models;

public class MenuConfig
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<MenuConfig>? Children { get; set; }
    public UserRole? RequiredRole { get; set; }
}
