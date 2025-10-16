using Pdf_Reader.Models;
using Pdf_Reader.Services.Extractors;

namespace Pdf_Reader.Services;

/// <summary>
/// Tüm extraction işlemlerini koordine eden servis
/// </summary>
public class ExtractorOrchestrator
{
    private readonly ILogger<ExtractorOrchestrator> _logger;
    private readonly CompanyDetectorService _companyDetector;
    private readonly DateExtractor _dateExtractor;
    private readonly MoneyExtractor _moneyExtractor;
    private readonly PolicyNumberExtractor _policyNumberExtractor;
    private readonly PlateNumberExtractor _plateNumberExtractor;
    private readonly NameExtractor _nameExtractor;
    private readonly TcNoExtractor _tcNoExtractor;

    public ExtractorOrchestrator(
        ILogger<ExtractorOrchestrator> logger,
        CompanyDetectorService companyDetector,
        DateExtractor dateExtractor,
        MoneyExtractor moneyExtractor,
        PolicyNumberExtractor policyNumberExtractor,
        PlateNumberExtractor plateNumberExtractor,
        NameExtractor nameExtractor,
        TcNoExtractor tcNoExtractor)
    {
        _logger = logger;
        _companyDetector = companyDetector;
        _dateExtractor = dateExtractor;
        _moneyExtractor = moneyExtractor;
        _policyNumberExtractor = policyNumberExtractor;
        _plateNumberExtractor = plateNumberExtractor;
        _nameExtractor = nameExtractor;
        _tcNoExtractor = tcNoExtractor;
    }

