using Benchmark.Core.Models;

namespace Benchmark.Core.Services;

public class BenchmarkProjectGenerator : IBenchmarkProjectGenerator
{
    private readonly ProjectFileWriter _fileWriter;
    private readonly CodeTemplateBuilder _templateBuilder;
    private readonly SolutionManager _solutionManager;

    public BenchmarkProjectGenerator()
    {
        _fileWriter = new ProjectFileWriter();
        _templateBuilder = new CodeTemplateBuilder();
        _solutionManager = new SolutionManager();
    }

    public async Task<string> GenerateUnitBenchmarkProjectAsync(string solutionPath, List<ProjectInfo> projects)
    {
        var context = new BenchmarkGenerationContext(solutionPath, "UnitBenchmarks");
        context.EnsureDirectoryExists();

        await _fileWriter.WriteProjectDefinitionAsync(
            context.ProjectPath, 
            context.ProjectName, 
            projects.Select(p => p.Path).ToList(),
            includeWebTestingPackage: false);

        foreach (var projectData in projects)
        {
            foreach (var classData in projectData.Classes)
            {
                var sourceCode = _templateBuilder.CreateUnitBenchmarkSource(classData);
                var outputFile = Path.Combine(context.ProjectPath, $"{classData.Name}Benchmarks.cs");
                await File.WriteAllTextAsync(outputFile, sourceCode);
            }
        }

        await _fileWriter.WriteEntryPointAsync(context.ProjectPath);
        _solutionManager.IntegrateProjectIntoSolution(solutionPath, context.CsprojFilePath);

        return context.ProjectPath;
    }

    public async Task<string> GenerateE2EBenchmarkProjectAsync(string solutionPath, List<ProjectInfo> projects)
    {
        var context = new BenchmarkGenerationContext(solutionPath, "E2EBenchmarks");
        context.EnsureDirectoryExists();

        var executableProjects = projects.Where(p => p.Type != ProjectType.ClassLibrary).ToList();
        var needsWebTesting = executableProjects.Any(p => p.Type == ProjectType.WebApi);

        await _fileWriter.WriteProjectDefinitionAsync(
            context.ProjectPath,
            context.ProjectName,
            executableProjects.Select(p => p.Path).ToList(),
            includeWebTestingPackage: needsWebTesting);

        foreach (var projectData in executableProjects)
        {
            var sourceCode = projectData.Type == ProjectType.WebApi
                ? _templateBuilder.CreateWebApiE2ESource(projectData)
                : _templateBuilder.CreateConsoleE2ESource(projectData);

            var outputFile = Path.Combine(context.ProjectPath, $"{projectData.Name}E2EBenchmarks.cs");
            await File.WriteAllTextAsync(outputFile, sourceCode);
        }

        await _fileWriter.WriteEntryPointAsync(context.ProjectPath);
        _solutionManager.IntegrateProjectIntoSolution(solutionPath, context.CsprojFilePath);

        return context.ProjectPath;
    }
}

internal class BenchmarkGenerationContext
{
    public string SolutionPath { get; }
    public string ProjectName { get; }
    public string ProjectPath { get; }
    public string CsprojFilePath { get; }

    public BenchmarkGenerationContext(string solutionPath, string suffix)
    {
        SolutionPath = solutionPath;
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        ProjectName = $"{solutionName}.{suffix}";
        
        var solutionDirectory = Path.GetDirectoryName(solutionPath) 
            ?? throw new ArgumentException("Invalid solution path");
        ProjectPath = Path.Combine(solutionDirectory, ProjectName);
        CsprojFilePath = Path.Combine(ProjectPath, $"{ProjectName}.csproj");
    }

    public void EnsureDirectoryExists()
    {
        Directory.CreateDirectory(ProjectPath);
    }
}

internal class ProjectFileWriter
{
    public async Task WriteProjectDefinitionAsync(
        string targetDirectory, 
        string projectName, 
        List<string> referencedProjectPaths,
        bool includeWebTestingPackage)
    {
        var lines = new List<string>
        {
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "  <PropertyGroup>",
            "    <OutputType>Exe</OutputType>",
            "    <TargetFramework>net9.0</TargetFramework>",
            "    <ImplicitUsings>enable</ImplicitUsings>",
            "    <Nullable>enable</Nullable>",
            "  </PropertyGroup>",
            "",
            "  <ItemGroup>",
            "    <PackageReference Include=\"BenchmarkDotNet\" Version=\"0.14.0\" />"
        };

        if (includeWebTestingPackage)
        {
            lines.Add("    <PackageReference Include=\"Microsoft.AspNetCore.Mvc.Testing\" Version=\"9.0.0\" />");
        }

        lines.Add("  </ItemGroup>");
        lines.Add("");
        lines.Add("  <ItemGroup>");

        foreach (var referencePath in referencedProjectPaths)
        {
            var relativePath = CalculateRelativePath(targetDirectory, referencePath);
            lines.Add($"    <ProjectReference Include=\"{relativePath}\" />");
        }

        lines.Add("  </ItemGroup>");
        lines.Add("</Project>");

        var outputPath = Path.Combine(targetDirectory, $"{projectName}.csproj");
        await File.WriteAllLinesAsync(outputPath, lines);
    }

