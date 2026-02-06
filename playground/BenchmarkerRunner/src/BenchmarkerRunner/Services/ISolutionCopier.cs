namespace BenchmarkerRunner.Services;

public interface ISolutionCopier
{
    List<string> DiscoverSolutions(string solutionsDirectory);
    string CopySolutionToArtifacts(string solutionPath, string artifactsDirectory);
}
