using System.Xml.Linq;
using Benchmark.Core.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using ProjectInfoModel = Benchmark.Core.Models.ProjectInfo;

namespace Benchmark.Core.Services;

public class SolutionAnalyzer : ISolutionAnalyzer
{
    private readonly object _lockObj = new();
    private bool _hasInitializedMsBuild;

    public async Task<List<ProjectInfoModel>> AnalyzeSolutionAsync(string solutionPath)
    {
        EnsureMsBuildInitialization();
        ValidateSolutionExists(solutionPath);

        var isSlnx = Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase);

        using var workspaceInstance = MSBuildWorkspace.Create();
        var discoveredProjects = new List<ProjectInfoModel>();

        if (isSlnx)
        {
            var projectPaths = ParseSlnxProjectPaths(solutionPath);
            foreach (var projectPath in projectPaths)
            {
                var loadedProject = await workspaceInstance.OpenProjectAsync(projectPath);
                var extractedInfo = await ExtractProjectDetailsAsync(loadedProject);
                if (extractedInfo != null)
                {
                    discoveredProjects.Add(extractedInfo);
                }
            }
        }
        else
        {
            var loadedSolution = await workspaceInstance.OpenSolutionAsync(solutionPath);
            foreach (var currentProject in loadedSolution.Projects)
            {
                var extractedInfo = await ExtractProjectDetailsAsync(currentProject);
                if (extractedInfo != null)
                {
                    discoveredProjects.Add(extractedInfo);
                }
            }
        }

