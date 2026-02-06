namespace Benchmark.Core.Models;

public class ClassInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<MethodInfo> PublicMethods { get; set; } = new();
    public List<ParameterInfo> ConstructorParameters { get; set; } = new();
    public bool IsPublic { get; set; }
    public bool IsStatic { get; set; }
    public bool IsInfrastructureClass { get; set; }
}
