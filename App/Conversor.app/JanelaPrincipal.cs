using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using System.IO;
using Avalonia;
using System;

// Chama o código príncipal
namespace ConversorNFe.App;

public partial class JanelaPrincipal : Window
{
    // Só um modelzinho pra listar os arquivos na tela
    public class ArquivoItem
    {
        public string Nome { get; set; } = "";
        public string Caminho { get; set; } = "";
    }

    // Limite de arquivos que a pessoa pode jogar de uma vez
    private const int LIMITE_ARQUIVOS = 10;

    // Lista que alimenta a ListBox de arquivos e o histórico
    private readonly ObservableCollection<ArquivoItem> _arquivos = new();
    private readonly ObservableCollection<string> _historico = new();

    // Pego os controles do XAML aqui pra eu poder usar no código sem dor de cabeça
    private ListBox _listaArquivos = null!;
    private ListBox _listaHistorico = null!;
    private Button _btnSelecionar = null!;
    private Button _btnGerar = null!;
    private Button _btnLimparArquivos = null!;
    private Button _btnLimparHistorico = null!;
    private ProgressBar _barraProgresso = null!;
    private TextBlock _txtStatus = null!;
    private RadioButton _rbModoCalculos = null!;
    private RadioButton _rbModoPowerCare = null!;
    private RadioButton _rbModoXmlBruto = null!;
    private Button _btnMaximizar = null!;

    // “Maximizar fake” pra respeitar a barra de tarefas (fica igual app normal)
    private bool _pseudoMaximizado = false;
    private PixelPoint _restorePos;
    private Size _restoreSize;

    public JanelaPrincipal()
    {
        InitializeComponent();

        // Aqui eu pego tudo pelo x:Name (se faltar algum, eu já erro na hora)
        _listaArquivos      = MustFind<ListBox>("ListaArquivos");
        _listaHistorico     = MustFind<ListBox>("ListaHistorico");
        _btnSelecionar      = MustFind<Button>("BtnSelecionar");
        _btnGerar           = MustFind<Button>("BtnGerar");
        _btnLimparArquivos  = MustFind<Button>("BtnLimparArquivos");
        _btnLimparHistorico = MustFind<Button>("BtnLimparHistorico");
        _barraProgresso     = MustFind<ProgressBar>("BarraProgresso");
        _txtStatus          = MustFind<TextBlock>("TxtStatus");
        _rbModoCalculos     = MustFind<RadioButton>("RbModoCalculos");
        _rbModoPowerCare    = MustFind<RadioButton>("RbModoPowerQuery");
        _rbModoXmlBruto     = MustFind<RadioButton>("RbModoXmlBruto");
        _btnMaximizar       = MustFind<Button>("BtnMaximizar");

        // Ligo as listas da UI com as collections
        _listaArquivos.ItemsSource = _arquivos;
        _listaHistorico.ItemsSource = _historico;

        // Eventos dos botões principais
        _btnSelecionar.Click += SelecionarArquivos_Click;
        _btnLimparArquivos.Click += LimparArquivos_Click;
        _btnLimparHistorico.Click += LimparHistorico_Click;
        _btnGerar.Click += GerarExcel_Click;

        // Ajusta o ícone do botão de maximizar conforme estado
        UpdateMaximizeIcon();

        // Mensagens iniciais
        AdicionarHistorico("Aplicação iniciada.");
        DefinirStatus("pronto.");
    }

    // Método “seguro”: se o controle não existir no XAML, já avisa o erro certinho
    private T MustFind<T>(string name) where T : Control
    {
        var c = this.FindControl<T>(name);
        if (c == null)
            throw new InvalidOperationException($"XAML: não encontrei o controle '{name}'. Confira o x:Name no JanelaPrincipal.axaml.");
        return c;
    }

    // -------- Titlebar custom --------
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Duplo clique no topo = maximizar/restaurar (igual Chrome)
        if (e.ClickCount == 2)
        {
            TogglePseudoMaximize();
            return;
        }

        // Clique normal segurando = arrastar janela
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    // Minimiza a janela
    private void Minimizar_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    // Maximiza/restaura (usando o “fake max”)
    private void Maximizar_Click(object? sender, RoutedEventArgs e)
        => TogglePseudoMaximize();

    // Fecha o app
    private void Fechar_Click(object? sender, RoutedEventArgs e)
        => Close();

    // Troca o ícone do botão pra ficar mais “padrão Windows”
    private void UpdateMaximizeIcon()
    {
        // normal: 1 quadrado / maximizado: 2 quadrados
        _btnMaximizar.Content = _pseudoMaximizado ? "❐" : "▢";
    }

