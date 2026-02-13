using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConversorNFe.App;

public partial class JanelaPrincipal : Window
{
    // Carrega o XAML da janela (layout + estilos)
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