    /// <summary>
    /// PDF metninden tüm poliçe bilgilerini çıkarır
    /// </summary>
    public async Task<PolicyData> ExtractPolicyDataAsync(string pdfText)
    {
        _logger.LogInformation("Poliçe verisi extraction başlıyor...");

        var policyData = new PolicyData();
        var fieldConfidence = new Dictionary<string, double>();
        var warnings = new List<string>();

        try
        {
            // 0. İptal/Zeyil/Fesih belgesi kontrolü (internal - response'a eklenmez)
            bool isCancellationDocument = IsCancellationDocument(pdfText);
            if (isCancellationDocument)
            {
                _logger.LogInformation("Belge iptal/zeyil/fesih belgesi olarak tespit edildi");
                warnings.Add("İptal/Zeyil/Fesih belgesi (negatif değerler kabul edilir)");
            }

            // 1. Şirket ve poliçe tipini tespit et
            var (companyType, companyName, companyConfidence) = _companyDetector.DetectCompany(pdfText);
            policyData.Company = companyType;
            policyData.CompanyName = companyName ?? companyType.ToString();
            fieldConfidence["Company"] = companyConfidence;

            if (companyType == CompanyType.Unknown)
            {
                warnings.Add("Sigorta şirketi tespit edilemedi");
                _logger.LogWarning("Sigorta şirketi tespit edilemedi");
            }

            var (policyType, policyTypeConfidence) = _companyDetector.DetectPolicyType(pdfText);
            policyData.PolicyType = policyType;
            fieldConfidence["PolicyType"] = policyTypeConfidence;

            if (policyType == PolicyType.Unknown)
            {
                warnings.Add("Poliçe tipi tespit edilemedi");
                _logger.LogWarning("Poliçe tipi tespit edilemedi");
            }

            // 2. Tarih bilgilerini çıkar
            var startDate = _dateExtractor.ExtractStartDate(pdfText, out double startDateConf);
            if (startDate.HasValue)
            {
                policyData.StartDate = startDate;
                fieldConfidence["StartDate"] = startDateConf;
            }
            else
            {
                warnings.Add("Başlangıç tarihi bulunamadı");
            }

            var endDate = _dateExtractor.ExtractEndDate(pdfText, out double endDateConf);
            if (endDate.HasValue)
            {
                policyData.EndDate = endDate;
                fieldConfidence["EndDate"] = endDateConf;
            }
            else
            {
                warnings.Add("Bitiş tarihi bulunamadı");
            }

            var issueDate = _dateExtractor.ExtractIssueDate(pdfText, out double issueDateConf);
            if (issueDate.HasValue)
            {
                policyData.IssueDate = issueDate;
                fieldConfidence["IssueDate"] = issueDateConf;
            }
            else
            {
                warnings.Add("Tanzim tarihi bulunamadı");
            }

            // 3. Para bilgilerini çıkar
            var netPremium = _moneyExtractor.ExtractNetPremium(pdfText, out double netPremConf);
            if (netPremium.HasValue)
            {
                policyData.NetPremium = netPremium;
                fieldConfidence["NetPremium"] = netPremConf;
            }
            else
            {
                warnings.Add("Net prim bulunamadı");
            }

            var grossPremium = _moneyExtractor.ExtractGrossPremium(pdfText, out double grossPremConf);
            if (grossPremium.HasValue)
            {
                policyData.GrossPremium = grossPremium;
                fieldConfidence["GrossPremium"] = grossPremConf;
            }
            else
            {
                warnings.Add("Brüt prim bulunamadı");
            }

            var tax = _moneyExtractor.ExtractTax(pdfText, out double taxConf);
            if (tax.HasValue)
            {
                policyData.GiderVergisi = tax;
                fieldConfidence["Tax"] = taxConf;
            }

            // 4. Poliçe numarasını çıkar
            var policyNumber = _policyNumberExtractor.Extract(pdfText, out double policyNumConf);
            if (!string.IsNullOrEmpty(policyNumber))
            {
                policyData.PolicyNumber = policyNumber;
                fieldConfidence["PolicyNumber"] = policyNumConf;
            }
            else
            {
                warnings.Add("Poliçe numarası bulunamadı");
            }

            // 5. Sigortalı bilgilerini çıkar
            var insuredName = _nameExtractor.Extract(pdfText, out double nameConf);
            if (!string.IsNullOrEmpty(insuredName))
            {
                policyData.InsuredName = insuredName;
                fieldConfidence["InsuredName"] = nameConf;
            }
            else
            {
                warnings.Add("Sigortalı adı bulunamadı");
            }

            var tcNo = _tcNoExtractor.Extract(pdfText, out double tcConf);
            if (!string.IsNullOrEmpty(tcNo))
            {
                policyData.InsuredTcNo = tcNo;
                fieldConfidence["TcNo"] = tcConf;
            }

            // 6. Araç bilgileri (Trafik, Kasko, Ferdi Kaza için)
            if (policyType == PolicyType.Trafik ||
                policyType == PolicyType.Kasko ||
                policyType == PolicyType.FerdiKaza)
            {
                var plateNumber = _plateNumberExtractor.Extract(pdfText, out double plateConf);
                if (!string.IsNullOrEmpty(plateNumber))
                {
                    policyData.PlateNumber = plateNumber;
                    fieldConfidence["PlateNumber"] = plateConf;
                }
                else
                {
                    warnings.Add($"{policyType} poliçesi için plaka numarası bulunamadı");
                }

                // Araç marka/model bilgisi (RegexPatterns'den)
                var vehicleInfo = ExtractVehicleInfo(pdfText);
                if (vehicleInfo.brand != null)
                {
                    policyData.VehicleBrand = vehicleInfo.brand;
                    fieldConfidence["VehicleBrand"] = vehicleInfo.confidence;
                }

                if (vehicleInfo.model != null)
                {
                    policyData.VehicleModel = vehicleInfo.model;
                    fieldConfidence["VehicleModel"] = vehicleInfo.confidence;
                }
            }

            // 7. Toplam confidence skorunu hesapla (FieldConfidence internal - response'a eklenmez)
            policyData.ConfidenceScore = CalculateOverallConfidence(fieldConfidence, policyType);

            _logger.LogInformation($"Extraction tamamlandı. Toplam confidence: {policyData.ConfidenceScore:P2}, Uyarı sayısı: {warnings.Count}");

            return await Task.FromResult(policyData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction sırasında hata oluştu");
            warnings.Add($"Extraction hatası: {ex.Message}");
            return policyData;
        }
    }

    /// <summary>
    /// Araç marka ve model bilgisini çıkarır
    /// </summary>
    private (string? brand, string? model, double confidence) ExtractVehicleInfo(string text)
    {
        try
        {
            var match = Helpers.RegexPatterns.VehicleInfoPattern.Match(text);
            if (match.Success && match.Groups.Count >= 3)
            {
                var brand = match.Groups[1].Value.Trim();
                var model = match.Groups[2].Value.Trim();

                // Basit validasyon
                if (!string.IsNullOrWhiteSpace(brand) && brand.Length >= 2 && brand.Length <= 30)
                {
                    return (brand, model, 0.8);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Araç bilgisi extraction hatası");
        }

        return (null, null, 0);
    }

    /// <summary>
    /// Toplam confidence skorunu hesaplar
    /// </summary>
    private double CalculateOverallConfidence(Dictionary<string, double> fieldConfidence, PolicyType policyType)
    {
        if (fieldConfidence.Count == 0)
            return 0;

        // Kritik alanlar (mutlaka olması gereken)
        var criticalFields = new[] { "Company", "PolicyNumber", "StartDate", "EndDate", "GrossPremium" };

        // Önemli alanlar
        var importantFields = new[] { "InsuredName", "NetPremium", "IssueDate" };

        // Araç poliçeleri için plaka kritik
        if (policyType == PolicyType.Trafik || policyType == PolicyType.Kasko)
        {
            criticalFields = criticalFields.Append("PlateNumber").ToArray();
        }

        double criticalScore = 0;
        int criticalCount = 0;
        foreach (var field in criticalFields)
        {
            if (fieldConfidence.TryGetValue(field, out double confidence))
            {
                criticalScore += confidence;
                criticalCount++;
            }
        }

        double importantScore = 0;
        int importantCount = 0;
        foreach (var field in importantFields)
        {
            if (fieldConfidence.TryGetValue(field, out double confidence))
            {
                importantScore += confidence;
                importantCount++;
            }
        }

        // Ağırlıklı ortalama: %70 kritik, %30 önemli
        double criticalAvg = criticalCount > 0 ? criticalScore / criticalCount : 0;
        double importantAvg = importantCount > 0 ? importantScore / importantCount : 0;

        double overallScore = (criticalAvg * 0.7) + (importantAvg * 0.3);

        // Kritik alanlardan biri eksikse ciddi ceza
        if (criticalCount < criticalFields.Length)
        {
            double missingRatio = (double)(criticalFields.Length - criticalCount) / criticalFields.Length;
            overallScore *= (1 - missingRatio * 0.5); // %50'ye kadar ceza
        }

        return Math.Round(overallScore, 2);
    }

    /// <summary>
    /// Belgenin iptal/zeyil/fesih belgesi olup olmadığını kontrol eder
    /// </summary>
    private bool IsCancellationDocument(string pdfText)
    {
        if (string.IsNullOrWhiteSpace(pdfText))
            return false;

        var normalizedText = pdfText.ToUpperInvariant();

        // İptal/Zeyil/Fesih keyword'lerini ara
        var cancellationKeywords = new[]
        {
            "İPTAL",
            "IPTAL",
            "ZEYİL",
            "ZEYIL",
            "FESİH",
            "FESIH",
            "PLAKA DEĞİŞİKLİĞİ",
            "PLAKA DEGISIKLIGI",
            "PLAKA ZEYLİ",
            "PLAKA ZEYLI",
            "ZEYİL PRİM BİLGİLERİ",
            "ZEYIL PRIM BILGILERI"
        };

        foreach (var keyword in cancellationKeywords)
        {
            if (normalizedText.Contains(keyword))
            {
                _logger.LogDebug($"Cancellation keyword bulundu: {keyword}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Batch extraction - birden fazla PDF için
    /// </summary>
    public async Task<List<(string fileName, PolicyData data)>> ExtractBatchAsync(
        Dictionary<string, string> pdfTexts)
    {
        _logger.LogInformation($"Batch extraction başlıyor: {pdfTexts.Count} dosya");

        var results = new List<(string fileName, PolicyData data)>();

        foreach (var kvp in pdfTexts)
        {
            try
            {
                var policyData = await ExtractPolicyDataAsync(kvp.Value);
                results.Add((kvp.Key, policyData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Dosya extraction hatası: {kvp.Key}");
                results.Add((kvp.Key, new PolicyData()));
            }
        }

        _logger.LogInformation($"Batch extraction tamamlandı: {results.Count} dosya işlendi");

        return results;
    }
}
