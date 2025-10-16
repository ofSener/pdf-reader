using Pdf_Reader.Models;

namespace Pdf_Reader.Services;

/// <summary>
/// Çıkarılan verileri doğrulayan servis
/// </summary>
public class ValidationService
{
    private readonly ILogger<ValidationService> _logger;

    public ValidationService(ILogger<ValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// PolicyData objesini doğrular ve hataları döndürür
    /// </summary>
    public ValidationResult Validate(PolicyData policyData)
    {
        var result = new ValidationResult();

        try
        {
            // 1. Zorunlu alan kontrolleri
            ValidateMandatoryFields(policyData, result);

            // 2. Tarih doğrulamaları
            ValidateDates(policyData, result);

            // 3. Para değerleri doğrulaması
            ValidateMoneyValues(policyData, result);

            // 4. TC Kimlik No doğrulaması
            ValidateTcNo(policyData, result);

            // 5. Plaka doğrulaması (araç poliçeleri için)
            ValidatePlateNumber(policyData, result);

            // 6. İsim doğrulaması
            ValidateName(policyData, result);

            // 7. Poliçe numarası doğrulaması
            ValidatePolicyNumber(policyData, result);

            // 8. Şirkete özel doğrulamalar
            ValidateCompanySpecific(policyData, result);

            // 9. Confidence bazlı uyarılar
            ValidateConfidence(policyData, result);

            result.IsValid = result.Errors.Count == 0;

            if (result.IsValid)
            {
                _logger.LogInformation($"Validation başarılı. Uyarı sayısı: {result.Warnings.Count}");
            }
            else
            {
                _logger.LogWarning($"Validation başarısız. Hata sayısı: {result.Errors.Count}, Uyarı sayısı: {result.Warnings.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation sırasında beklenmeyen hata");
            result.Errors.Add($"Validation hatası: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Zorunlu alanları kontrol eder
    /// </summary>
    private void ValidateMandatoryFields(PolicyData data, ValidationResult result)
    {
        if (data.Company == CompanyType.Unknown)
        {
            result.Errors.Add("Sigorta şirketi tespit edilemedi (zorunlu alan)");
        }

        if (string.IsNullOrWhiteSpace(data.PolicyNumber))
        {
            result.Errors.Add("Poliçe numarası bulunamadı (zorunlu alan)");
        }

        if (!data.StartDate.HasValue)
        {
            result.Errors.Add("Başlangıç tarihi bulunamadı (zorunlu alan)");
        }

        if (!data.EndDate.HasValue)
        {
            result.Errors.Add("Bitiş tarihi bulunamadı (zorunlu alan)");
        }

        // Prim kontrolü - Zeyil/Ek belgelerde 0 TL olabilir
        if (!data.GrossPremium.HasValue && !data.NetPremium.HasValue)
        {
            result.Errors.Add("Prim bilgisi bulunamadı (en az brüt veya net prim zorunlu)");
        }
        else if (data.GrossPremium.HasValue && data.NetPremium.HasValue &&
                 data.GrossPremium.Value == 0 && data.NetPremium.Value == 0)
        {
            // Her iki prim de 0 TL ise bu bir Zeyil/Ek belgesi olabilir - error yerine warning
            result.Warnings.Add("Prim bilgisi 0 TL (Zeyil/Ek belge olabilir)");
        }

        // Araç poliçeleri için plaka zorunlu
        if ((data.PolicyType == PolicyType.Trafik || data.PolicyType == PolicyType.Kasko) &&
            string.IsNullOrWhiteSpace(data.PlateNumber))
        {
            result.Errors.Add($"{data.PolicyType} poliçesi için plaka numarası zorunludur");
        }
    }

    /// <summary>
    /// Tarihleri doğrular
    /// </summary>
    private void ValidateDates(PolicyData data, ValidationResult result)
    {
        var now = DateTime.Now;

        // Başlangıç tarihi kontrolü
        if (data.StartDate.HasValue)
        {
            // Gelecekte çok ileri bir tarih olamaz (max 2 yıl)
            if (data.StartDate.Value > now.AddYears(2))
            {
                result.Warnings.Add($"Başlangıç tarihi çok ileri bir tarih: {data.StartDate.Value:dd/MM/yyyy}");
            }

            // Çok eski bir tarih olamaz (max 10 yıl önce)
            if (data.StartDate.Value < now.AddYears(-10))
            {
                result.Warnings.Add($"Başlangıç tarihi çok eski: {data.StartDate.Value:dd/MM/yyyy}");
            }
        }

        // Bitiş tarihi kontrolü
        if (data.EndDate.HasValue)
        {
            // Bitiş tarihi başlangıçtan önce olamaz
            if (data.StartDate.HasValue && data.EndDate.Value < data.StartDate.Value)
            {
                result.Errors.Add($"Bitiş tarihi ({data.EndDate.Value:dd/MM/yyyy}) başlangıç tarihinden ({data.StartDate.Value:dd/MM/yyyy}) önce olamaz");
            }

            // Poliçe süresi çok uzun olamaz (max 5 yıl)
            if (data.StartDate.HasValue)
            {
                var duration = (data.EndDate.Value - data.StartDate.Value).TotalDays;
                if (duration > 365 * 5)
                {
                    result.Warnings.Add($"Poliçe süresi çok uzun: {duration} gün");
                }
                else if (duration < 1)
                {
                    result.Errors.Add($"Geçersiz poliçe süresi: {duration} gün");
                }
                else
                {
                    // Süreyi hesapla
                    data.DurationDays = (int)duration;
                }
            }
        }

        // Tanzim tarihi kontrolü
        if (data.IssueDate.HasValue)
        {
            // Tanzim tarihi başlangıçtan önce olmalı veya aynı gün
            if (data.StartDate.HasValue && data.IssueDate.Value > data.StartDate.Value.AddDays(30))
            {
                result.Warnings.Add($"Tanzim tarihi ({data.IssueDate.Value:dd/MM/yyyy}) başlangıç tarihinden çok sonra");
            }

            // Tanzim tarihi gelecekte olamaz
            if (data.IssueDate.Value > now.AddDays(1))
            {
                result.Warnings.Add($"Tanzim tarihi gelecekte: {data.IssueDate.Value:dd/MM/yyyy}");
            }

            // Tanzim tarihi çok eski olamaz (max 10 yıl)
            if (data.IssueDate.Value < now.AddYears(-10))
            {
                result.Warnings.Add($"Tanzim tarihi çok eski: {data.IssueDate.Value:dd/MM/yyyy}");
            }
        }
    }

    /// <summary>
    /// Para değerlerini doğrular
    /// </summary>
    private void ValidateMoneyValues(PolicyData data, ValidationResult result)
    {
        // Not: İptal/Zeyil/Fesih belgelerinde negatif değerler kabul edilebilir
        // Ancak bu bilgi artık PolicyData'da tutulmadığı için negatif değerleri warning olarak işliyoruz

        // Net prim kontrolü
        if (data.NetPremium.HasValue)
        {
            if (data.NetPremium.Value < 0)
            {
                // Negatif değerler iptal/zeyil belgelerinde görülebilir
                result.Warnings.Add($"Net prim negatif (İptal/Zeyil belgesi olabilir): {data.NetPremium.Value:N2} TL");
            }
            else if (data.NetPremium.Value == 0)
            {
                result.Warnings.Add("Net prim 0 TL");
            }
            else if (data.NetPremium.Value > 1_000_000)
            {
                result.Warnings.Add($"Net prim çok yüksek: {data.NetPremium.Value:N2} TL");
            }
        }

        // Brüt prim kontrolü
        if (data.GrossPremium.HasValue)
        {
            if (data.GrossPremium.Value < 0)
            {
                // Negatif değerler iptal/zeyil belgelerinde görülebilir
                result.Warnings.Add($"Brüt prim negatif (İptal/Zeyil belgesi olabilir): {data.GrossPremium.Value:N2} TL");
            }
            else if (data.GrossPremium.Value == 0)
            {
                result.Warnings.Add("Brüt prim 0 TL");
            }
            else if (data.GrossPremium.Value > 1_000_000)
            {
                result.Warnings.Add($"Brüt prim çok yüksek: {data.GrossPremium.Value:N2} TL");
            }
        }

        // Brüt >= Net kontrolü
        if (data.GrossPremium.HasValue && data.NetPremium.HasValue)
        {
            if (data.GrossPremium.Value < data.NetPremium.Value)
            {
                // Bu durum iptal/zeyil belgelerinde görülebilir
                result.Warnings.Add($"Brüt prim ({data.GrossPremium.Value:N2}) net primden ({data.NetPremium.Value:N2}) küçük (İptal/Zeyil belgesi olabilir)");
            }

            // Fark çok fazla ise şüpheli (0'a bölme hatası önlemi)
            if (data.NetPremium.Value > 0)
            {
                var diff = data.GrossPremium.Value - data.NetPremium.Value;
                var diffPercent = (diff / data.NetPremium.Value) * 100;

                if (diffPercent > 50)
                {
                    result.Warnings.Add($"Brüt-Net prim farkı çok yüksek: %{diffPercent:N1}");
                }
            }
        }

        // Gider vergisi kontrolü
        if (data.GiderVergisi.HasValue)
        {
            if (data.GiderVergisi.Value < 0)
            {
                // Negatif değerler iptal/zeyil belgelerinde görülebilir
                result.Warnings.Add($"Gider vergisi negatif (İptal/Zeyil belgesi olabilir): {data.GiderVergisi.Value:N2} TL");
            }
        }
    }

    /// <summary>
    /// TC Kimlik No doğrular
    /// </summary>
    private void ValidateTcNo(PolicyData data, ValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(data.InsuredTcNo))
        {
            if (data.InsuredTcNo.Length != 11)
            {
                result.Errors.Add($"TC Kimlik No 11 haneli olmalı: {data.InsuredTcNo}");
            }
            else if (!data.InsuredTcNo.All(char.IsDigit))
            {
                result.Errors.Add($"TC Kimlik No sadece rakamlardan oluşmalı: {data.InsuredTcNo}");
            }
            else if (data.InsuredTcNo[0] == '0')
            {
                result.Errors.Add($"TC Kimlik No sıfır ile başlayamaz: {data.InsuredTcNo}");
            }
            // TcNoExtractor zaten algoritma ile validate ediyor, burada ekstra kontrol yapmaya gerek yok
        }
    }

    /// <summary>
    /// Plaka numarasını doğrular
    /// </summary>
    private void ValidatePlateNumber(PolicyData data, ValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(data.PlateNumber))
        {
            var plateClean = data.PlateNumber.Replace(" ", "");

            if (plateClean.Length < 6 || plateClean.Length > 8)
            {
                result.Warnings.Add($"Plaka formatı şüpheli: {data.PlateNumber}");
            }

            // İlk 2 karakter rakam olmalı (il kodu)
            if (plateClean.Length >= 2)
            {
                if (!char.IsDigit(plateClean[0]) || !char.IsDigit(plateClean[1]))
                {
                    result.Warnings.Add($"Plaka il kodu geçersiz: {data.PlateNumber}");
                }
                else
                {
                    int ilKodu = int.Parse(plateClean.Substring(0, 2));
                    if (ilKodu < 1 || ilKodu > 81)
                    {
                        result.Warnings.Add($"Geçersiz il kodu: {ilKodu}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// İsim doğrular
    /// </summary>
    private void ValidateName(PolicyData data, ValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(data.InsuredName))
        {
            var parts = data.InsuredName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                result.Warnings.Add($"İsim eksik görünüyor (sadece tek kelime): {data.InsuredName}");
            }

            if (data.InsuredName.Length < 5)
            {
                result.Warnings.Add($"İsim çok kısa: {data.InsuredName}");
            }

            if (data.InsuredName.Length > 100)
            {
                result.Warnings.Add($"İsim çok uzun: {data.InsuredName}");
            }
        }
    }

    /// <summary>
    /// Poliçe numarasını doğrular
    /// </summary>
    private void ValidatePolicyNumber(PolicyData data, ValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(data.PolicyNumber))
        {
            if (data.PolicyNumber.Length < 6)
            {
                result.Warnings.Add($"Poliçe numarası çok kısa: {data.PolicyNumber}");
            }

            if (data.PolicyNumber.Length > 20)
            {
                result.Warnings.Add($"Poliçe numarası çok uzun: {data.PolicyNumber}");
            }
        }
    }

    /// <summary>
    /// Şirkete özel validasyonlar
    /// </summary>
    private void ValidateCompanySpecific(PolicyData data, ValidationResult result)
    {
        // Şirkete özel kurallar buraya eklenebilir
        // Örnek: Mapfre poliçe numaraları belirli bir formatta olabilir

        switch (data.Company)
        {
            case CompanyType.Mapfre:
                // Mapfre özel kontroller
                break;

            case CompanyType.Doga:
                // Doğa özel kontroller
                break;

            case CompanyType.Unico:
                // Unico özel kontroller
                break;
        }
    }

    /// <summary>
    /// Confidence skorlarını kontrol eder
    /// </summary>
    private void ValidateConfidence(PolicyData data, ValidationResult result)
    {
        // Toplam confidence çok düşükse uyar
        if (data.ConfidenceScore < 0.5)
        {
            result.Warnings.Add($"Genel güven skoru çok düşük: {data.ConfidenceScore:P0}");
        }
        else if (data.ConfidenceScore < 0.7)
        {
            result.Warnings.Add($"Genel güven skoru düşük: {data.ConfidenceScore:P0}");
        }

        // Not: Alan bazlı confidence skorları artık response'da yer almadığı için
        // sadece toplam confidence skoru kontrol ediliyor
    }
}

/// <summary>
/// Validation sonucu
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public bool HasWarnings => Warnings.Count > 0;
    public bool HasErrors => Errors.Count > 0;
}
