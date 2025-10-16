namespace Pdf_Reader.Models;

/// <summary>
/// PDF'den çıkarılan poliçe verisi
/// </summary>
public class PolicyData
{
    // Poliçe Bilgileri
    public string? PolicyNumber { get; set; }
    public CompanyType Company { get; set; } = CompanyType.Unknown;
    public string? CompanyName { get; set; }
    public string? PolicyType { get; set; } // String olarak (örn: "Dask", "Trafik")

    // Tarihler
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? IssueDate { get; set; } // Tanzim tarihi
    public int? DurationDays { get; set; }

    // Prim Bilgileri
    public decimal? NetPremium { get; set; }
    public decimal? GrossPremium { get; set; }

    // Sigortalı Bilgileri
    public string? InsuredName { get; set; }
    public string? InsuredTcNo { get; set; }
    public string? InsuredPhone { get; set; }
    public string? InsuredAddress { get; set; }

    // Araç Bilgileri (Trafik/Kasko için)
    public string? PlateNumber { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }

    // Güven Skoru
    public double ConfidenceScore { get; set; } // 0.0 - 1.0 arası
}
