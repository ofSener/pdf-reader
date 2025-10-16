using Pdf_Reader.Helpers;
using System.Globalization;

namespace Pdf_Reader.Services.Extractors;

/// <summary>
/// Tarih extraction servisi
/// </summary>
public class DateExtractor : IFieldExtractor<DateTime>
{
    private readonly ILogger<DateExtractor> _logger;

    public string ExtractorName => "DateExtractor";

    public DateExtractor(ILogger<DateExtractor> logger)
    {
        _logger = logger;
    }

    public DateTime? Extract(string text, out double confidence)
    {
        confidence = 0;

        var matches = RegexPatterns.DatePattern.Matches(text);
        if (matches.Count == 0) return null;

        // İlk bulduğu geçerli tarihi döndür
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (TryParseDate(match.Value, out DateTime result))
            {
                // Makul tarih aralığında mı kontrol et (1990-2050)
                if (result.Year >= 1990 && result.Year <= 2050)
                {
                    confidence = 0.9;
                    return result;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Başlangıç tarihi çıkar
    /// </summary>
    public DateTime? ExtractStartDate(string text, out double confidence)
    {
        confidence = 0;

        // HDI formatı: "Başlangıç-Bitiş Tarihi 19/10/2025-19/10/2026"
        var hdiCombinedMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"BA[ŞS]LANGI[ÇC][\s\-]+B[İI]T[İI][ŞS]\s+TAR[İI]H[İI]\s+(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})[\s\-]+(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (hdiCombinedMatch.Success && TryParseDate(hdiCombinedMatch.Groups[1].Value, out DateTime hdiResult))
        {
            confidence = 0.98;
            _logger.LogInformation($"HDI combined format matched - Start Date: {hdiResult:dd/MM/yyyy}");
            return hdiResult;
        }

        var match = RegexPatterns.StartDatePattern.Match(text);

        if (match.Success && TryParseDate(match.Groups[1].Value, out DateTime result))
        {
            confidence = 0.95;
            return result;
        }

        // Tablo formatı: "BAŞLANGIÇ TARİHİ" kolonunda
        var tableMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"BA[ŞS]LANGI[ÇC]\s+TAR[İI]H[İI][:\s]+(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (tableMatch.Success && TryParseDate(tableMatch.Groups[1].Value, out DateTime result2))
        {
            confidence = 0.90;
            return result2;
        }

        // Fallback: ilk geçerli tarih
        return Extract(text, out confidence);
    }

    /// <summary>
    /// Bitiş tarihi çıkar
    /// </summary>
    public DateTime? ExtractEndDate(string text, out double confidence)
    {
        confidence = 0;

        // HDI formatı: "Başlangıç-Bitiş Tarihi 19/10/2025-19/10/2026" - ikinci tarihi al
        var hdiCombinedMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"BA[ŞS]LANGI[ÇC][\s\-]+B[İI]T[İI][ŞS]\s+TAR[İI]H[İI]\s+(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})[\s\-]+(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (hdiCombinedMatch.Success && TryParseDate(hdiCombinedMatch.Groups[2].Value, out DateTime hdiResult))
        {
            confidence = 0.98;
            _logger.LogInformation($"HDI combined format matched - End Date: {hdiResult:dd/MM/yyyy}");
            return hdiResult;
        }

        var match = RegexPatterns.EndDatePattern.Match(text);

        if (match.Success && TryParseDate(match.Groups[1].Value, out DateTime result))
        {
            confidence = 0.95;
            return result;
        }

        // Tablo formatı: "BİTİŞ TARİHİ" kolonunda
        var tableMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"B[İI]T[İI][ŞS]\s+TAR[İI]H[İI][:\s]+(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (tableMatch.Success && TryParseDate(tableMatch.Groups[1].Value, out DateTime result2))
        {
            confidence = 0.90;
            return result2;
        }

        // Alternatif: Başlangıç tarihinden sonraki ilk tarih
        var startDate = ExtractStartDate(text, out double _);
        if (startDate.HasValue)
        {
            var allDates = System.Text.RegularExpressions.Regex.Matches(text, @"\b(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})\b");
            foreach (System.Text.RegularExpressions.Match dateMatch in allDates)
            {
                if (TryParseDate(dateMatch.Groups[1].Value, out DateTime possibleEndDate))
                {
                    if (possibleEndDate > startDate.Value && possibleEndDate.Year >= 1990 && possibleEndDate.Year <= 2050)
                    {
                        confidence = 0.75;
                        return possibleEndDate;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Tanzim tarihi çıkar
    /// </summary>
    public DateTime? ExtractIssueDate(string text, out double confidence)
    {
        confidence = 0;
        var match = RegexPatterns.IssueDatePattern.Match(text);

        if (match.Success && TryParseDate(match.Groups[1].Value, out DateTime result))
        {
            confidence = 0.95;
            return result;
        }

        // Alternatif format: "Tanzim Tarihi :" formatı
        var altMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Tanzim\s+Tarihi\s*[:\s]+(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (altMatch.Success && TryParseDate(altMatch.Groups[1].Value, out DateTime result2))
        {
            confidence = 0.90;
            return result2;
        }

        // Allianz formatı: "29/08/2025 TARİHİNDE...TANZİM EDİLMİŞTİR"
        var allianzMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(\d{1,2}[./\-]\d{1,2}[./\-]\d{4}).*TAR[İI]H[İI]NDE.*TANZ[İI]M",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (allianzMatch.Success && TryParseDate(allianzMatch.Groups[1].Value, out DateTime result3))
        {
            confidence = 0.95;
            return result3;
        }

        // Anadolu formatı: "Düzenleme Tarihi" başlık, tarih alt satırda olabilir
        var anadoluMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"D[üu]zenle[nm]e\s+Tarihi[\s\S]{0,100}?(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (anadoluMatch.Success && TryParseDate(anadoluMatch.Groups[1].Value, out DateTime result4))
        {
            confidence = 0.95;
            return result4;
        }

        return null;
    }

    private bool TryParseDate(string dateStr, out DateTime result)
    {
        result = DateTime.MinValue;

        // Farklı ayırıcıları normalize et
        dateStr = dateStr.Replace('.', '/').Replace('-', '/');

        // Önce DD/MM/YYYY formatını dene
        if (DateTime.TryParseExact(dateStr, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        // Sonra diğer formatları dene
        string[] formats = { "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy" };
        return DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
