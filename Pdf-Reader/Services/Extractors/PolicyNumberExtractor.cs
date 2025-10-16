using Pdf_Reader.Helpers;

namespace Pdf_Reader.Services.Extractors;

/// <summary>
/// Poliçe numarası extraction servisi
/// </summary>
public class PolicyNumberExtractor : IFieldExtractor
{
    private readonly ILogger<PolicyNumberExtractor> _logger;

    public string ExtractorName => "PolicyNumberExtractor";

    public PolicyNumberExtractor(ILogger<PolicyNumberExtractor> logger)
    {
        _logger = logger;
    }

    public string? Extract(string text, out double confidence)
    {
        confidence = 0;

        // Quick Hayat Sigortası formatı (Sertifika Numarası: 1038197)
        var quickHayatMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Sertifika\s+Numaras[ıi]\s*[:]?\s*(\d{6,15})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (quickHayatMatch.Success)
        {
            confidence = 0.95;
            return quickHayatMatch.Groups[1].Value;
        }

        // Unico Ferdi Kaza formatı (Poliçenin Suretidir. 182832580-2460951-091326)
        var unicoFerdiMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Poli[çc]enin\s+Suretidir[.]\s+([\d-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (unicoFerdiMatch.Success)
        {
            confidence = 0.95;
            return unicoFerdiMatch.Groups[1].Value;
        }

        // Unico formatını dene (138573715 / 0 formatı)
        var unicoMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"KARAYOLLARI.*?(\d{8,12})\s*/\s*\d",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        if (unicoMatch.Success)
        {
            confidence = 0.95;
            return unicoMatch.Groups[1].Value;
        }

        // Önce Doğa formatını dene (en spesifik)
        var dogaMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Poli[çc]e\s*/\s*Yenileme\s*/\s*Zeyil\s*/\s*[ÜU]r[üu]n\s*No\s*:\s*(\d{6,15})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (dogaMatch.Success)
        {
            confidence = 0.95;
            return dogaMatch.Groups[1].Value;
        }

        // Neova formatını dene (POLİÇE / YENİLEME NO : 438891339)
        var neovaMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"POL[İI][ÇC]E\s*/\s*YEN[İI]LEME\s+NO\s*[:]?\s*(\d{6,15})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (neovaMatch.Success)
        {
            confidence = 0.95;
            _logger.LogInformation($"Neova Poliçe/Yenileme No pattern matched: {neovaMatch.Groups[1].Value}");
            return neovaMatch.Groups[1].Value;
        }

        var match = RegexPatterns.PolicyNumberPattern.Match(text);
        if (match.Success)
        {
            var policyNumber = match.Groups[1].Value.Trim();

            // Uzunluk kontrolü (6-30 hane arası - prefix dahil)
            if (policyNumber.Length >= 6 && policyNumber.Length <= 30)
            {
                confidence = 0.9;
                return policyNumber;
            }
        }

        // Alternatif: "Poliçe" kelimesinin yakınındaki sayıları ara (prefix desteği ile)
        var keywords = new[] { "Poliçe No", "Police No", "POLİÇE NO", "Poliçe Numara", "Policy No" };
        foreach (var keyword in keywords)
        {
            var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var searchText = text.Substring(index, Math.Min(150, text.Length - index));

                // Önce prefix'li format dene (T-382492524-0-0 gibi)
                var prefixMatch = System.Text.RegularExpressions.Regex.Match(searchText, @"[A-Z]?-?\d{6,20}(?:-\d+)*");
                if (prefixMatch.Success && prefixMatch.Value.Length >= 6)
                {
                    confidence = 0.9;
                    return prefixMatch.Value;
                }

                // Sonra sadece rakam dene
                var numberMatch = System.Text.RegularExpressions.Regex.Match(searchText, @"\d{6,20}");
                if (numberMatch.Success)
                {
                    confidence = 0.85;
                    return numberMatch.Value;
                }
            }
        }

        // Tablo formatında ara (kolonlarda)
        var tableMatch = System.Text.RegularExpressions.Regex.Match(text, @"[A-Z]?-?\d{6,20}(?:-\d+)*(?=\s+[A-Z0-9]{2,10}\s+\d{2}/\d{2}/\d{4})");
        if (tableMatch.Success)
        {
            confidence = 0.85;
            return tableMatch.Value;
        }

        return null;
    }
}
