using System;
using Avalonia;
using CC.Signer.Services;
using CC.Signer.ViewModels;
using CC.Signer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CC.Signer;

sealed class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<CCReaderService>();
        services.AddSingleton<EncryptionService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
