using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using System.Linq;
using System;

// Chama o código príncipal
namespace ConversorNFe.App;

public partial class SelecionarColunasWindow : Window
{
    // Item da lista (linha do XML)
    public sealed class LinhaVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Index { get; }
        public string Display { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public LinhaVm(int index, string display)
        {
            Index = index;
            Display = display;
        }
    }

    // Resultado final: índices marcados
    public List<int> ResultadoIndices { get; private set; } = new();

    private List<LinhaVm> _todas = new();
    private ObservableCollection<LinhaVm> _visiveis = new();

    // Controles da tela
    private TextBox _txtFiltro = null!;
    private ListBox _lista = null!;
    private Button _btnMarcarTudo = null!;
    private Button _btnDesmarcarTudo = null!;
    private Button _btnOk = null!;
    private Button _btnCancelar = null!;
    private TextBlock? _txtTitulo;

    // Construtor vazio pro Avalonia
    public SelecionarColunasWindow()
        : this(Array.Empty<string>(), "Power Query", "Selecione as linhas que deseja exportar")
    {
    }

    public SelecionarColunasWindow(IReadOnlyList<string> linhas, string tituloJanela, string tituloTela)
    {
        InitializeComponent();

        // Pega controles pelo x:Name
        _txtFiltro = MustFind<TextBox>("TxtFiltro");
        _lista = MustFind<ListBox>("Lista");
        _btnMarcarTudo = MustFind<Button>("BtnMarcarTudo");
        _btnDesmarcarTudo = MustFind<Button>("BtnDesmarcarTudo");
        _btnOk = MustFind<Button>("BtnOk");
        _btnCancelar = MustFind<Button>("BtnCancelar");
        _txtTitulo = this.FindControl<TextBlock>("TxtTitulo");

        // Ajusta textos da janela
        Title = string.IsNullOrWhiteSpace(tituloJanela) ? "Power Query" : tituloJanela;
        if (_txtTitulo != null)
            _txtTitulo.Text = string.IsNullOrWhiteSpace(tituloTela) ? "Selecione as linhas" : tituloTela;

        // Monta lista de linhas
        _todas = (linhas ?? Array.Empty<string>())
            .Select((s, i) => new LinhaVm(i, s ?? ""))
            .ToList();

        _visiveis = new ObservableCollection<LinhaVm>(_todas);
        _lista.ItemsSource = _visiveis;

        // Filtra conforme digita
        _txtFiltro.TextChanged += (_, __) => AplicarFiltro();

        // Marca tudo (somente o que está visível)
        _btnMarcarTudo.Click += (_, __) =>
        {
            foreach (var x in _visiveis)
                x.IsChecked = true;

            RefreshLista();
        };

        // Desmarca tudo (somente o que está visível)
        _btnDesmarcarTudo.Click += (_, __) =>
        {
            foreach (var x in _visiveis)
                x.IsChecked = false;

            RefreshLista();
        };

        // Cancela
        _btnCancelar.Click += (_, __) =>
        {
            ResultadoIndices = new();
            Close(false);
        };

        // Confirma seleção
        _btnOk.Click += (_, __) =>
        {
            ResultadoIndices = _todas
                .Where(x => x.IsChecked)
                .Select(x => x.Index)
                .ToList();

            Close(ResultadoIndices.Count > 0);
        };
    }

    private T MustFind<T>(string name) where T : Control
    {
        var c = this.FindControl<T>(name);
        if (c == null)
            throw new InvalidOperationException($"XAML: não encontrei o controle '{name}' em SelecionarColunasWindow.axaml");
        return c;
    }

    private void RefreshLista()
    {
        // Recarrega ItemsSource pra atualizar a tela
        _lista.ItemsSource = null;
        _lista.ItemsSource = _visiveis;
    }

    private void AplicarFiltro()
    {
        var f = (_txtFiltro.Text ?? "").Trim();

        _visiveis.Clear();

        IEnumerable<LinhaVm> query = _todas;

        // Filtra por texto
        if (!string.IsNullOrWhiteSpace(f))
            query = query.Where(x => x.Display.Contains(f, StringComparison.OrdinalIgnoreCase));

        foreach (var item in query)
            _visiveis.Add(item);

        RefreshLista();
    }
}
