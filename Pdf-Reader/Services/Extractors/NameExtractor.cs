using Pdf_Reader.Helpers;

namespace Pdf_Reader.Services.Extractors;

/// <summary>
/// İsim extraction servisi
/// </summary>
public class NameExtractor : IFieldExtractor
{
    private readonly ILogger<NameExtractor> _logger;

    public string ExtractorName => "NameExtractor";

    public NameExtractor(ILogger<NameExtractor> logger)
    {
        _logger = logger;
    }

    public string? Extract(string text, out double confidence)
    {
        confidence = 0;

        // Önce Doğa formatını dene: "Sigortalı\nAMIR FARES" (tek başına satırda)
        var dogaMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Sigortal[ıi]\s*\n\s*([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜa-zçğıöşü\s]+?)(?=\s*\n)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (dogaMatch.Success)
        {
            var name = dogaMatch.Groups[1].Value.Trim();
            if (IsValidName(name))
            {
                confidence = 0.90;
                return NormalizeName(name);
            }
        }

        // Önce "Adı Soyadı / Ünvanı :" formatını dene (Ak Sigorta, Anadolu gibi)
        // Özel karakterleri de destekle (Adõ Soyadõ)
        // Newline veya başka keyword'e kadar al
        var titleMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"Ad[ıõi]\s+Soyad[ıõi]\s*/\s*[ÜU]nvan[ıõi]\s*[:\s]+([A-ZÇĞİÖŞÜ][a-zçğıöşüA-ZÇĞİÖŞÜ\s]+?)(?=\s*\n|\s+MAH|\s+CAD|\s+SOK|TC\s|İletişim)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (titleMatch.Success)
        {
            var name = titleMatch.Groups[1].Value.Trim();
            if (IsValidName(name))
            {
                confidence = 0.90;
                return NormalizeName(name);
            }
        }

        // Pattern ile dene
        var match = RegexPatterns.NamePattern.Match(text);
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            if (IsValidName(name))
            {
                confidence = 0.85;
                return NormalizeName(name);
            }
        }

        // Keyword proximity ile dene (özel karakterleri de içeren varyasyonlar)
        var keywords = new[] { "Adı Soyadı", "Adõ Soyadõ", "İsim", "Sigortalı", "Ad Soyad", "Unvanı", "Unvanõ" };
        foreach (var keyword in keywords)
        {
            var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var searchText = text.Substring(index, Math.Min(200, text.Length - index));

                // Keyword'den sonraki ilk büyük harfle başlayan kelime grupunu bul (tamamı büyük harf de olabilir)
                var nameMatch = System.Text.RegularExpressions.Regex.Match(
                    searchText,
                    @"[:\s]+([A-ZÇĞİÖŞÜ][a-zçğıöşüA-ZÇĞİÖŞÜ]+(?:\s+[A-ZÇĞİÖŞÜ][a-zçğıöşüA-ZÇĞİÖŞÜ]+)+)"
                );

                if (nameMatch.Success)
                {
                    var name = nameMatch.Groups[1].Value.Trim();
                    if (IsValidName(name))
                    {
                        confidence = 0.75;
                        return NormalizeName(name);
                    }
                }
            }
        }

        return null;
    }

    private bool IsValidName(string name)
    {
        // En az 2 kelime olmalı
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        // Çok uzun isimler muhtemelen yanlış parse edilmiş
        if (parts.Length > 5) return false;

        // Her kelime en az 2 karakter olmalı
        if (!parts.All(p => p.Length >= 2)) return false;

        // TC, VD, VKN, Uavt, İletişim gibi kısaltmalar ve adres kelimeleri isim içinde bulunmamalı
        var invalidKeywords = new[] { "TC", "VD", "VKN", "LTD", "ŞTİ", "A.Ş", "LİMİTED", "UAVT", "İLETİŞİM", "İLETISIM", "ADRESİ", "ADRESI", "MAH", "MAHALLE", "MAHALLESİ", "CAD", "CADDE", "CADDESİ", "SOK", "SOKAK", "SOKAĞI", "CEP", "TELEFON", "TELEFONU", "SABİT", "SABIT" };
        foreach (var keyword in invalidKeywords)
        {
            if (parts.Any(p => p.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    private string NormalizeName(string name)
    {
        // TC, VD, VKN, Uavt, İletişim gibi kısaltmaları ve adres kelimelerini temizle
        var cleanName = System.Text.RegularExpressions.Regex.Replace(
            name,
            @"\b(TC|VD|VKN|LTD|ŞTİ|A\.Ş|LİMİTED|UAVT|İLETİŞİM|İLETISIM|ADRESİ|ADRESI|MAH|MAHALLE|MAHALLESİ|CAD|CADDE|CADDESİ|SOK|SOKAK|SOKAĞI|CEP|TELEFON|TELEFONU|SABİT|SABIT)\b",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Çoklu boşlukları tek boşluğa indir
        cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s+", " ").Trim();

        return cleanName;
    }
}