        return discoveredProjects;
    }

    private static List<string> ParseSlnxProjectPaths(string slnxPath)
    {
        var solutionDir = Path.GetDirectoryName(slnxPath)
            ?? throw new InvalidOperationException($"Cannot determine directory for: {slnxPath}");

        var doc = XDocument.Load(slnxPath);
        var projectPaths = new List<string>();

        foreach (var projectElement in doc.Descendants("Project"))
        {
            var pathAttr = projectElement.Attribute("Path")?.Value;
            if (!string.IsNullOrEmpty(pathAttr))
            {
                var fullPath = Path.GetFullPath(Path.Combine(solutionDir, pathAttr));
                if (File.Exists(fullPath))
                {
                    projectPaths.Add(fullPath);
                }
            }
        }

        return projectPaths;
    }

    private void EnsureMsBuildInitialization()
    {
        lock (_lockObj)
        {
            if (_hasInitializedMsBuild) return;
            MSBuildLocator.RegisterDefaults();
            _hasInitializedMsBuild = true;
        }
    }

    private static void ValidateSolutionExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Cannot locate solution at: {filePath}");
        }

        var extension = Path.GetExtension(filePath);
        if (!extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported solution file format '{extension}'. Expected .sln or .slnx: {filePath}");
        }
    }

    private async Task<ProjectInfoModel?> ExtractProjectDetailsAsync(Project projectToAnalyze)
    {
        var builtCompilation = await projectToAnalyze.GetCompilationAsync();
        if (builtCompilation == null) return null;

        var discoveredInfo = new ProjectInfoModel
        {
            Name = projectToAnalyze.Name,
            Path = projectToAnalyze.FilePath ?? string.Empty,
            Type = InferProjectCategory(projectToAnalyze)
        };

        await PopulateClassDetailsAsync(builtCompilation, discoveredInfo);

        if (discoveredInfo.Type == ProjectType.WebApi)
        {
            discoveredInfo.HubEndpoint = await ExtractHubEndpointAsync(builtCompilation);
        }

        return discoveredInfo.Classes.Any() ? discoveredInfo : null;
    }

    private async Task PopulateClassDetailsAsync(Compilation targetCompilation, ProjectInfoModel container)
    {
        foreach (var currentTree in targetCompilation.SyntaxTrees)
        {
            var treeRoot = await currentTree.GetRootAsync();
            var publicClassNodes = treeRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(classNode => HasPublicModifier(classNode));

            foreach (var classNode in publicClassNodes)
            {
                var extractedClass = await BuildClassInfoAsync(classNode, currentTree, targetCompilation);
                if (extractedClass != null && extractedClass.PublicMethods.Any())
                {
                    container.Classes.Add(extractedClass);
                }
            }
        }
    }

    private async Task<ClassInfo?> BuildClassInfoAsync(ClassDeclarationSyntax classNode, SyntaxTree tree, Compilation compilation)
    {
        var modelForTree = compilation.GetSemanticModel(tree);
        var symbolForClass = modelForTree.GetDeclaredSymbol(classNode);

        if (symbolForClass == null) return null;

        var builtClass = new ClassInfo
        {
            Name = symbolForClass.Name,
            Namespace = symbolForClass.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            FullName = symbolForClass.ToDisplayString(),
            IsPublic = true
        };

        // Extract constructor parameters from the first public constructor
        var constructorNodes = classNode.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => HasPublicModifier(c))
            .ToList();

        if (constructorNodes.Count > 0)
        {
            var primaryCtor = constructorNodes[0];
            foreach (var param in primaryCtor.ParameterList.Parameters)
            {
                if (modelForTree.GetDeclaredSymbol(param) is IParameterSymbol paramSymbol)
                {
                    builtClass.ConstructorParameters.Add(new Models.ParameterInfo
                    {
                        Name = paramSymbol.Name,
                        Type = paramSymbol.Type.ToDisplayString()
                    });
                }
            }
        }

        // Detect infrastructure base classes (Hub, BackgroundService, Controller)
        var baseType = (symbolForClass as INamedTypeSymbol)?.BaseType;
        while (baseType != null && baseType.SpecialType == SpecialType.None)
        {
            if (baseType.Name is "Hub" or "BackgroundService" or "ControllerBase" or "Controller")
            {
                builtClass.IsInfrastructureClass = true;
                break;
            }
            baseType = baseType.BaseType;
        }

        var publicMethodNodes = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(HasPublicModifier);

        foreach (var methodNode in publicMethodNodes)
        {
            var methodDetails = ExtractMethodDetails(methodNode, modelForTree);
            if (methodDetails != null)
            {
                builtClass.PublicMethods.Add(methodDetails);
            }
        }

        return builtClass;
    }

    private Models.MethodInfo? ExtractMethodDetails(MethodDeclarationSyntax methodNode, SemanticModel model)
    {
        var methodSymbol = model.GetDeclaredSymbol(methodNode) as IMethodSymbol;
        if (methodSymbol == null) return null;

        var returnTypeName = methodSymbol.ReturnType.ToDisplayString();
        var isAsyncMethod = methodSymbol.IsAsync || returnTypeName.Contains("Task");

        var builtMethod = new Models.MethodInfo
        {
            Name = methodSymbol.Name,
            ReturnType = returnTypeName,
            IsAsync = isAsyncMethod
        };

        foreach (var paramSymbol in methodSymbol.Parameters)
        {
            builtMethod.Parameters.Add(new Models.ParameterInfo
            {
                Name = paramSymbol.Name,
                Type = paramSymbol.Type.ToDisplayString()
            });
        }

        return builtMethod;
    }

    private static bool HasPublicModifier(MemberDeclarationSyntax memberNode)
    {
        return memberNode.Modifiers.Any(modifier => modifier.Text == "public");
    }

    private async Task<HubEndpointInfo?> ExtractHubEndpointAsync(Compilation compilation)
    {
        string? hubTypeName = null;
        string? endpointPath = null;
        string? callbackMethod = null;
        string? subscriptionTopic = null;
        bool foundSendPattern = false;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = await tree.GetRootAsync();
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                // Look for app.MapHub<T>("/path")
                if (hubTypeName == null)
                {
                    var mapHubResult = ExtractMapHubInvocation(invocation, compilation, tree);
                    if (mapHubResult != null)
                    {
                        hubTypeName = mapHubResult.Value.hubType;
                        endpointPath = mapHubResult.Value.path;
                    }
                }

                // Look for .SendAsync("MethodName", ...) on Clients.Group or Clients.All
                if (!foundSendPattern)
                {
                    var sendResult = ExtractSignalRSendInfo(invocation);
                    if (sendResult != null)
                    {
                        callbackMethod = sendResult.Value.callback;
                        subscriptionTopic = sendResult.Value.topic;
                        foundSendPattern = true;
                    }
                }

                if (hubTypeName != null && foundSendPattern) break;
            }

            if (hubTypeName != null && foundSendPattern) break;
        }

        if (hubTypeName == null || endpointPath == null || callbackMethod == null)
            return null;

        return new HubEndpointInfo
        {
            HubClassName = hubTypeName,
            EndpointPath = endpointPath,
            ClientCallbackMethod = callbackMethod,
            SubscriptionTopic = subscriptionTopic
        };
    }

    private (string hubType, string path)? ExtractMapHubInvocation(
        InvocationExpressionSyntax invocation, Compilation compilation, SyntaxTree tree)
    {
        // Match pattern: .MapHub<T>("path")
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        if (!memberAccess.Name.ToString().StartsWith("MapHub"))
            return null;

        // Extract the generic type argument
        if (memberAccess.Name is not GenericNameSyntax genericName)
            return null;

        if (genericName.TypeArgumentList.Arguments.Count == 0)
            return null;

        var typeArg = genericName.TypeArgumentList.Arguments[0];

        // Resolve the full type name via the semantic model
        var semanticModel = compilation.GetSemanticModel(tree);
        var typeInfo = semanticModel.GetTypeInfo(typeArg);
        var hubFullName = typeInfo.Type?.ToDisplayString() ?? typeArg.ToString();

        // Extract the path string from the first argument
        if (invocation.ArgumentList.Arguments.Count == 0)
            return null;

        var pathArg = invocation.ArgumentList.Arguments[0].Expression;
        if (pathArg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return (hubFullName, literal.Token.ValueText);
        }

        return null;
    }

    private (string callback, string? topic)? ExtractSignalRSendInfo(InvocationExpressionSyntax invocation)
    {
        // Match pattern: .SendAsync("MethodName", ...)
        if (invocation.Expression is not MemberAccessExpressionSyntax sendAccess)
            return null;

        if (sendAccess.Name.ToString() != "SendAsync")
            return null;

        // Extract the callback method name from the first argument
        if (invocation.ArgumentList.Arguments.Count == 0)
            return null;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;
        if (firstArg is not LiteralExpressionSyntax callbackLiteral ||
            !callbackLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            return null;

        var callbackName = callbackLiteral.Token.ValueText;

        // Walk up the member access chain to find .Group("topic") or .All
        // Pattern: _hubContext.Clients.Group("topic").SendAsync(...)
        // Pattern: _hubContext.Clients.All.SendAsync(...)
        var receiverExpr = sendAccess.Expression;
        string? topic = null;

        // Check for .Group("topic") pattern: the receiver of SendAsync is a Group() call
        if (receiverExpr is InvocationExpressionSyntax groupInvocation &&
            groupInvocation.Expression is MemberAccessExpressionSyntax groupAccess &&
            groupAccess.Name.ToString() == "Group" &&
            groupInvocation.ArgumentList.Arguments.Count > 0)
        {
            var topicArg = groupInvocation.ArgumentList.Arguments[0].Expression;
            if (topicArg is LiteralExpressionSyntax topicLiteral &&
                topicLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            {
                topic = topicLiteral.Token.ValueText;
            }
        }
        // If receiver is .All (MemberAccessExpressionSyntax ending with "All"), topic stays null

        return (callbackName, topic);
    }

    private ProjectType InferProjectCategory(Project projectToCheck)
    {
        var projectFilePath = projectToCheck.FilePath ?? string.Empty;
        if (string.IsNullOrEmpty(projectFilePath)) return ProjectType.Unknown;

        if (!File.Exists(projectFilePath)) return ProjectType.Unknown;

        // Stream the file to check for specific patterns
        var isExecutable = false;
        var isWebProject = false;

        foreach (var line in File.ReadLines(projectFilePath))
        {
            if (line.Contains("<OutputType>Exe</OutputType>"))
            {
                isExecutable = true;
            }
            if (line.Contains("Microsoft.AspNetCore") || line.Contains("Microsoft.NET.Sdk.Web"))
            {
                isWebProject = true;
            }
            
            // Early exit if we found both
            if (isExecutable && isWebProject) break;
        }

        // Web SDK projects are always executable even without explicit OutputType
        if (isWebProject) return ProjectType.WebApi;
        if (!isExecutable) return ProjectType.ClassLibrary;
        return ProjectType.Console;
    }
}
