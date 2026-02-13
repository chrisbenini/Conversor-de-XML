using System.Text.RegularExpressions;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using System.Text;
using System.IO;
using System;

// chama o código principal
namespace ConversorNFe.App;

public static class ExtratorXmlDePdf
{
    // Extrai um XML do arquivo (PDF real ou arquivo renomeado),
    // salva em um .xml temporário e devolve o caminho.

    public static string ExtrairXmlParaArquivoTemporario(string caminhoPdf)
    {
        // validações básicas
        if (string.IsNullOrWhiteSpace(caminhoPdf))
            throw new ArgumentException("Caminho do arquivo inválido.");

        if (!File.Exists(caminhoPdf))
            throw new FileNotFoundException("Arquivo não encontrado.", caminhoPdf);

        // pega o XML em texto
        var xmlString = ExtrairXmlStringDoArquivo(caminhoPdf);

        if (string.IsNullOrWhiteSpace(xmlString))
            throw new InvalidOperationException("Não encontrei XML embutido neste arquivo.");

        // salva em arquivo temporário .xml
        var tempPath = Path.Combine(Path.GetTempPath(), $"xml_{Guid.NewGuid():N}.xml");
        File.WriteAllText(tempPath, xmlString, new UTF8Encoding(false));
        return tempPath;
    }

    private static string ExtrairXmlStringDoArquivo(string caminho)
    {
        // Se for PDF de verdade, extrai texto das páginas
        if (ParecePdfDeVerdade(caminho))
        {
            var texto = ExtrairTextoDeTodasPaginas(caminho);
            return ExtrairMelhorBlocoXml(texto);
        }

        // Se não for PDF real, lê como texto normal
        var raw = File.ReadAllText(caminho, Encoding.UTF8);
        return ExtrairMelhorBlocoXml(raw);
    }

    private static bool ParecePdfDeVerdade(string caminho)
    {
        // checa se começa com "%PDF-"
        try
        {
            using var fs = File.OpenRead(caminho);
            var buf = new byte[5];
            var n = fs.Read(buf, 0, buf.Length);
            if (n < 5) return false;

            return buf[0] == (byte)'%' &&
                   buf[1] == (byte)'P' &&
                   buf[2] == (byte)'D' &&
                   buf[3] == (byte)'F' &&
                   buf[4] == (byte)'-';
        }
        catch
        {
            return false;
        }
    }

    private static string ExtrairTextoDeTodasPaginas(string caminhoPdf)
    {
        // usa iText pra ler texto de cada página
        var sb = new StringBuilder();

        using var reader = new PdfReader(caminhoPdf);
        using var pdf = new PdfDocument(reader);

        for (int p = 1; p <= pdf.GetNumberOfPages(); p++)
        {
            var page = pdf.GetPage(p);
            var t = PdfTextExtractor.GetTextFromPage(page);

            if (!string.IsNullOrWhiteSpace(t))
                sb.AppendLine(t);
        }

        return sb.ToString();
    }

    private static string ExtrairMelhorBlocoXml(string texto)
    {
        // tenta pegar NF-e primeiro (nfeProc/NFe)
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var firstLt = texto.IndexOf('<');
        if (firstLt > 0)
            texto = texto.Substring(firstLt);

        var nfe = ExtrairBlocoPorTag(texto, "nfeProc");
        if (!string.IsNullOrWhiteSpace(nfe)) return nfe;

        nfe = ExtrairBlocoPorTag(texto, "NFe");
        if (!string.IsNullOrWhiteSpace(nfe)) return nfe;

        // se não for NF-e, tenta um XML genérico
        return ExtrairXmlGenerico(texto);
    }

    private static string ExtrairBlocoPorTag(string texto, string tag)
    {
        // recorta do <tag ...> até </tag>
        var start = texto.IndexOf("<" + tag, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "";

        var end = texto.IndexOf("</" + tag + ">", start, StringComparison.OrdinalIgnoreCase);
        if (end <= start) return "";

        return texto.Substring(start, (end - start) + ("</" + tag + ">").Length);
    }

    private static string ExtrairXmlGenerico(string texto)
    {
        // tenta começar em <?xml ...?>, senão no primeiro "<"
        var start = texto.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            start = texto.IndexOf('<');

        if (start < 0) return "";

        var candidate = texto.Substring(start);

        // tenta descobrir o root do XML
        var m = Regex.Match(candidate, @"<\s*([A-Za-z_][A-Za-z0-9_:\-\.]*)\b[^>]*>", RegexOptions.Singleline);
        if (!m.Success) return "";

        var root = m.Groups[1].Value;
        var close = "</" + root + ">";
        var end = candidate.LastIndexOf(close, StringComparison.OrdinalIgnoreCase);

        if (end > 0)
        {
            end += close.Length;
            return candidate.Substring(0, end);
        }

        // fallback: corta até o último ">"
        var lastGt = candidate.LastIndexOf('>');
        if (lastGt > 0)
            return candidate.Substring(0, lastGt + 1);

        return "";
    }
}
