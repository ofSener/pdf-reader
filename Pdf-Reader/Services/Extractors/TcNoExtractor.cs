using Pdf_Reader.Helpers;

namespace Pdf_Reader.Services.Extractors;

/// <summary>
/// TC Kimlik No extraction servisi
/// </summary>
public class TcNoExtractor : IFieldExtractor
{
    private readonly ILogger<TcNoExtractor> _logger;

    public string ExtractorName => "TcNoExtractor";

    public TcNoExtractor(ILogger<TcNoExtractor> logger)
    {
        _logger = logger;
    }

    public string? Extract(string text, out double confidence)
    {
        confidence = 0;

        var matches = RegexPatterns.TcNoPattern.Matches(text);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var tcNo = match.Value;

            // TC No algoritması ile validate et
            if (IsValidTcNo(tcNo))
            {
                confidence = 0.95;
                return tcNo;
            }
        }

        return null;
    }

    /// <summary>
    /// TC Kimlik No algoritması ile validasyon
    /// </summary>
    private bool IsValidTcNo(string tcNo)
    {
        if (string.IsNullOrEmpty(tcNo) || tcNo.Length != 11)
            return false;

        // İlk hane 0 olamaz
        if (tcNo[0] == '0')
            return false;

        // Tüm karakterler rakam olmalı
        if (!tcNo.All(char.IsDigit))
            return false;

        try
        {
            int[] digits = tcNo.Select(c => int.Parse(c.ToString())).ToArray();

            // 10. hane kontrolü
            int sum1 = (digits[0] + digits[2] + digits[4] + digits[6] + digits[8]) * 7;
            int sum2 = digits[1] + digits[3] + digits[5] + digits[7];
            int digit10 = (sum1 - sum2) % 10;

            if (digits[9] != digit10)
                return false;

            // 11. hane kontrolü
            int sum3 = digits.Take(10).Sum();
            int digit11 = sum3 % 10;

            return digits[10] == digit11;
        }
        catch
        {
            return false;
        }
    }
}
