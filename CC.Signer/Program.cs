using System;
using System.Linq;
using Avalonia;
using CC.Signer.Api;
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
        // API mode: cc-signer --api [--port 8085]
        if (args.Contains("--api"))
        {
            int port = 8085;
            var portIdx = Array.IndexOf(args, "--port");
            if (portIdx >= 0 && portIdx + 1 < args.Length && int.TryParse(args[portIdx + 1], out var p))
                port = p;

            CcSignerApi.Run(args, port).GetAwaiter().GetResult();
            return;
        }

        // GUI mode (default)
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
