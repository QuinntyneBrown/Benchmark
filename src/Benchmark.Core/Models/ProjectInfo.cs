namespace Benchmark.Core.Models;

public class ProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<ClassInfo> Classes { get; set; } = new();
    public ProjectType Type { get; set; }
}

public enum ProjectType
{
    Unknown,
    WebApi,
    Console,
    ClassLibrary
}
