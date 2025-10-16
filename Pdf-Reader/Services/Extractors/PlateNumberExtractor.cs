using Pdf_Reader.Helpers;

namespace Pdf_Reader.Services.Extractors;

/// <summary>
/// Plaka numarası extraction servisi
/// </summary>
public class PlateNumberExtractor : IFieldExtractor
{
    private readonly ILogger<PlateNumberExtractor> _logger;

    public string ExtractorName => "PlateNumberExtractor";

    public PlateNumberExtractor(ILogger<PlateNumberExtractor> logger)
    {
        _logger = logger;
    }

    public string? Extract(string text, out double confidence)
    {
        confidence = 0;

        // Önce Doğa formatını dene: "Plaka       :016MA0437" (çok boşluk olabilir)
        var dogaMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Plaka\s*:\s*(0?\d{2}[A-Z]{1,3}\d{2,4})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (dogaMatch.Success)
        {
            var plate = dogaMatch.Groups[1].Value.Replace(" ", "").ToUpper();
            if (IsValidTurkishPlate(plate))
            {
                confidence = 0.95;
                return FormatPlate(plate);
            }
        }

        // Önce "Plaka No :" keyword'üyle dene
        var plateKeywords = new[] { "Plaka No", "Plaka", "Plate No" };
        foreach (var keyword in plateKeywords)
        {
            var keywordIndex = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (keywordIndex >= 0)
            {
                var searchStart = keywordIndex + keyword.Length;
                var searchEnd = Math.Min(searchStart + 50, text.Length);
                var searchText = text.Substring(searchStart, searchEnd - searchStart);

                // Keyword'den sonraki ilk plaka formatını bul
                var proximityMatch = RegexPatterns.PlateNumberPattern.Match(searchText);
                if (proximityMatch.Success)
                {
                    var plate = proximityMatch.Groups[1].Value.Replace(" ", "").ToUpper();
                    if (IsValidTurkishPlate(plate))
                    {
                        confidence = 0.95;
                        return FormatPlate(plate);
                    }
                }
            }
        }

        // Fallback: genel pattern matching
        var match = RegexPatterns.PlateNumberPattern.Match(text);
        if (match.Success)
        {
            var plate = match.Groups[1].Value.Replace(" ", "").ToUpper();

            // Türk plaka format kontrolü (34ABC123 formatı)
            if (IsValidTurkishPlate(plate))
            {
                confidence = 0.9;
                return FormatPlate(plate);
            }
        }

        return null;
    }

    private bool IsValidTurkishPlate(string plate)
    {
        // En az 6, en fazla 9 karakter (016MA0437 gibi leading zero olabilir)
        if (plate.Length < 6 || plate.Length > 9) return false;

        // İlk 2-3 karakter rakam olmalı (il kodu, leading zero olabilir)
        if (!char.IsDigit(plate[0]) || !char.IsDigit(plate[1])) return false;

        return true;
    }

    private string FormatPlate(string plate)
    {
        // Leading zero'yu temizle: 016MA0437 -> 16MA0437
        if (plate.Length > 0 && plate[0] == '0' && char.IsDigit(plate[1]))
        {
            plate = plate.Substring(1);
        }

        // 34ABC123 -> 34 ABC 123 formatına çevir
        if (plate.Length >= 7)
        {
            var ilKodu = plate.Substring(0, 2);
            var remaining = plate.Substring(2);

            // Harf ve rakam kısmını ayır
            int digitStart = 0;
            for (int i = 0; i < remaining.Length; i++)
            {
                if (char.IsDigit(remaining[i]))
                {
                    digitStart = i;
                    break;
                }
            }

            if (digitStart > 0)
            {
                var letters = remaining.Substring(0, digitStart);
                var numbers = remaining.Substring(digitStart);
                return $"{ilKodu} {letters} {numbers}";
            }
        }

        return plate;
    }
}