    public async Task WriteEntryPointAsync(string targetDirectory)
    {
        var entryPointCode = new[]
        {
            "using BenchmarkDotNet.Running;",
            "",
            "BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);"
        };

        var outputPath = Path.Combine(targetDirectory, "Program.cs");
        await File.WriteAllLinesAsync(outputPath, entryPointCode);
    }

    private string CalculateRelativePath(string fromDirectory, string toFile)
    {
        var fromUri = new Uri(fromDirectory + Path.DirectorySeparatorChar);
        var toUri = new Uri(toFile);
        var relativeUri = fromUri.MakeRelativeUri(toUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }
}

internal class CodeTemplateBuilder
{
    public string CreateUnitBenchmarkSource(ClassInfo targetClass)
    {
        var lines = new List<string>
        {
            "using BenchmarkDotNet.Attributes;",
            $"using {targetClass.Namespace};",
            "",
            "namespace Benchmarks;",
            "",
            "[MemoryDiagnoser]",
            $"public class {targetClass.Name}Benchmarks",
            "{",
            $"    private {targetClass.FullName}? _testSubject;",
            "",
            "    [GlobalSetup]",
            "    public void Initialize()",
            "    {",
            $"        _testSubject = new {targetClass.FullName}();",
            "    }",
            ""
        };

        foreach (var methodData in targetClass.PublicMethods)
        {
            lines.Add("    [Benchmark]");
            var returnDeclaration = methodData.IsAsync ? "async Task" : "void";
            lines.Add($"    public {returnDeclaration} Measure_{methodData.Name}()");
            lines.Add("    {");

            var invocation = BuildMethodInvocation(methodData);
            var callLine = methodData.IsAsync 
                ? $"        await _testSubject!.{invocation};"
                : $"        _testSubject!.{invocation};";
            
            lines.Add(callLine);
            lines.Add("    }");
            lines.Add("");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    public string CreateWebApiE2ESource(ProjectInfo webProject)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "using BenchmarkDotNet.Attributes;",
            "using Microsoft.AspNetCore.Mvc.Testing;",
            "",
            "namespace Benchmarks;",
            "",
            "[MemoryDiagnoser]",
            $"public class {webProject.Name}E2EBenchmarks",
            "{",
            "    private HttpClient? _httpClient;",
            "",
            "    [GlobalSetup]",
            "    public void Initialize()",
            "    {",
            $"        // TODO: Configure WebApplicationFactory with your Program class from {webProject.Name}",
            $"        // var testFactory = new WebApplicationFactory<Program>();",
            $"        // _httpClient = testFactory.CreateClient();",
            "    }",
            "",
            "    [Benchmark]",
            "    public async Task MeasureApiEndpoint()",
            "    {",
            "        // TODO: Update with your actual API endpoint",
            "        // var result = await _httpClient!.GetAsync(\"/api/health\");",
            "        // result.EnsureSuccessStatusCode();",
            "        await Task.CompletedTask;",
            "    }",
            "}"
        });
    }

    public string CreateConsoleE2ESource(ProjectInfo consoleProject)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "using BenchmarkDotNet.Attributes;",
            "",
            "namespace Benchmarks;",
            "",
            "[MemoryDiagnoser]",
            $"public class {consoleProject.Name}E2EBenchmarks",
            "{",
            "    [Benchmark]",
            "    public void MeasureConsoleApp()",
            "    {",
            $"        // TODO: Add console application execution logic for {consoleProject.Name}",
            "    }",
            "}"
        });
    }

    private string BuildMethodInvocation(Models.MethodInfo methodData)
    {
        if (!methodData.Parameters.Any())
        {
            return $"{methodData.Name}()";
        }

        var argumentValues = methodData.Parameters.Select(p => CreateDefaultArgument(p.Type));
        return $"{methodData.Name}({string.Join(", ", argumentValues)})";
    }

    private string CreateDefaultArgument(string typeIdentifier)
    {
        // Match exact type names to avoid false positives
        var normalizedType = typeIdentifier.Trim();
        
        if (normalizedType == "string" || normalizedType == "System.String") return "\"sample\"";
        if (normalizedType == "int" || normalizedType == "System.Int32") return "42";
        if (normalizedType == "long" || normalizedType == "System.Int64") return "42L";
        if (normalizedType == "bool" || normalizedType == "System.Boolean") return "true";
        if (normalizedType == "double" || normalizedType == "System.Double") return "42.0";
        if (normalizedType == "float" || normalizedType == "System.Single") return "42.0f";
        if (normalizedType == "decimal" || normalizedType == "System.Decimal") return "42m";
        if (normalizedType == "DateTime" || normalizedType == "System.DateTime") return "DateTime.UtcNow";
        if (normalizedType == "Guid" || normalizedType == "System.Guid") return "Guid.NewGuid()";
        
        return "default";
    }
}

internal class SolutionManager
{
    public void IntegrateProjectIntoSolution(string solutionFilePath, string projectFilePath)
    {
        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"sln \"{solutionFilePath}\" add \"{projectFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(processInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start dotnet process");
        }
        
        process.WaitForExit();
        
        if (process.ExitCode != 0)
        {
            var errorOutput = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"Failed to add project to solution. Exit code: {process.ExitCode}. Error: {errorOutput}");
        }
    }
}
