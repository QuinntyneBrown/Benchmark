using Benchmark.Cli.Commands;
using Benchmark.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace Benchmark.Cli;

public class Program
{
    private static IServiceProvider? _servicesContainer;
    
    public static IServiceProvider ServiceProvider => _servicesContainer 
        ?? throw new InvalidOperationException("Services not initialized");

    public static async Task<int> Main(string[] args)
    {
        var appConfig = LoadApplicationConfiguration();
        _servicesContainer = SetupDependencyContainer(appConfig);

        var mainCommand = ConstructCommandHierarchy();
        return await mainCommand.InvokeAsync(args);
    }

    private static IConfiguration LoadApplicationConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.SetBasePath(Directory.GetCurrentDirectory());
        
        var settingsPath = "appsettings.json";
        if (File.Exists(settingsPath))
        {
            configBuilder.AddJsonFile(settingsPath, optional: true, reloadOnChange: true);
        }
        
        configBuilder.AddEnvironmentVariables();
        
        return configBuilder.Build();
    }

    private static IServiceProvider SetupDependencyContainer(IConfiguration config)
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton(config);
        
        ConfigureLoggingServices(serviceCollection);
        RegisterApplicationServices(serviceCollection);

        return serviceCollection.BuildServiceProvider();
    }

    private static void ConfigureLoggingServices(IServiceCollection services)
    {
        services.AddLogging(configureLogging =>
        {
            configureLogging.AddConsole();
            configureLogging.SetMinimumLevel(LogLevel.Information);
        });
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();
        services.AddSingleton<IBenchmarkProjectGenerator, BenchmarkProjectGenerator>();
    }

    private static RootCommand ConstructCommandHierarchy()
    {
        var rootCmd = new RootCommand("Benchmark project generator for .NET solutions");
        rootCmd.AddCommand(new GenerateBenchmarksCommand());
        return rootCmd;
    }
}
