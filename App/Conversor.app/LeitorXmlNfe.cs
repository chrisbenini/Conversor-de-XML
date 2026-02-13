using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System;

// Chama o código principal
namespace ConversorNFe.App;

public static class LeitorXmlNfe
{
    // Namespace padrão da NF-e (SEFAZ)
    private static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";

    // Lê o XML e devolve uma lista de itens (<det>) já com cálculos (ST/IPI e totais)
    public static List<Dictionary<string, object?>> ExtrairItensComCalculos(string xmlPath)
    {
        // Garante que o arquivo existe
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException("XML não encontrado.", xmlPath);

        // Carrega o XML completo
        var doc = XDocument.Load(xmlPath);

        // Pega a raiz do documento (ex.: <nfeProc> / <NFe>)
        var root = doc.Root ?? throw new InvalidOperationException("XML inválido (sem raiz).");

        // Localiza todos os itens da nota: <det>
        var itens = root.Descendants(Ns + "det").ToList();

        // Aqui vira uma “tabela”, cada item vira uma linha (dicionário)
        var rows = new List<Dictionary<string, object?>>();

        foreach (var det in itens)
        {
            // Dentro do item: dados do produto e impostos
            var prod = det.Element(Ns + "prod");
            var imposto = det.Element(Ns + "imposto");

            // Sem <prod> não dá pra montar o item
            if (prod == null) continue;

            // Campos principais do produto
            string? ean = GetText(prod, Ns + "cEAN");
            string? produto = GetText(prod, Ns + "xProd");
            string? ncm = GetText(prod, Ns + "NCM");
            string? cest = GetText(prod, Ns + "CEST");

            // Valores do produto (quantidade, total, desconto, unitário)
            decimal quantidade = GetDecimal(prod, Ns + "qTrib");
            decimal valorTotal = GetDecimal(prod, Ns + "vProd");
            decimal desconto = GetDecimal(prod, Ns + "vDesc");
            decimal vlrUnit = GetDecimal(prod, Ns + "vUnTrib");

            // Impostos: procura em qualquer lugar dentro de <imposto> pelo nome da tag
            decimal ipi = GetDecimalAny(imposto, "vIPI");
            decimal totalSt = GetDecimalAny(imposto, "vICMSST");

            // Cálculos derivados
            decimal valorUnitSt = quantidade > 0 ? totalSt / quantidade : 0m;
            decimal valorLiquido = valorTotal - desconto;
            decimal valorUnitIpi = quantidade > 0 ? ipi / quantidade : 0m;
            decimal totalLiquido = valorLiquido + totalSt + ipi;
            decimal valorUnitTotal = quantidade > 0 ? totalLiquido / quantidade : 0m;

            // Monta a linha final (colunas da planilha)
            rows.Add(new Dictionary<string, object?>
            {
                ["EAN"] = ean,
                ["PRODUTO"] = produto,
                ["NCM"] = ncm,
                ["CEST"] = cest,
                ["QUANTIDADE"] = Round2(quantidade),
                ["VALOR UNITARIO"] = Round2(vlrUnit),
                ["VALOR BRUTO"] = Round2(valorTotal),
                ["DESCONTO"] = Round2(desconto),
                ["VALOR SEM ST"] = Round2(valorLiquido),
                ["VALOR UNITARIO ST"] = Round2(valorUnitSt),
                ["VALOR TOTAL ST"] = Round2(totalSt),
                ["VALOR UNITARIO IPI"] = Round2(valorUnitIpi),
                ["VALOR TOTAL IPI"] = Round2(ipi),
                ["VALOR TOTAL LIQUIDO"] = Round2(totalLiquido),
                ["VALOR UNITARIO LIQUIDO"] = Round2(valorUnitTotal),
            });
        }

        // Retorna todas as linhas (itens) calculadas
        return rows;
    }

    // Pega texto de uma tag específica (com namespace)
    private static string? GetText(XElement parent, XName name)
        => parent.Element(name)?.Value?.Trim();

    // Pega decimal de uma tag específica (com namespace)
    private static decimal GetDecimal(XElement parent, XName name)
        => ParseDecimal(parent.Element(name)?.Value);

    // Pega decimal procurando por LocalName (serve quando a estrutura muda dentro de <imposto>)
    private static decimal GetDecimalAny(XElement? parent, string localName)
    {
        if (parent == null) return 0m;

        // Procura em qualquer descendente pela tag (sem depender do namespace exato)
        var el = parent.Descendants()
                       .FirstOrDefault(x => x.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

        return ParseDecimal(el?.Value);
    }

    // Converte string em decimal aceitando ponto ou vírgula
    private static decimal ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0m;

        s = s.Trim();

        // Tenta no padrão com ponto (InvariantCulture)
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;

        // Se vier com vírgula, normaliza e tenta de novo
        s = s.Replace(",", ".");
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
            return v;

        return 0m;
    }

    // Arredonda para 2 casas (padrão monetário)
    private static decimal Round2(decimal v) => Math.Round(v, 2);
}