    // Faz o “maximizar” respeitando a barra de tarefas
    private void TogglePseudoMaximize()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen == null) return;

        // Área útil da tela (sem taskbar)
        var wa = screen.WorkingArea;
        var scale = screen.Scaling;

        if (!_pseudoMaximizado)
        {
            // Salva onde tava pra poder voltar depois
            _restorePos = Position;
            _restoreSize = new Size(Width, Height);

            WindowState = WindowState.Normal;

            // Joga a janela pro tamanho da área útil
            Position = wa.Position;
            Width  = wa.Width / scale;
            Height = wa.Height / scale;

            _pseudoMaximizado = true;
        }
        else
        {
            // Volta pro tamanho/posição antiga
            WindowState = WindowState.Normal;
            Position = _restorePos;
            Width = _restoreSize.Width;
            Height = _restoreSize.Height;

            _pseudoMaximizado = false;
        }

        UpdateMaximizeIcon();
    }

    // -------- Arquivos --------
    private async void SelecionarArquivos_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Filtros do seletor (pra pessoa não escolher coisa nada a ver)
            var filtros = new[]
            {
                new FilePickerFileType("NF-e (XML/PDF/TXT)") { Patterns = new[] { "*.xml", "*.pdf", "*.txt" } },
                new FilePickerFileType("XML") { Patterns = new[] { "*.xml" } },
                new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } },
                new FilePickerFileType("TXT") { Patterns = new[] { "*.txt" } }
            };

            // Abre o explorador de arquivos
            var arquivos = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Selecione até {LIMITE_ARQUIVOS} arquivos (XML, PDF ou TXT)",
                AllowMultiple = true,
                FileTypeFilter = filtros
            });

            if (arquivos == null || arquivos.Count == 0)
                return;

            // Pego no máximo o limite e converto pra caminho local
            var selecionados = arquivos
                .Take(LIMITE_ARQUIVOS)
                .Select(a => a.TryGetLocalPath())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!)
                .ToList();

            // Atualiza a lista na tela
            _arquivos.Clear();
            foreach (var caminho in selecionados)
            {
                _arquivos.Add(new ArquivoItem
                {
                    Caminho = caminho,
                    Nome = Path.GetFileName(caminho)
                });
            }

            AdicionarHistorico($"Selecionado(s): {selecionados.Count} arquivo(s).");
            DefinirStatus($"arquivos selecionados ({selecionados.Count}/{LIMITE_ARQUIVOS}).");
        }
        catch (Exception ex)
        {
            // Se der ruim aqui, pelo menos eu registro
            AdicionarHistorico($"Erro ao selecionar arquivos: {ex.Message}");
            DefinirStatus("erro ao selecionar arquivos.");
        }
    }

    // Limpa a lista de arquivos selecionados
    private void LimparArquivos_Click(object? sender, RoutedEventArgs e)
    {
        _arquivos.Clear();
        AdicionarHistorico("Arquivos limpos.");
        DefinirStatus("pronto.");
    }

    // Limpa o histórico da tela
    private void LimparHistorico_Click(object? sender, RoutedEventArgs e)
    {
        _historico.Clear();
        AdicionarHistorico("Histórico limpo.");
        DefinirStatus("pronto.");
    }

    // Remove só um arquivo da lista
    private void RemoverArquivo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string caminho || string.IsNullOrWhiteSpace(caminho)) return;

        var item = _arquivos.FirstOrDefault(a => a.Caminho == caminho);
        if (item == null) return;

        _arquivos.Remove(item);
        AdicionarHistorico($"Removido: {item.Nome}");
        DefinirStatus($"arquivos selecionados ({_arquivos.Count}/{LIMITE_ARQUIVOS}).");
    }

    // -------- Geração --------
    private async void GerarExcel_Click(object? sender, RoutedEventArgs e)
    {
        // Sem arquivo = sem milagre
        if (_arquivos.Count == 0)
        {
            AdicionarHistorico("Nenhum arquivo selecionado.");
            DefinirStatus("selecione arquivos primeiro.");
            return;
        }

        // Descobre qual modo tá marcado
        var modoCalculos  = _rbModoCalculos.IsChecked == true;
        var modoPowerCare = _rbModoPowerCare.IsChecked == true;
        var modoXmlBruto  = _rbModoXmlBruto.IsChecked == true;

        // Só pra mostrar no histórico bonitinho
        var modoTexto =
            modoPowerCare ? "Power Query (selecionar linhas)" :
            modoXmlBruto ? "XML bruto" :
            "NF-e (c/ cálculos)";

        // UI de “tá trabalhando”
        _barraProgresso.IsVisible = true;
        DefinirStatus("gerando...");
        AdicionarHistorico($"Iniciando geração ({modoTexto})...");

        // Aqui vai entrar o que deu pra converter pra XML de verdade
        var xmlsValidos = new List<(string XmlPath, string NomeArquivo)>();

        // Lista de temporários (pdf/txt viram xml temp)
        var temporariosParaApagar = new List<string>();

        // Tudo que falhar vai cair aqui pra eu mostrar no aviso
        var erros = new List<(string NomeArquivo, string Motivo)>();

        try
        {
            // Snapshot pra evitar treta se mexerem na lista no meio
            var arquivosSnapshot = _arquivos.ToList();

            // Processa os arquivos fora da UI (pra não travar)
            await Task.Run(() =>
            {
                foreach (var a in arquivosSnapshot)
                {
                    try
                    {
                        var ext = Path.GetExtension(a.Caminho).ToLowerInvariant();

                        if (!File.Exists(a.Caminho))
                            throw new FileNotFoundException("Arquivo não encontrado.");

                        // XML direto: já serve
                        if (ext == ".xml")
                        {
                            xmlsValidos.Add((a.Caminho, a.Nome));
                            continue;
                        }

                        // PDF: tenta puxar o XML embutido e salva num temp
                        if (ext == ".pdf")
                        {
                            var xmlTemp = ExtratorXmlDePdf.ExtrairXmlParaArquivoTemporario(a.Caminho);
                            temporariosParaApagar.Add(xmlTemp);
                            xmlsValidos.Add((xmlTemp, a.Nome));
                            continue;
                        }

                        // TXT: tenta achar um bloco XML dentro do texto e salva num temp
                        if (ext == ".txt")
                        {
                            var xmlTemp = ExtrairXmlDeTxtParaArquivoTemporario(a.Caminho);
                            temporariosParaApagar.Add(xmlTemp);
                            xmlsValidos.Add((xmlTemp, a.Nome));
                            continue;
                        }

                        // Qualquer outra coisa não rola
                        throw new InvalidOperationException("Extensão não suportada. Use .xml, .pdf ou .txt.");
                    }
                    catch (Exception ex)
                    {
                        // Joga o motivo do erro pra lista do aviso
                        erros.Add((a.Nome, ex.Message));
                    }
                }
            });

            // No modo cálculos eu só aceito XML SEFAZ de NF-e
            if (modoCalculos && xmlsValidos.Count > 0)
            {
                var somenteSefaz = xmlsValidos.Where(x => PareceNfeSefaz(x.XmlPath)).ToList();

                // Tudo que não for SEFAZ vira “inválido” com motivo
                var ignorados = xmlsValidos.Where(x => !PareceNfeSefaz(x.XmlPath))
                                           .Select(x => x.NomeArquivo)
                                           .Distinct()
                                           .ToList();

                foreach (var nome in ignorados)
                    erros.Add((nome, "Não é um XML válido para o modo de cálculos."));

                xmlsValidos = somenteSefaz;
            }

            // Se rolou qualquer erro, eu abro a janela de aviso
            if (erros.Count > 0)
            {
                // Lista de inválidos com “arquivo: motivo”
                var listaInvalidos = erros
                    .Select(x => $"{x.NomeArquivo}: {x.Motivo}")
                    .Distinct()
                    .ToList();

                // Lista só com os nomes dos válidos
                var listaValidos = xmlsValidos
                    .Select(x => x.NomeArquivo)
                    .Distinct()
                    .ToList();

                // Se nada sobrou válido: só mostra OK e já era
                if (xmlsValidos.Count == 0)
                {
                    AdicionarHistorico("Nenhum arquivo válido para converter.");
                    foreach (var (nome, motivo) in erros)
                        AdicionarHistorico($"Falhou: {nome} — {motivo}");

                    DefinirStatus("não foi possível gerar.");

                    var winOk = new AvisoArquivosWindow(
                        titulo: "Não foi possível gerar",
                        subtitulo: "Nenhum arquivo válido foi encontrado. Verifique os arquivos selecionados e tente novamente.",
                        invalidos: listaInvalidos,
                        validos: Array.Empty<string>(),
                        apenasOk: true
                    );

                    await winOk.ShowDialog<bool>(this);
                    return;
                }

                // Se tem válidos e inválidos: pergunta se quer seguir só com os válidos
                var win = new AvisoArquivosWindow(
                    titulo: "Alguns arquivos não puderam ser lidos",
                    subtitulo: "Você deseja continuar gerando o Excel apenas com os arquivos válidos?",
                    invalidos: listaInvalidos,
                    validos: listaValidos,
                    apenasOk: false
                );

                var continuar = await win.ShowDialog<bool>(this);
                if (!continuar)
                {
                    AdicionarHistorico("Geração cancelada pelo usuário.");
                    DefinirStatus("pronto.");
                    return;
                }
            }

            // Power Query: pra cada XML válido, abre a janela pra pessoa marcar as linhas
            var selecoesPorXmlPath = new Dictionary<string, HashSet<int>>();
            if (modoPowerCare)
            {
                foreach (var (xmlPath, nomeArquivo) in xmlsValidos)
                {
                    var linhas = GeradorExcel.CarregarXmlComoLinhas(xmlPath);

                    var win = new SelecionarColunasWindow(
                        linhas,
                        tituloJanela: $"Power Query — {nomeArquivo}",
                        tituloTela: $"Selecione as linhas que deseja exportar ({nomeArquivo})"
                    );

                    var okDialog = await win.ShowDialog<bool>(this);

                    // Se a pessoa cancelar, eu paro tudo
                    if (!okDialog || win.ResultadoIndices == null || win.ResultadoIndices.Count == 0)
                    {
                        AdicionarHistorico("Seleção cancelada (Power Query).");
                        DefinirStatus("pronto.");
                        return;
                    }

                    selecoesPorXmlPath[xmlPath] = win.ResultadoIndices.ToHashSet();
                }
            }

            // Nome sugerido do excel
            var suggested = $"ConversorNFe_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            // Abre o “salvar como”
            var destino = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Salvar Excel",
                SuggestedFileName = suggested,
                DefaultExtension = "xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                }
            });

            var savePath = destino?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(savePath))
            {
                AdicionarHistorico("Geração cancelada.");
                DefinirStatus("pronto.");
                return;
            }

            // Gera o excel de verdade (também fora da UI)
            await Task.Run(() =>
            {
                if (modoPowerCare)
                    GeradorExcel.GerarModoPowerCareLinhasFiltradas(xmlsValidos, savePath, selecoesPorXmlPath);
                else if (modoXmlBruto)
                    GeradorExcel.GerarModoXmlLinhaALinha(xmlsValidos, savePath);
                else
                    GeradorExcel.GerarModoCalculos(xmlsValidos, savePath);
            });

            AdicionarHistorico($"Excel gerado: {Path.GetFileName(savePath)} ✅");
            DefinirStatus("concluído ✅");
        }
        catch (Exception ex)
        {
            // Log no histórico (pra eu saber o que deu)
            AdicionarHistorico($"Erro geral ao gerar: {ex}");
            DefinirStatus("erro ao gerar.");

            // E também mostro um aviso na tela
            var winOk = new AvisoArquivosWindow(
                titulo: "Erro ao gerar",
                subtitulo: "Ocorreu um erro durante a geração. Veja o motivo abaixo.",
                invalidos: new[] { ex.Message },
                validos: Array.Empty<string>(),
                apenasOk: true
            );

            await winOk.ShowDialog<bool>(this);
        }
        finally
        {
            // Apaga os xmls temporários que eu criei (pdf/txt)
            foreach (var temp in temporariosParaApagar)
            {
                try { File.Delete(temp); } catch { }
            }

            // Desliga a barra de loading
            _barraProgresso.IsVisible = false;
        }
    }

    // TXT -> tenta recortar um XML dentro do texto e salvar num arquivo temp
    private static string ExtrairXmlDeTxtParaArquivoTemporario(string txtPath)
    {
        var text = File.ReadAllText(txtPath);

        int start = text.IndexOf('<');
        int end = text.LastIndexOf('>');

        if (start < 0 || end <= start)
            throw new InvalidOperationException("TXT não contém XML.");

        var xml = text.Substring(start, (end - start) + 1).Trim();

        if (!xml.StartsWith("<") || !xml.EndsWith(">"))
            throw new InvalidOperationException("TXT não contém um XML válido.");

        var temp = Path.Combine(Path.GetTempPath(), $"conversor_txt_{Guid.NewGuid():N}.xml");
        File.WriteAllText(temp, xml);
        return temp;
    }

    // Checagem rápida pra ver se parece NF-e SEFAZ (modo cálculos)
    private static bool PareceNfeSefaz(string xmlPath)
    {
        try
        {
            var text = File.ReadAllText(xmlPath);
            if (text.Length > 20000) text = text.Substring(0, 20000);

            return text.Contains("http://www.portalfiscal.inf.br/nfe", StringComparison.OrdinalIgnoreCase)
                   && (text.Contains("<nfeProc", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("<NFe", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    // Joga mensagem no histórico com hora e desce o scroll pro final
    private void AdicionarHistorico(string msg)
    {
        var hora = DateTime.Now.ToString("HH:mm:ss");
        _historico.Add($"{hora} • {msg}");

        if (_historico.Count > 0)
            _listaHistorico.ScrollIntoView(_historico.Last());
    }

    // Status curtinho ali embaixo
    private void DefinirStatus(string msg)
    {
        _txtStatus.Text = $"Status: {msg}";
    }
}
