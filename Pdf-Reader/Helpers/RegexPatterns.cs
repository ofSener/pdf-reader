using System.Text.RegularExpressions;

namespace Pdf_Reader.Helpers;

/// <summary>
/// Tüm regex pattern'leri merkezi bir yerde tutan helper class
/// </summary>
public static class RegexPatterns
{
    #region Tarih Patterns

    /// <summary>
    /// DD/MM/YYYY, DD.MM.YYYY, DD-MM-YYYY formatları
    /// </summary>
    public static readonly Regex DatePattern = new(
        @"\b(\d{1,2})[./\-](\d{1,2})[./\-](\d{4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Başlangıç tarihi pattern'leri
    /// </summary>
    public static readonly Regex StartDatePattern = new(
        @"(?:Ba[şs]lang[ıi][çc]|Start|Ba[şs]lama)[\s:]+[Tt]arih[i]?[\s:]*(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Bitiş tarihi pattern'leri
    /// </summary>
    public static readonly Regex EndDatePattern = new(
        @"(?:Bit[i]?[şs]|End|Sona)[\s:]+[Tt]arih[i]?[\s:]*(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Tanzim tarihi pattern'leri
    /// </summary>
    public static readonly Regex IssueDatePattern = new(
        @"(?:Tanzim|[İI]hda[çc]|[DD]üzenle[nm]|Issue)[\s:]+[Tt]arih[i]?[\s:]*(\d{1,2}[./\-]\d{1,2}[./\-]\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    #endregion

    #region Para/Prim Patterns

    /// <summary>
    /// Para miktarı: 1.234,56 veya 1234.56 formatları
    /// </summary>
    public static readonly Regex MoneyPattern = new(
        @"\b(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?)\b",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Net prim
    /// </summary>
    public static readonly Regex NetPremiumPattern = new(
        @"(?:Net\s+Prim|Net\s+Premium)[\s:]*([0-9.,]+)\s*(?:TL)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Brüt prim
    /// </summary>
    public static readonly Regex GrossPremiumPattern = new(
        @"(?:Br[üu]t\s+Prim|Br[üu]t\s+Premium|BRÜT\s+PR[İI]M)[\s:]*([0-9.,]+)\s*(?:TL)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Gider vergisi
    /// </summary>
    public static readonly Regex TaxPattern = new(
        @"(?:Gider\s+Vergisi)[\s:]*([0-9.,]+)\s*(?:TL)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    #endregion

    #region Poliçe Bilgileri

    /// <summary>
    /// Poliçe numarası
    /// </summary>
    public static readonly Regex PolicyNumberPattern = new(
        @"(?:Poli[çc]e\s+No|Policy\s+No|Poli[çc]e\s+Numara)[\s:./]*(\d{6,20})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Poliçe türü
    /// </summary>
    public static readonly Regex PolicyTypePattern = new(
        @"\b(Trafik|Kasko|DASK|Konut|Ferdi\s+Kaza|Seyahat|Sa[ğg]l[ıi]k|IMM|Ye[şs]il\s+Kart)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    #endregion

    #region Kişi Bilgileri

    /// <summary>
    /// TC Kimlik No (11 haneli)
    /// </summary>
    public static readonly Regex TcNoPattern = new(
        @"\b(\d{11})\b",
        RegexOptions.Compiled
    );

    /// <summary>
    /// İsim soyisim (Türkçe karakterlerle)
    /// </summary>
    public static readonly Regex NamePattern = new(
        @"(?:Ad[ıi]\s+Soyad[ıi]|[İI]sim|Sigortal[ıi])[\s:]+([A-ZÇĞİÖŞÜ][a-zçğıöşü]+(?:\s+[A-ZÇĞİÖŞÜ][a-zçğıöşü]+)+)",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Telefon numarası
    /// </summary>
    public static readonly Regex PhonePattern = new(
        @"\b(0?\d{3}[\s\-]?\d{3}[\s\-]?\d{2}[\s\-]?\d{2})\b",
        RegexOptions.Compiled
    );

    #endregion

    #region Araç Bilgileri

    /// <summary>
    /// Plaka numarası: 34ABC123, 06 AA 1234, 016MA0437 gibi formatlar (leading zero destekli)
    /// </summary>
    public static readonly Regex PlateNumberPattern = new(
        @"\b(0?\d{2}\s*[A-Z]{1,3}\s*\d{2,4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Araç markası
    /// </summary>
    public static readonly Regex VehicleBrandPattern = new(
        @"(?:Marka|Brand)[\s:]+([A-ZÇĞİÖŞÜa-zçğıöşü\-/]+(?:\s+\([A-Z]+\))?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Araç tipi/modeli
    /// </summary>
    public static readonly Regex VehicleModelPattern = new(
        @"(?:Tip|Model|Type)[\s:]+([A-Z0-9][A-Za-z0-9\s\.\-/]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Model yılı
    /// </summary>
    public static readonly Regex VehicleYearPattern = new(
        @"(?:Model|Y[ıi]l)[\s:]*(\d{4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Şasi numarası
    /// </summary>
    public static readonly Regex ChassisNumberPattern = new(
        @"(?:[ŞS]asi|Chassis|[ŞS]ase)[\s:]+No[\s:]*([A-Z0-9]{17})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Motor numarası
    /// </summary>
    public static readonly Regex EngineNumberPattern = new(
        @"(?:Motor|Engine)[\s:]+No[\s:]*([A-Z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Araç marka ve model birlikte
    /// </summary>
    public static readonly Regex VehicleInfoPattern = new(
        @"(?:Marka|Brand)[\s:]+([A-ZÇĞİÖŞÜa-zçğıöşü\-/]+)[\s\S]{0,100}?(?:Tip|Model|Type)[\s:]+([A-Z0-9][A-Za-z0-9\s\.\-/]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    #endregion

    #region Şirket Tespiti

    /// <summary>
    /// Sigorta şirketi isimleri
    /// </summary>
    public static readonly Dictionary<string, string> CompanyKeywords = new()
    {
        { "MAPFRE", "MAPFRE" },
        { "DO[ĞG]A", "Doğa" },
        { "UNICO", "Unico" },
        { "T[ÜU]RK\\s+NIPPON", "Türk Nippon" },
        { "ANADOLU", "Anadolu" },
        { "ALLIANZ", "Allianz" },
        { "AXA", "Axa" },
        { "SOMPO", "Sompo" },
        { "HEP[İI]YI", "Hepiyi" },
        { "NEOVA", "Neova" },
        { "QUICK", "Quick" },
        { "RAY\\s*S[İI]GORTA|RAYSIGORTA", "Ray" },
        { "KORU", "Koru" },
        { "ANKARA", "Ankara" },
        { "AK\\s+S[İI]GORTA", "Ak" },
        { "AKS[İI]GORTA", "Aksigorta" }
    };

    #endregion

    #region Helper Methods

    /// <summary>
    /// Keyword proximity search - anahtar kelimenin yakınındaki değeri bulur
    /// Örnek: "Net Prim: 12.345,67" -> "12.345,67"
    /// </summary>
    public static string? ExtractByKeyword(string text, string keyword, int maxDistance = 50)
    {
        var keywordIndex = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (keywordIndex == -1) return null;

        var searchStart = keywordIndex + keyword.Length;
        var searchEnd = Math.Min(searchStart + maxDistance, text.Length);
        var searchText = text.Substring(searchStart, searchEnd - searchStart);

        // İlk sayısal değeri bul
        var match = Regex.Match(searchText, @"[\d.,]+");
        return match.Success ? match.Value : null;
    }

    #endregion
}
