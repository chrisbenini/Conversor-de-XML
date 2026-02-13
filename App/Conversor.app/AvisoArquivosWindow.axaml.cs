using System.Collections.ObjectModel;
using Avalonia.Controls;
using System;

// Chama o código principal
namespace ConversorNFe.App;

// janela aviso erro
public partial class AvisoArquivosWindow : Window
{
    // item inválido
    public class ArquivoInvalidoVM
    {
        public string Arquivo { get; }
        public string Motivo { get; }

        public ArquivoInvalidoVM(string arquivo, string motivo)
        {
            // evita null
            Arquivo = arquivo ?? "";
            Motivo = motivo ?? "";
        }
    }

    // texto topo
    public string Titulo { get; }

    // texto subtítulo
    public string Subtitulo { get; }

    // modo só OK
    public bool ApenasOk { get; }

    // lista inválidos
    public ObservableCollection<ArquivoInvalidoVM> Invalidos { get; } = new();

    // lista válidos
    public ObservableCollection<string> Validos { get; } = new();

    // recebe dados
    public AvisoArquivosWindow(
        string titulo,
        string subtitulo,
        System.Collections.Generic.IEnumerable<string> invalidos,
        System.Collections.Generic.IEnumerable<string> validos,
        bool apenasOk)
    {
        InitializeComponent();

        // seta textos
        Titulo = titulo;
        Subtitulo = subtitulo;

        // seta modo
        ApenasOk = apenasOk;

        // monta inválidos
        foreach (var s in invalidos ?? Array.Empty<string>())
        {
            var (arq, mot) = SplitArquivoMotivo(s);
            Invalidos.Add(new ArquivoInvalidoVM(arq, mot));
        }

        // monta válidos
        foreach (var v in validos ?? Array.Empty<string>())
            Validos.Add(v);

        // bind geral
        DataContext = this;

        // pega botões
        var btnOk = this.FindControl<Button>("BtnOk");
        var btnContinuar = this.FindControl<Button>("BtnContinuar");
        var btnCancelar = this.FindControl<Button>("BtnCancelar");

        // troca visibilidade
        if (btnOk != null && btnContinuar != null && btnCancelar != null)
        {
            if (ApenasOk)
            {
                btnOk.IsVisible = true;
                btnContinuar.IsVisible = false;
                btnCancelar.IsVisible = false;
            }
            else
            {
                btnOk.IsVisible = false;
                btnContinuar.IsVisible = true;
                btnCancelar.IsVisible = true;
            }
        }
    }

    // quebra "arquivo: motivo"
    private static (string arquivo, string motivo) SplitArquivoMotivo(string texto)
    {
        // vazio? sai
        if (string.IsNullOrWhiteSpace(texto))
            return ("", "");

        // acha dois pontos
        var idx = texto.IndexOf(':');

        // sem motivo
        if (idx < 0)
            return (texto.Trim(), "");

        // separa partes
        var arquivo = texto.Substring(0, idx).Trim();
        var motivo = texto.Substring(idx + 1).Trim();
        return (arquivo, motivo);
    }

    // continuar = true
    private void Continuar_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(true);

    // cancelar = false
    private void Cancelar_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(false);

    // ok = false
    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(false);
}
