using Microsoft.Extensions.Logging;

namespace BenchmarkerRunner.Services;

public class SolutionCopier : ISolutionCopier
{
    private readonly ILogger<SolutionCopier> _logger;

    public SolutionCopier(ILogger<SolutionCopier> logger)
    {
        _logger = logger;
    }

    public List<string> DiscoverSolutions(string solutionsDirectory)
    {
        if (!Directory.Exists(solutionsDirectory))
        {
            _logger.LogWarning("Solutions directory does not exist: {Directory}", solutionsDirectory);
            return [];
        }

        var solutions = Directory.GetFiles(solutionsDirectory, "*.slnx", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(solutionsDirectory, "*.sln", SearchOption.AllDirectories))
            .OrderBy(s => s)
            .ToList();

        _logger.LogInformation("Discovered {Count} solutions in {Directory}", solutions.Count, solutionsDirectory);
        return solutions;
    }

    public string CopySolutionToArtifacts(string solutionPath, string artifactsDirectory)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)
            ?? throw new InvalidOperationException($"Cannot determine directory for solution: {solutionPath}");
        var solutionName = Path.GetFileName(solutionDir);
        var targetDir = Path.Combine(artifactsDirectory, solutionName);

        // Clean existing artifact folder before copying
        if (Directory.Exists(targetDir))
        {
            _logger.LogInformation("Cleaning existing artifact folder: {Directory}", targetDir);
            ClearReadOnlyAttributes(targetDir);
            Directory.Delete(targetDir, recursive: true);
        }

        _logger.LogInformation("Copying {Solution} to {Target}", solutionName, targetDir);
        CopyDirectoryRecursive(solutionDir, targetDir);

        var copiedSlnxPath = Path.Combine(targetDir, Path.GetFileName(solutionPath));
        _logger.LogInformation("Solution copied successfully: {Path}", copiedSlnxPath);
        return copiedSlnxPath;
    }

    private static void ClearReadOnlyAttributes(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            var attrs = File.GetAttributes(file);
            if (attrs.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
            }
        }
    }

    private void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);

            // Skip bin/, obj/, and .git directories to keep artifacts clean
            if (string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, ".git", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectoryRecursive(subDir, targetSubDir);
        }
    }
}
