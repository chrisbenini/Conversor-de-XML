using System.IO;
using Avalonia;
using System;

// Chama o código principal
namespace ConversorNFe.App;

internal sealed class Program
{
    // Configura o AppBuilder do Avalonia (plataforma e logs).
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    public static void Main(string[] args)
    {
        try
        {
            // Inicia o app no modo desktop (janela).
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Se der crash, salva um log no TEMP pra facilitar suporte.
            var logPath = Path.Combine(Path.GetTempPath(), "ConversorNFe_crash.log");
            File.WriteAllText(logPath, ex.ToString());
        }
    }
}
