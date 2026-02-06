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
                if (sourceCode == null) continue;
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
        var needsSignalRClient = executableProjects.Any(p => p.HubEndpoint != null);

        await _fileWriter.WriteProjectDefinitionAsync(
            context.ProjectPath,
            context.ProjectName,
            executableProjects.Select(p => p.Path).ToList(),
            includeWebTestingPackage: needsWebTesting,
            includeSignalRClientPackage: needsSignalRClient);

        foreach (var projectData in executableProjects)
        {
            var sourceCode = projectData.Type == ProjectType.WebApi
                ? _templateBuilder.CreateWebApiE2ESource(projectData)
                : _templateBuilder.CreateConsoleE2ESource(projectData);

            var safeProjectName = projectData.Name.Replace(".", "");
            var outputFile = Path.Combine(context.ProjectPath, $"{safeProjectName}E2EBenchmarks.cs");
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
        bool includeWebTestingPackage,
        bool includeSignalRClientPackage = false)
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

        lines.Add("    <PackageReference Include=\"Microsoft.Extensions.Logging.Abstractions\" Version=\"9.0.0\" />");
        lines.Add("    <PackageReference Include=\"Microsoft.Extensions.Options\" Version=\"9.0.0\" />");
        lines.Add("    <PackageReference Include=\"Microsoft.Extensions.Caching.Memory\" Version=\"9.0.0\" />");

        if (includeWebTestingPackage)
        {
            lines.Add("    <PackageReference Include=\"Microsoft.AspNetCore.Mvc.Testing\" Version=\"9.0.0\" />");
        }

        if (includeSignalRClientPackage)
        {
            lines.Add("    <PackageReference Include=\"Microsoft.AspNetCore.SignalR.Client\" Version=\"9.0.0\" />");
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
    public string? CreateUnitBenchmarkSource(ClassInfo targetClass)
    {
        // Skip infrastructure classes (Hub, BackgroundService, Controller)
        if (targetClass.IsInfrastructureClass)
            return null;

        // Skip classes whose constructor parameters we cannot provide
        if (!targetClass.ConstructorParameters.All(CanProvideConstructorParameter))
            return null;

        var usings = new List<string>
        {
            "using BenchmarkDotNet.Attributes;"
        };

        if (targetClass.ConstructorParameters.Any(p => IsLoggerType(p.Type)))
            usings.Add("using Microsoft.Extensions.Logging.Abstractions;");
        if (targetClass.ConstructorParameters.Any(p => IsOptionsType(p.Type)))
            usings.Add("using Microsoft.Extensions.Options;");
        if (targetClass.ConstructorParameters.Any(p => IsMemoryCacheType(p.Type)))
            usings.Add("using Microsoft.Extensions.Caching.Memory;");

        var constructorArgs = BuildConstructorArguments(targetClass);

        var lines = new List<string>();
        lines.AddRange(usings);
        lines.Add("");
        lines.Add("namespace Benchmarks;");
        lines.Add("");
        lines.Add("[MemoryDiagnoser]");
        lines.Add($"public class {targetClass.Name}Benchmarks");
        lines.Add("{");
        lines.Add($"    private {targetClass.FullName}? _testSubject;");
        lines.Add("");
        lines.Add("    [GlobalSetup]");
        lines.Add("    public void Initialize()");
        lines.Add("    {");
        lines.Add($"        _testSubject = new {targetClass.FullName}({constructorArgs});");
        lines.Add("    }");
        lines.Add("");

        foreach (var methodData in targetClass.PublicMethods)
        {
            var isAsyncEnumerable = methodData.ReturnType.Contains("IAsyncEnumerable");

            lines.Add("    [Benchmark]");

            if (isAsyncEnumerable)
            {
                lines.Add($"    public async Task Measure_{methodData.Name}()");
                lines.Add("    {");
                var invocation = BuildMethodInvocation(methodData);
                lines.Add($"        await foreach (var _ in _testSubject!.{invocation})");
                lines.Add("        {");
                lines.Add("            break;");
                lines.Add("        }");
            }
            else if (methodData.IsAsync)
            {
                lines.Add($"    public async Task Measure_{methodData.Name}()");
                lines.Add("    {");
                var invocation = BuildMethodInvocation(methodData);
                lines.Add($"        await _testSubject!.{invocation};");
            }
            else
            {
                lines.Add($"    public void Measure_{methodData.Name}()");
                lines.Add("    {");
                var invocation = BuildMethodInvocation(methodData);
                lines.Add($"        _testSubject!.{invocation};");
            }

            lines.Add("    }");
            lines.Add("");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    public string CreateWebApiE2ESource(ProjectInfo webProject)
    {
        if (webProject.HubEndpoint != null)
        {
            return CreateSignalRE2ESource(webProject, webProject.HubEndpoint);
        }

        return CreateHttpGetE2ESource(webProject);
    }

    private string CreateHttpGetE2ESource(ProjectInfo webProject)
    {
        var anchorClass = webProject.Classes.FirstOrDefault();
        var anchorType = anchorClass?.FullName ?? $"{webProject.Name}.Program";
        var safeName = webProject.Name.Replace(".", "");

        return string.Join(Environment.NewLine, new[]
        {
            "using BenchmarkDotNet.Attributes;",
            "using Microsoft.AspNetCore.Mvc.Testing;",
            "",
            "namespace Benchmarks;",
            "",
            "[MemoryDiagnoser]",
            $"public class {safeName}E2EBenchmarks",
            "{",
            $"    private WebApplicationFactory<{anchorType}> _factory = null!;",
            "    private HttpClient _httpClient = null!;",
            "",
            "    [GlobalSetup]",
            "    public void Initialize()",
            "    {",
            $"        _factory = new WebApplicationFactory<{anchorType}>();",
            "        _httpClient = _factory.CreateClient();",
            "    }",
            "",
            "    [GlobalCleanup]",
            "    public void Cleanup()",
            "    {",
            "        _httpClient.Dispose();",
            "        _factory.Dispose();",
            "    }",
            "",
            "    [Benchmark]",
            "    public async Task MeasureRootEndpoint()",
            "    {",
            "        using var response = await _httpClient.GetAsync(\"/\");",
            "    }",
            "}"
        });
    }

    private string CreateSignalRE2ESource(ProjectInfo webProject, Models.HubEndpointInfo hub)
    {
        var anchorClass = webProject.Classes.FirstOrDefault();
        var anchorType = anchorClass?.FullName ?? $"{webProject.Name}.Program";
        var safeName = webProject.Name.Replace(".", "");

        var lines = new List<string>
        {
            "using BenchmarkDotNet.Attributes;",
            "using Microsoft.AspNetCore.Mvc.Testing;",
            "using Microsoft.AspNetCore.SignalR.Client;",
            "using Microsoft.AspNetCore.TestHost;",
            "",
            "namespace Benchmarks;",
            "",
            "[MemoryDiagnoser]",
            $"public class {safeName}E2EBenchmarks",
            "{",
            $"    private WebApplicationFactory<{anchorType}> _factory = null!;",
            "    private HubConnection _hubConnection = null!;",
            "    private TaskCompletionSource<string> _messageReceived = null!;",
            "",
            "    [GlobalSetup]",
            "    public async Task Initialize()",
            "    {",
            $"        _factory = new WebApplicationFactory<{anchorType}>();",
            "        var server = _factory.Server;",
            "        var handler = server.CreateHandler();",
            "",
            "        _hubConnection = new HubConnectionBuilder()",
            $"            .WithUrl(\"http://localhost{hub.EndpointPath}\", options =>",
            "            {",
            "                options.HttpMessageHandlerFactory = _ => handler;",
            "                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents;",
            "            })",
            "            .Build();",
            "",
            "        _messageReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);",
            "",
            $"        _hubConnection.On<string>(\"{hub.ClientCallbackMethod}\", message =>",
            "        {",
            "            _messageReceived.TrySetResult(message);",
            "        });",
            "",
            "        await _hubConnection.StartAsync();",
        };

        if (!hub.UsesClientsAll)
        {
            lines.Add($"        await _hubConnection.InvokeAsync(\"Subscribe\", \"{hub.SubscriptionTopic}\");");
        }

        lines.AddRange(new[]
        {
            "    }",
            "",
            "    [IterationSetup]",
            "    public void ResetCompletionSource()",
            "    {",
            "        _messageReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);",
            "    }",
            "",
            "    [GlobalCleanup]",
            "    public async Task Cleanup()",
            "    {",
            "        if (_hubConnection != null)",
            "        {",
            "            await _hubConnection.DisposeAsync();",
            "        }",
            "        _factory?.Dispose();",
            "    }",
            "",
            "    [Benchmark]",
            "    public async Task<string> MeasureSignalRMessageFlow()",
            "    {",
            "        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));",
            "        cts.Token.Register(() => _messageReceived.TrySetCanceled());",
            "        return await _messageReceived.Task;",
            "    }",
            "}"
        });

        return string.Join(Environment.NewLine, lines);
    }

    public string CreateConsoleE2ESource(ProjectInfo consoleProject)
    {
        var safeName = consoleProject.Name.Replace(".", "");
        return string.Join(Environment.NewLine, new[]
        {
            "using System.Diagnostics;",
            "using BenchmarkDotNet.Attributes;",
            "",
            "namespace Benchmarks;",
            "",
            "[MemoryDiagnoser]",
            $"public class {safeName}E2EBenchmarks",
            "{",
            "    private string _executablePath = null!;",
            "",
            "    [GlobalSetup]",
            "    public void Initialize()",
            "    {",
            $"        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, \"..\", \"..\", \"..\", \"..\", \"src\", \"{consoleProject.Name}\"));",
            "        _executablePath = Path.Combine(projectDir, \"bin\", \"Release\", \"net9.0\", " +
                $"\"{consoleProject.Name}.exe\");",
            "    }",
            "",
            "    [Benchmark]",
            "    public void MeasureConsoleExecution()",
            "    {",
            "        using var process = Process.Start(new ProcessStartInfo",
            "        {",
            "            FileName = _executablePath,",
            "            RedirectStandardOutput = true,",
            "            RedirectStandardError = true,",
            "            UseShellExecute = false,",
            "            CreateNoWindow = true",
            "        });",
            "        process?.WaitForExit();",
            "    }",
            "}"
        });
    }

    private string BuildConstructorArguments(ClassInfo targetClass)
    {
        if (targetClass.ConstructorParameters.Count == 0)
            return string.Empty;

        var args = targetClass.ConstructorParameters
            .Select(CreateConstructorArgument);

        return Environment.NewLine +
            string.Join("," + Environment.NewLine, args.Select(a => $"            {a}"));
    }

    private string CreateConstructorArgument(Models.ParameterInfo param)
    {
        var type = param.Type;

        if (IsLoggerType(type))
        {
            var innerType = ExtractGenericArgument(type);
            return $"Microsoft.Extensions.Logging.Abstractions.NullLogger<{innerType}>.Instance";
        }

        if (IsOptionsType(type))
        {
            var innerType = ExtractGenericArgument(type);
            return $"Options.Create(new {innerType}())";
        }

        if (IsMemoryCacheType(type))
        {
            return "new MemoryCache(new MemoryCacheOptions())";
        }

        return CreateDefaultArgument(type);
    }

    private static bool CanProvideConstructorParameter(Models.ParameterInfo param)
    {
        var type = param.Type;
        if (IsLoggerType(type)) return true;
        if (IsOptionsType(type)) return true;
        if (IsMemoryCacheType(type)) return true;
        // Simple value types
        if (type is "string" or "System.String") return true;
        if (type is "int" or "System.Int32") return true;
        if (type is "long" or "System.Int64") return true;
        if (type is "bool" or "System.Boolean") return true;
        if (type is "double" or "System.Double") return true;
        if (type is "float" or "System.Single") return true;
        if (type is "decimal" or "System.Decimal") return true;
        return false;
    }

    private static bool IsLoggerType(string type) =>
        type.StartsWith("Microsoft.Extensions.Logging.ILogger<");

    private static bool IsOptionsType(string type) =>
        type.StartsWith("Microsoft.Extensions.Options.IOptions<");

    private static bool IsMemoryCacheType(string type) =>
        type == "Microsoft.Extensions.Caching.Memory.IMemoryCache";

    private static string ExtractGenericArgument(string genericType)
    {
        var start = genericType.IndexOf('<') + 1;
        var end = genericType.LastIndexOf('>');
        return genericType.Substring(start, end - start);
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
        var isSlnx = Path.GetExtension(solutionFilePath).Equals(".slnx", StringComparison.OrdinalIgnoreCase);

        if (isSlnx)
        {
            IntegrateProjectIntoSlnx(solutionFilePath, projectFilePath);
        }
        else
        {
            IntegrateProjectViaDotnetCli(solutionFilePath, projectFilePath);
        }
    }

    private void IntegrateProjectIntoSlnx(string slnxFilePath, string projectFilePath)
    {
        var solutionDir = Path.GetDirectoryName(slnxFilePath)
            ?? throw new InvalidOperationException($"Cannot determine directory for: {slnxFilePath}");

        var relativePath = Path.GetRelativePath(solutionDir, projectFilePath).Replace('\\', '/');

        var doc = System.Xml.Linq.XDocument.Load(slnxFilePath);
        var root = doc.Root
            ?? throw new InvalidOperationException($"Invalid .slnx file: {slnxFilePath}");

        // Check if the project is already in the solution
        var alreadyExists = root.Descendants("Project")
            .Any(p => string.Equals(
                p.Attribute("Path")?.Value?.Replace('\\', '/'),
                relativePath,
                StringComparison.OrdinalIgnoreCase));

        if (!alreadyExists)
        {
            root.Add(new System.Xml.Linq.XElement("Project",
                new System.Xml.Linq.XAttribute("Path", relativePath)));
            doc.Save(slnxFilePath);
        }
    }

    private void IntegrateProjectViaDotnetCli(string solutionFilePath, string projectFilePath)
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

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to add project to solution. Exit code: {process.ExitCode}. Error: {stderr}");
        }
    }
}
