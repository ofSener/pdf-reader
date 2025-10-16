using Pdf_Reader.Helpers;
using System.Globalization;

namespace Pdf_Reader.Services.Extractors;

/// <summary>
/// Para/Prim extraction servisi
/// </summary>
public class MoneyExtractor : IFieldExtractor<decimal>
{
    private readonly ILogger<MoneyExtractor> _logger;

    public string ExtractorName => "MoneyExtractor";

    public MoneyExtractor(ILogger<MoneyExtractor> logger)
    {
        _logger = logger;
    }

    public decimal? Extract(string text, out double confidence)
    {
        confidence = 0;

        var matches = RegexPatterns.MoneyPattern.Matches(text);
        if (matches.Count == 0) return null;

        // İlk geçerli para değerini döndür
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (TryParseMoney(match.Value, out decimal result))
            {
                confidence = 0.7; // Generic money extraction düşük confidence
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Net prim çıkar
    /// </summary>
    public decimal? ExtractNetPremium(string text, out double confidence)
    {
        confidence = 0;

        // Neova Katılım pattern - "TOPLAM NET KATKI PRİMİ" or "NET KATKI PRİMİ" (colon optional)
        var neovaKatilimMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:TOPLAM\s+)?NET\s+KATKI\s+PR[İI]M[İI]\s*[:]?\s*([0-9.,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (neovaKatilimMatch.Success && TryParseMoney(neovaKatilimMatch.Groups[1].Value, out decimal neovaResult))
        {
            confidence = 0.95;
            return neovaResult;
        }

        // AXA Special Pattern - For AXA, find the LAST occurrence of "Net Prim"
        bool isAxa = text.ToUpper().Contains("AXA");
        if (isAxa)
        {
            _logger.LogDebug("AXA document detected for Net Prim extraction");

            // Find ALL occurrences of "Net Prim" with values
            var allMatches = System.Text.RegularExpressions.Regex.Matches(text,
                @"Net\s+Prim\s+([\d.,]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            _logger.LogDebug($"AXA: Found {allMatches.Count} 'Net Prim' occurrences");

            // Get the LAST match (which should be in the summary table)
            if (allMatches.Count > 0)
            {
                var lastMatch = allMatches[allMatches.Count - 1];

                if (TryParseMoney(lastMatch.Groups[1].Value, out decimal axaNetResult))
                {
                    confidence = 0.98;
                    _logger.LogInformation($"AXA Last Net Prim Pattern matched - Net Premium: {axaNetResult}");
                    return axaNetResult;
                }
            }
        }

        // "Vergi Öncesi Prim" formatı - Allianz table format (value on next line or after whitespace)
        // Format: "Vergi Öncesi Prim YSV* BSM Vergisi* Tutar\n4.525,97 TL" or "105,74 USD"
        // Supports both TL and USD (or no currency)
        var vergiOncesiTableMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Vergi\s+[ÖO]ncesi\s+Prim[\s\S]{0,100}?([0-9]{1,3}(?:[.,][0-9]{3})*,[0-9]{2})\s*(?:TL|USD|EUR)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (vergiOncesiTableMatch.Success && TryParseMoney(vergiOncesiTableMatch.Groups[1].Value, out decimal vergiTableResult))
        {
            confidence = 0.95;
            _logger.LogInformation($"Vergi Öncesi Prim (table) pattern matched - Net Premium: {vergiTableResult}");
            return vergiTableResult;
        }

        // "Vergi Öncesi Prim" formatı (Anadolu gibi) - Direct format with colon
        var vergiOncesiMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Vergi\s+[ÖO]ncesi\s+Prim\s*[:]?\s*([0-9.,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (vergiOncesiMatch.Success && TryParseMoney(vergiOncesiMatch.Groups[1].Value, out decimal result3))
        {
            confidence = 0.95;
            return result3;
        }

        var match = RegexPatterns.NetPremiumPattern.Match(text);

        if (match.Success && TryParseMoney(match.Groups[1].Value, out decimal result))
        {
            confidence = 0.95;
            return result;
        }

        // "Net :" formatı (Ak Sigorta gibi)
        var altMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Net\s*[:]\s*([0-9.,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (altMatch.Success && TryParseMoney(altMatch.Groups[1].Value, out decimal result2))
        {
            confidence = 0.90;
            return result2;
        }

        // Keyword proximity ile dene
        var value = RegexPatterns.ExtractByKeyword(text, "Net Prim");
        if (value != null && TryParseMoney(value, out result))
        {
            confidence = 0.85;
            return result;
        }

        return null;
    }

    /// <summary>
    /// Brüt prim çıkar
    /// </summary>
    public decimal? ExtractGrossPremium(string text, out double confidence)
    {
        confidence = 0;

        // Quick Hayat Sigortası pattern - Tabloda "Bitiş Tarihi Prim Tutarı" başlığından önceki değer
        // Format: ": 100.00 TL\nBitiş Tarihi Prim Tutarı"
        var quickHayatTableMatch = System.Text.RegularExpressions.Regex.Match(text,
            @":\s*([0-9.,]+)\s*TL[\s\r\n]+Biti[şs]\s+Tarihi\s+Prim\s+Tutar[ıi]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (quickHayatTableMatch.Success && TryParseMoney(quickHayatTableMatch.Groups[1].Value, out decimal quickTableResult))
        {
            confidence = 0.95;
            return quickTableResult;
        }

        // Quick Hayat Sigortası pattern - REVERSE: Value BEFORE "Prim Tutarı"
        // Format: ": 100.00 TL\n:15.09.2026\nBitiş Tarihi Prim Tutarı"
        var quickHayatReverseMatch = System.Text.RegularExpressions.Regex.Match(text,
            @":\s*([0-9.,]+)\s*TL[\s\r\n]+:[\d.]+[\s\r\n]+Biti[şs]\s+Tarihi\s+Prim\s+Tutar[ıi]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (quickHayatReverseMatch.Success && TryParseMoney(quickHayatReverseMatch.Groups[1].Value, out decimal quickRevResult))
        {
            confidence = 0.98;
            _logger.LogInformation($"Quick Hayat Reverse Pattern matched - Premium: {quickRevResult}");
            return quickRevResult;
        }

        // Quick Hayat Sigortası pattern - "Prim Tutarı" sonrasında değer (TL opsiyonel)
        // Format: "Prim Tutarı\n: 100.00 TL" veya "Prim Tutarı: 100.00 TL"
        var quickHayatMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Prim\s+Tutar[ıi]\s*[\r\n]*\s*[:]?\s*([0-9.,]+)(?:\s*TL)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (quickHayatMatch.Success && TryParseMoney(quickHayatMatch.Groups[1].Value, out decimal quickResult))
        {
            confidence = 0.95;
            return quickResult;
        }

        // "Toplam Prim" pattern (Allianz TSS gibi)
        // Format: "Toplam Prim : 51.305,41 TL"
        var toplamPrimMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Toplam\s+Prim\s*[:]?\s*([0-9.,]+)(?:\s*TL)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (toplamPrimMatch.Success && TryParseMoney(toplamPrimMatch.Groups[1].Value, out decimal toplamResult))
        {
            confidence = 0.95;
            _logger.LogInformation($"Toplam Prim Pattern matched - Premium: {toplamResult}");
            return toplamResult;
        }

        // Neova Katılım pattern - "BRÜT KATKI PRİMİ" (colon optional)
        var neovaKatilimMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"BR[ÜU]T\s+KATKI\s+PR[İI]M[İI]\s*[:]?\s*([0-9.,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (neovaKatilimMatch.Success && TryParseMoney(neovaKatilimMatch.Groups[1].Value, out decimal neovaResult))
        {
            confidence = 0.95;
            return neovaResult;
        }

        // Zeyil/Endorsement formatını dene (EKTEN GELEN GÜNCEL TAKSİT)
        // Format: "Peşin 0,00 11.830,00 11.830,00" - Son tutar alınmalı
        var zeyilMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Pe[şs]in\s+(\d{1,3}(?:[.,]\d{3})*,\d{2})\s+(\d{1,3}(?:[.,]\d{3})*,\d{2})\s+(\d{1,3}(?:[.,]\d{3})*,\d{2})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (zeyilMatch.Success)
        {
            // Üçüncü değeri al (SON ÖDEME TUTARI)
            if (TryParseMoney(zeyilMatch.Groups[3].Value, out decimal zeyilResult) && zeyilResult > 0)
            {
                confidence = 0.90;
                _logger.LogInformation($"Zeyil pattern matched - Gross Premium: {zeyilResult}");
                return zeyilResult;
            }
        }

        // "Poliçe Primi" pattern (DASK gibi) - HIGHEST PRIORITY for DASK
        var policePrimiMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Poli[çc]e\s+Primi\s*[:]\s*([0-9.,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (policePrimiMatch.Success && TryParseMoney(policePrimiMatch.Groups[1].Value, out decimal daskResult))
        {
            confidence = 0.95;
            return daskResult;
        }

        // AXA Special Pattern - HIGHEST PRIORITY for AXA
        // For AXA, find the LAST occurrence of "Ödenecek Prim" since it appears in the summary table
        bool isAxa = text.ToUpper().Contains("AXA");
        if (isAxa)
        {
            _logger.LogDebug("AXA document detected, looking for last Ödenecek Prim occurrence");

            // Find ALL occurrences of "Ödenecek Prim" with values
            var allMatches = System.Text.RegularExpressions.Regex.Matches(text,
                @"Ödenecek Prim\s+([\d.,]+)",
                System.Text.RegularExpressions.RegexOptions.None);

            _logger.LogDebug($"AXA: Found {allMatches.Count} 'Ödenecek Prim' occurrences");

            // Get the LAST match (which should be in the summary table)
            if (allMatches.Count > 0)
            {
                var lastMatch = allMatches[allMatches.Count - 1];

                if (TryParseMoney(lastMatch.Groups[1].Value, out decimal axaLastResult))
                {
                    confidence = 0.98;
                    _logger.LogInformation($"AXA Last Ödenecek Prim Pattern matched - Gross Premium: {axaLastResult}");
                    return axaLastResult;
                }
                else
                {
                    _logger.LogWarning($"AXA: Failed to parse money value: {lastMatch.Groups[1].Value}");
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("AXA: No 'Ödenecek Prim' patterns matched!");
                return null;
            }
        }

        // "Ödenecek Prim" specific pattern (Axa gibi) - HIGH PRIORITY
        // Match the exact format: "Ödenecek Prim 15.376,70"
        // Look for the value directly after "Ödenecek Prim" on the same or next line
        var axaPattern = System.Text.RegularExpressions.Regex.Match(text,
            @"[ÖO]denecek\s+Prim\s+([0-9]{1,3}(?:\.[0-9]{3})*,[0-9]{2})(?:\s|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (axaPattern.Success && TryParseMoney(axaPattern.Groups[1].Value, out decimal axaResult))
        {
            confidence = 0.95;
            return axaResult;
        }

        // Alternative Axa pattern - look for "Ödenecek Prim" followed by value on next line
        var axaAltPattern = System.Text.RegularExpressions.Regex.Match(text,
            @"[ÖO]denecek\s+Prim[\s\n\r]+([0-9]{1,3}(?:\.[0-9]{3})*,[0-9]{2})(?:\s|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        if (axaAltPattern.Success && TryParseMoney(axaAltPattern.Groups[1].Value, out decimal axaAltResult))
        {
            confidence = 0.95;
            return axaAltResult;
        }

        // "Ödenecek Tutar" formatı (Anadolu gibi) - Second priority
        var odenecekTutarMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"[ÖO]denecek\s+Tutar\s*[:]?\s*([0-9.,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (odenecekTutarMatch.Success && TryParseMoney(odenecekTutarMatch.Groups[1].Value, out decimal anadoluResult))
        {
            confidence = 0.95;
            return anadoluResult;
        }

        var match = RegexPatterns.GrossPremiumPattern.Match(text);

        if (match.Success && TryParseMoney(match.Groups[1].Value, out decimal result))
        {
            confidence = 0.95;
            return result;
        }

        // "Ödenecek :" formatı (Aksigorta gibi)
        var odenecekMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"[ÖO]denecek\s*[:]\s*([0-9.,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (odenecekMatch.Success && TryParseMoney(odenecekMatch.Groups[1].Value, out decimal result2))
        {
            confidence = 0.90;
            return result2;
        }

        // "Ödenecek Prim" in table with label on header row (Allianz)
        // The value is typically the largest amount near "Ödenecek Prim"
        // BUT skip if we're dealing with AXA format (direct label-value pattern)
        // Also skip if we already found a match with Axa patterns
        if (!axaPattern.Success && !axaAltPattern.Success)
        {
            var allianzMatches = System.Text.RegularExpressions.Regex.Matches(text,
                @"[ÖO]denecek\s+Prim[\s\S]{1,150}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (allianzMatches.Count > 0)
            {
                // Find all money values in the matched region
                var regionText = allianzMatches[0].Value;
                var moneyMatches = System.Text.RegularExpressions.Regex.Matches(regionText, @"([0-9.,]+)\s*TL");

                decimal maxValue = 0;
                foreach (System.Text.RegularExpressions.Match moneyMatch in moneyMatches)
                {
                    if (TryParseMoney(moneyMatch.Groups[1].Value, out decimal moneyValue) && moneyValue > maxValue)
                    {
                        maxValue = moneyValue;
                    }
                }

                if (maxValue > 100)
                {
                    confidence = 0.90;
                    return maxValue;
                }
            }
        }

        // Keyword proximity ile dene - BUT be careful with Axa format where "Ödenecek" might catch wrong values
        // For Axa, skip the fallback as we have specific patterns above
        if (!isAxa)
        {
            var value = RegexPatterns.ExtractByKeyword(text, "Brüt Prim");
            value ??= RegexPatterns.ExtractByKeyword(text, "BRÜT PRİM");
            value ??= RegexPatterns.ExtractByKeyword(text, "Ödenecek Prim");
            value ??= RegexPatterns.ExtractByKeyword(text, "ÖDENECEK PRİM");
            value ??= RegexPatterns.ExtractByKeyword(text, "Ödenecek Tutar");
            value ??= RegexPatterns.ExtractByKeyword(text, "Ödenecek");

            if (value != null && TryParseMoney(value, out result))
            {
                confidence = 0.85;
                return result;
            }
        }
        else
        {
            // For Axa, look for the specific table format
            // "Ödenecek Prim" appears on its own line with the value on the same line
            var specificAxaMatch = System.Text.RegularExpressions.Regex.Match(text,
                @"Ödenecek\s+Prim\s+(\d{1,3}(?:\.\d{3})*,\d{2})",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (specificAxaMatch.Success && TryParseMoney(specificAxaMatch.Groups[1].Value, out result))
            {
                confidence = 0.92;
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gider vergisi çıkar
    /// </summary>
    public decimal? ExtractTax(string text, out double confidence)
    {
        confidence = 0;
        var match = RegexPatterns.TaxPattern.Match(text);

        if (match.Success && TryParseMoney(match.Groups[1].Value, out decimal result))
        {
            confidence = 0.9;
            return result;
        }

        return null;
    }

    private bool TryParseMoney(string moneyStr, out decimal result)
    {
        result = 0;

        try
        {
            // Türk formatı: 1.234,56 veya uluslararası: 1,234.56
            moneyStr = moneyStr.Trim();

            // Negative değerleri handle et (İptal/İade zeyilleri için)
            bool isNegative = moneyStr.StartsWith("-");
            if (isNegative)
            {
                moneyStr = moneyStr.Substring(1).Trim();
            }

            // Hangi format olduğunu anla
            bool isTurkishFormat = moneyStr.Contains(',') && moneyStr.LastIndexOf(',') > moneyStr.LastIndexOf('.');

            if (isTurkishFormat)
            {
                // Türk formatı: 1.234,56 -> 1234.56
                moneyStr = moneyStr.Replace(".", "").Replace(',', '.');
            }
            else
            {
                // Uluslararası format: 1,234.56 -> 1234.56
                moneyStr = moneyStr.Replace(",", "");
            }

            result = decimal.Parse(moneyStr, CultureInfo.InvariantCulture);

            // Negatif değerleri absolute value yap (İptal/İade zeyillerinde)
            // Validation için pozitif değer kullanılmalı
            if (isNegative)
            {
                result = Math.Abs(result);
                _logger.LogDebug($"Negative premium value detected, using absolute value: {result}");
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
