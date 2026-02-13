using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace ConversorNFe.App;
using Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        // Carrega o App.axaml (temas/estilos globais)
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Se for app de desktop (Windows/Linux/macOS)
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Define a janela principal do programa
            desktop.MainWindow = new JanelaPrincipal();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
