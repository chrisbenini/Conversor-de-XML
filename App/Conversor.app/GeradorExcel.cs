using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using ClosedXML.Excel;
using System.Linq;
using System.IO;
using System;

// Chama o código principal
namespace ConversorNFe.App;

public static class GeradorExcel
{
    // MODO 1: CÁLCULOS (NF-e com ST/IPI)
    public static void GerarModoCalculos(List<(string XmlPath, string NomeArquivo)> xmls, string destinoXlsx)
    {
        using var wb = new XLWorkbook();

        foreach (var (xmlPath, nomeArquivo) in xmls)
        {
            // uma aba por arquivo
            var sheetName = MakeUniqueSheetName(wb, Path.GetFileNameWithoutExtension(nomeArquivo));
            var ws = wb.Worksheets.Add(sheetName);

            // lê os itens do XML e já traz com os cálculos
            var linhas = LeitorXmlNfe.ExtrairItensComCalculos(xmlPath);

            if (linhas.Count == 0)
            {
                ws.Cell(1, 1).Value = "Não encontrei itens <det> nesse XML.";
                ws.Cell(1, 1).Style.Font.Bold = true;
                continue;
            }

            // pega todas as colunas (chaves) na ordem do primeiro item
            var colunas = linhas[0].Keys.ToList();

            // cabeçalho (linha 1)
            for (int c = 0; c < colunas.Count; c++)
            {
                ws.Cell(1, c + 1).Value = colunas[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
            }

            // dados (a partir da linha 2)
            for (int r = 0; r < linhas.Count; r++)
            {
                var row = linhas[r];

                for (int c = 0; c < colunas.Count; c++)
                {
                    var key = colunas[c];
                    row.TryGetValue(key, out var valor);

                    var cell = ws.Cell(r + 2, c + 1);
                    SetCellValue(cell, valor);

                    if (valor is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents(1, 120);
        }

        wb.SaveAs(destinoXlsx);
    }

    // POWER QUERY:
    // 1 linha de títulos (tags) + produtos para baixo

    public static void GerarModoPowerCareLinhasFiltradas(
        List<(string XmlPath, string NomeArquivo)> xmls,
        string destinoXlsx,
        Dictionary<string, HashSet<int>> linhasSelecionadasPorXmlPath)
    {
        using var wb = new XLWorkbook();

        foreach (var (xmlPath, nomeArquivo) in xmls)
        {
            var sheetName = MakeUniqueSheetName(wb, Path.GetFileNameWithoutExtension(nomeArquivo) + "_FILTRADO");
            var ws = wb.Worksheets.Add(sheetName);

            var lines = CarregarXmlComoLinhas(xmlPath);

            if (!linhasSelecionadasPorXmlPath.TryGetValue(xmlPath, out var selected) || selected.Count == 0)
            {
                ws.Cell(1, 1).Value = "Nenhuma linha selecionada.";
                continue;
            }

            var selecionadas = selected.OrderBy(x => x)
                                       .Where(idx => idx >= 0 && idx < lines.Count)
                                       .Select(idx => lines[idx])
                                       .ToList();

            if (selecionadas.Count == 0)
            {
                ws.Cell(1, 1).Value = "Nenhuma linha selecionada.";
                continue;
            }

            // Tenta extrair "nomes de tags" a partir das linhas escolhidas
            var colunasDesejadas = ExtrairColunasDesejadasDoPowerQuery(selecionadas);

            // Se não conseguiu puxar tags, cai no modo antigo (1 linha por coluna)
            if (colunasDesejadas.Count == 0)
            {
                int col = 1;
                foreach (var (linha, i) in selecionadas.Select((l, i) => (l, i)))
                {
                    ws.Cell(1, col).Value = $"Linha {i + 1}";
                    ws.Cell(1, col).Style.Font.Bold = true;

                    ws.Cell(2, col).Value = linha;
                    ws.Cell(2, col).Style.Alignment.WrapText = true;

                    col++;
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(1, 120);
                continue;
            }

            // Tenta montar tabela por produto (det / grupo repetido)
            if (!TryExtrairTabelaPorGrupoRepetido(xmlPath, out var linhasProdutos, out var todasColunasNoXml))
            {
                // Se não achou grupo repetido, também cai no modo antigo
                int col = 1;
                foreach (var (linha, i) in selecionadas.Select((l, i) => (l, i)))
                {
                    ws.Cell(1, col).Value = $"Linha {i + 1}";
                    ws.Cell(1, col).Style.Font.Bold = true;

                    ws.Cell(2, col).Value = linha;
                    ws.Cell(2, col).Style.Alignment.WrapText = true;

                    col++;
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(1, 120);
                continue;
            }

            // Mantém só as colunas que o usuário selecionou (na ordem dele)
            var colunasFinal = colunasDesejadas
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Cabeçalho (linha 1)
            for (int c = 0; c < colunasFinal.Count; c++)
            {
                ws.Cell(1, c + 1).Value = colunasFinal[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
            }

            // Linhas (produtos) a partir da linha 2
            for (int r = 0; r < linhasProdutos.Count; r++)
            {
                var row = linhasProdutos[r];

                for (int c = 0; c < colunasFinal.Count; c++)
                {
                    var colName = colunasFinal[c];

                    string valor;
                    if (row.TryGetValue(colName, out var v) && !string.IsNullOrWhiteSpace(v))
                        valor = v;
                    else
                        valor = "não possui";

                    ws.Cell(r + 2, c + 1).Value = valor;
                    ws.Cell(r + 2, c + 1).Style.Alignment.WrapText = true;
                }
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents(1, 120);
        }

        wb.SaveAs(destinoXlsx);
    }

    // XML BRUTO:
    public static void GerarModoXmlLinhaALinha(List<(string XmlPath, string NomeArquivo)> xmls, string destinoXlsx)
    {
        using var wb = new XLWorkbook();

        foreach (var (xmlPath, nomeArquivo) in xmls)
        {
            var sheetName = MakeUniqueSheetName(wb, Path.GetFileNameWithoutExtension(nomeArquivo) + "_RAW");
            var ws = wb.Worksheets.Add(sheetName);

            // tenta pegar "tabela por produto"
            if (!TryExtrairTabelaPorGrupoRepetido(xmlPath, out var linhasProdutos, out var colunas))
            {
                // fallback: modo antigo (1 registro só)
                var dados = ExtrairTagsEValoresAntigo(xmlPath);

                if (dados.Count == 0)
                {
                    ws.Cell(1, 1).Value = "Não foi possível extrair TAG/VALOR deste XML.";
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    continue;
                }

                int col = 1;
                foreach (var kv in dados)
                {
                    ws.Cell(1, col).Value = kv.Key;
                    ws.Cell(1, col).Style.Font.Bold = true;

                    ws.Cell(2, col).Value = kv.Value;
                    ws.Cell(2, col).Style.Alignment.WrapText = true;

                    col++;
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(1, 80);
                continue;
            }

            if (linhasProdutos.Count == 0 || colunas.Count == 0)
            {
                ws.Cell(1, 1).Value = "Não encontrei itens repetidos (produtos) nesse XML.";
                ws.Cell(1, 1).Style.Font.Bold = true;
                continue;
            }

            // Cabeçalho (linha 1)
            for (int c = 0; c < colunas.Count; c++)
            {
                ws.Cell(1, c + 1).Value = colunas[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
            }

            // Linhas (produtos) a partir da linha 2
            for (int r = 0; r < linhasProdutos.Count; r++)
            {
                var row = linhasProdutos[r];

                for (int c = 0; c < colunas.Count; c++)
                {
                    var colName = colunas[c];

                    string valor;
                    if (row.TryGetValue(colName, out var v) && !string.IsNullOrWhiteSpace(v))
                        valor = v;
                    else
                        valor = "não possui";

                    ws.Cell(r + 2, c + 1).Value = valor;
                    ws.Cell(r + 2, c + 1).Style.Alignment.WrapText = true;
                }
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents(1, 120);
        }

        wb.SaveAs(destinoXlsx);
    }


    // Carrega XML como linhas (Power Query)
    public static List<string> CarregarXmlComoLinhas(string xmlPath)
    {
        try
        {
            using var sr = new StreamReader(xmlPath, detectEncodingFromByteOrderMarks: true);
            var text = sr.ReadToEnd();

            try
            {
                var doc = XDocument.Parse(text, System.Xml.Linq.LoadOptions.None);
                text = doc.ToString(System.Xml.Linq.SaveOptions.None);
            }
            catch
            {
                // se der erro, só usa o texto do jeito que tá
            }

            return text.Replace("\r\n", "\n")
                       .Replace("\r", "\n")
                       .Split('\n')
                       .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    // Transforma "object" em valor aceitável pelo ClosedXML
    private static void SetCellValue(IXLCell cell, object? value)
    {
        if (value == null)
        {
            cell.Value = "";
            return;
        }

        switch (value)
        {
            case string s:
                cell.Value = s;
                return;

            case bool b:
                cell.Value = b;
                return;

            case DateTime dt:
                cell.Value = dt;
                return;

            case decimal dec:
                cell.Value = (double)dec;
                return;

            case double d:
                cell.Value = d;
                return;

            case float f:
                cell.Value = (double)f;
                return;

            case sbyte or byte or short or ushort or int or uint or long or ulong:
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return;

            default:
                cell.Value = value.ToString() ?? "";
                return;
        }
    }

    // POWER QUERY:
    private static List<string> ExtrairColunasDesejadasDoPowerQuery(List<string> linhasSelecionadas)
    {
        var cols = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var l in linhasSelecionadas)
        {
            var line = (l ?? "").Trim();
            if (line.Length == 0) continue;

            // pega tag de abertura (ignora </tag>, <?xml ...)
            var m = Regex.Match(line, @"<\s*(?!/|\?)([A-Za-z_][A-Za-z0-9_:\-\.]*)\b");
            if (!m.Success) continue;

            var tag = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(tag)) continue;

            // tira namespace "ns:Tag" -> "Tag"
            var local = tag.Contains(':') ? tag.Split(':').Last() : tag;

            if (seen.Add(local))
                cols.Add(local);
        }

        return cols;
    }

    // Tenta montar "tabela por produto"
    private static bool TryExtrairTabelaPorGrupoRepetido(
        string xmlPath,
        out List<Dictionary<string, string>> linhas,
        out List<string> colunas)
    {
        linhas = new List<Dictionary<string, string>>();
        colunas = new List<string>();

        try
        {
            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;
            if (root == null) return false;

            // 1) NF-e: usa <det> como produto
            var dets = root.Descendants().Where(x => x.Name.LocalName.Equals("det", StringComparison.OrdinalIgnoreCase)).ToList();
            if (dets.Count > 0)
            {
                MontarTabela(dets, out linhas, out colunas);
                return linhas.Count > 0 && colunas.Count > 0;
            }

            // 2) Genérico: achar o "melhor" grupo repetido
            var grupos = root.Descendants()
                .GroupBy(e => e.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    Nome = g.Key,
                    Itens = g.ToList(),
                    Score = g.Count() * (g.First().Descendants().Count(d => !d.HasElements) + 1)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            if (grupos.Count == 0)
                return false;

            var melhor = grupos.First().Itens;

            MontarTabela(melhor, out linhas, out colunas);
            return linhas.Count > 0 && colunas.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    // Monta a tabela: cada elemento do grupo vira uma linha (produto)
    private static void MontarTabela(
        List<XElement> grupo,
        out List<Dictionary<string, string>> linhas,
        out List<string> colunas)
    {
        linhas = new List<Dictionary<string, string>>();
        var setColunas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in grupo)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // (extra) se for <det>, pega o nItem como uma coluna legal
            var nItemAttr = item.Attribute("nItem")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(nItemAttr))
                row["nItem"] = nItemAttr;

            foreach (var el in item.Descendants())
            {
                if (el.HasElements) continue;

                var key = (el.Name.LocalName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var value = (el.Value ?? "").Trim();

                // se repetir chave dentro do mesmo produto, junta com " | "
                if (row.TryGetValue(key, out var existente) && !string.IsNullOrWhiteSpace(existente))
                {
                    if (!string.Equals(existente, value, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                        row[key] = existente + " | " + value;
                }
                else
                {
                    row[key] = value;
                }

                setColunas.Add(key);
            }

            // garante que pelo menos tem algo
            if (row.Count > 0)
                linhas.Add(row);
        }

        // Colunas: coloca nItem primeiro se existir
        var cols = setColunas.ToList();
        cols.Sort(StringComparer.OrdinalIgnoreCase);

        if (cols.Contains("nItem", StringComparer.OrdinalIgnoreCase))
        {
            cols.RemoveAll(c => c.Equals("nItem", StringComparison.OrdinalIgnoreCase));
            cols.Insert(0, "nItem");
        }

        colunas = cols;
    }

    // FALLBACK (1 registro só):
    private static List<KeyValuePair<string, string>> ExtrairTagsEValoresAntigo(string xmlPath)
    {
        try
        {
            var text = File.ReadAllText(xmlPath);

            var doc = XDocument.Parse(text, System.Xml.Linq.LoadOptions.None);
            var root = doc.Root;
            if (root == null) return new List<KeyValuePair<string, string>>();

            var result = new List<KeyValuePair<string, string>>();
            var contador = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var el in root.Descendants())
            {
                if (el.HasElements) continue;

                var keyBase = el.Name.LocalName?.Trim();
                if (string.IsNullOrWhiteSpace(keyBase))
                    continue;

                var value = (el.Value ?? "").Trim();

                if (!contador.ContainsKey(keyBase))
                    contador[keyBase] = 0;

                contador[keyBase]++;

                var key = contador[keyBase] == 1 ? keyBase : $"{keyBase}_{contador[keyBase]}";
                result.Add(new KeyValuePair<string, string>(key, value));
            }

            return result;
        }
        catch
        {
            return new List<KeyValuePair<string, string>>();
        }
    }

    // Nome único de aba:
    private static string MakeUniqueSheetName(XLWorkbook wb, string baseName)
    {
        string name = SanitizeSheetName(baseName);
        if (name.Length == 0) name = "Planilha";

        if (!wb.Worksheets.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return name;

        int i = 2;
        while (true)
        {
            var attempt = name;
            var suffix = $"_{i}";
            if (attempt.Length + suffix.Length > 31)
                attempt = attempt.Substring(0, Math.Max(0, 31 - suffix.Length));
            attempt += suffix;

            if (!wb.Worksheets.Any(s => s.Name.Equals(attempt, StringComparison.OrdinalIgnoreCase)))
                return attempt;

            i++;
        }
    }

    private static string SanitizeSheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Planilha";
        name = Regex.Replace(name, @"[\[\]\*:/\\\?]", "_");
        name = name.Trim();
        if (name.Length > 31) name = name.Substring(0, 31);
        return name;
    }
}
