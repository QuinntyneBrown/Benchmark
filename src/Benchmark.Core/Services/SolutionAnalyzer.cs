using Benchmark.Core.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
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

        using var workspaceInstance = MSBuildWorkspace.Create();
        var loadedSolution = await workspaceInstance.OpenSolutionAsync(solutionPath);
        
        var discoveredProjects = new List<ProjectInfoModel>();
        
        foreach (var currentProject in loadedSolution.Projects)
        {
            var extractedInfo = await ExtractProjectDetailsAsync(currentProject);
            if (extractedInfo != null)
            {
                discoveredProjects.Add(extractedInfo);
            }
        }

        return discoveredProjects;
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

    private ProjectType InferProjectCategory(Project projectToCheck)
    {
        var projectFilePath = projectToCheck.FilePath ?? string.Empty;
        if (string.IsNullOrEmpty(projectFilePath)) return ProjectType.Unknown;

        var fileContent = File.ReadAllText(projectFilePath);

        var isExecutable = fileContent.Contains("<OutputType>Exe</OutputType>");
        if (!isExecutable) return ProjectType.ClassLibrary;

        var isWebProject = fileContent.Contains("Microsoft.AspNetCore") || 
                          fileContent.Contains("Microsoft.NET.Sdk.Web");
        
        return isWebProject ? ProjectType.WebApi : ProjectType.Console;
    }
}
