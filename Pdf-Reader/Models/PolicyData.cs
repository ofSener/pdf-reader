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
    public PolicyType PolicyType { get; set; } = PolicyType.Unknown;
    public string? PolicyTypeText { get; set; }

    // Tarihler
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? IssueDate { get; set; } // Tanzim tarihi
    public int? DurationDays { get; set; }

    // Prim Bilgileri
    public decimal? NetPremium { get; set; }
    public decimal? GrossPremium { get; set; }
    public decimal? GiderVergisi { get; set; }

    // Sigortalı Bilgileri
    public string? InsuredName { get; set; }
    public string? InsuredTcNo { get; set; }
    public string? InsuredPhone { get; set; }
    public string? InsuredAddress { get; set; }

    // Araç Bilgileri (Trafik/Kasko/Ferdi Kaza için)
    public string? PlateNumber { get; set; }
    public string? VehicleBrand { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleType { get; set; }
    public int? VehicleYear { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }

    // Meta Bilgiler
    public double ConfidenceScore { get; set; } // 0.0 - 1.0 arası
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, double> FieldConfidence { get; set; } = new(); // Her alan için ayrı güven skoru
    public string? ExtractionMethod { get; set; } // Hangi yöntemle çıkarıldı
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    // Özel Durum Flag'leri
    public bool IsCancellationDocument { get; set; } = false; // İptal/Zeyil/Fesih belgesi mi?
}
