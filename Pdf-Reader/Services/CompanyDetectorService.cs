using Pdf_Reader.Models;
using Pdf_Reader.Helpers;

namespace Pdf_Reader.Services;

/// <summary>
/// Sigorta şirketi tespit servisi
/// </summary>
public class CompanyDetectorService
{
    private readonly ILogger<CompanyDetectorService> _logger;

    // Şirket isimleri ve anahtar kelimeler
    private readonly Dictionary<CompanyType, string[]> _companyKeywords = new()
    {
        { CompanyType.Acibadem, new[] { "ACİBADEM", "ACIBADEM", "ACİBADEM SİGORTA" } },
        { CompanyType.AcnTurk, new[] { "ACN TÜRK", "ACN TURK", "ACNTURK" } },
        { CompanyType.Ak, new[] { "AKSİGORTA", "AKSIGORTA", "AK SİGORTA", "AK ANONİM", "Aksigorta A.Ş", "Aksigorta Anonim" } },
        { CompanyType.Allianz, new[] { "ALLIANZ", "ALLİANZ", "ALLIANZ SİGORTA" } },
        { CompanyType.Ana, new[] { "ANA SİGORTA", "ANA SIGORTA", "ANADOLU ANONİM" } },
        { CompanyType.Anadolu, new[] { "ANADOLU ANONIM TÜRK SIGORTA ŞIRKETI", "ANADOLU ANONIM TURK SIGORTA SIRKETI", "ANADOLU ANONİM TÜRK SİGORTA ŞİRKETİ", "ANADOLU ANONİM TÜRK SİGORTA", "ANADOLU SİGORTA", "ANADOLU SIGORTA", "ANADOLU ANONİM" } },
        { CompanyType.Ankara, new[] { "ANKARA SİGORTA", "ANKARA SIGORTA", "ANKARA ANONİM" } },
        { CompanyType.Arex, new[] { "AREX", "AREX SİGORTA", "AREX SIGORTA" } },
        { CompanyType.Atlas, new[] { "ATLAS", "ATLAS SİGORTA", "ATLAS SIGORTA" } },
        { CompanyType.Axa, new[] { "AXA", "AXA SİGORTA", "AXA SIGORTA" } },
        { CompanyType.Bereket, new[] { "BEREKET", "BEREKET SİGORTA", "BEREKET SIGORTA" } },
        { CompanyType.Corpus, new[] { "CORPUS", "CORPUS SİGORTA", "CORPUS SIGORTA" } },
        { CompanyType.Doga, new[] { "DOĞA SİGORTA", "DOGA SIGORTA", "DOĞA", "DOGA" } },
        { CompanyType.Emaa, new[] { "EMAA", "EMAA SİGORTA", "EMAA SIGORTA" } },
        { CompanyType.Ethica, new[] { "ETHICA", "ETHICA SİGORTA", "ETHİCA" } },
        { CompanyType.Eureko, new[] { "EUREKO", "EUREKO SİGORTA", "EUREKO SIGORTA" } },
        { CompanyType.Gulf, new[] { "GULF", "GULF SİGORTA", "GULF SIGORTA" } },
        { CompanyType.HDI, new[] { "HDI", "HDI SİGORTA", "HDI SIGORTA" } },
        { CompanyType.Hepiyi, new[] { "HEPİYİ", "HEPIYI", "HEPİYİ SİGORTA" } },
        { CompanyType.Koru, new[] { "KORU", "KORU SİGORTA", "KORU SIGORTA" } },
        { CompanyType.Magdeburger, new[] { "MAGDEBURGER", "MAGDEBURGER SİGORTA" } },
        { CompanyType.Mapfre, new[] { "MAPFRE", "MAPFRE SİGORTA", "MAPFRE GENEL SİGORTA" } },
        { CompanyType.Neova, new[] { "NEOVA KATILIM SİGORTA", "NEOVA SİGORTA", "NEOVA SIGORTA", "NEOVA", "KATILIM SİGORTA POLİÇESİ" } },
        { CompanyType.Orient, new[] { "ORIENT", "ORIENT SİGORTA", "ORIENT SIGORTA" } },
        { CompanyType.Prive, new[] { "PRIVE", "PRİVE", "PRIVE SİGORTA" } },
        { CompanyType.Quick, new[] { "QUICK", "QUICK SİGORTA", "QUİCK" } },
        { CompanyType.Ray, new[] { "RAY SİGORTA", "RAY SIGORTA", "RAY ANONİM", "RAYSIGORTA" } },
        { CompanyType.Referans, new[] { "REFERANS SİGORTA", "REFERANS SIGORTA" } },
        { CompanyType.Seker, new[] { "ŞEKER", "SEKER", "ŞEKER SİGORTA" } },
        { CompanyType.Sompo, new[] { "SOMPO", "SOMPO SİGORTA", "SOMPO JAPAN" } },
        { CompanyType.TurkiyeKatilim, new[] { "TÜRKİYE KATILIM", "TURKIYE KATILIM", "KATILIM SİGORTA" } },
        { CompanyType.TurkiyePusula, new[] { "TÜRKİYE PUSULA", "TURKIYE PUSULA", "PUSULA SİGORTA", "TÜRKİYE SİGORTA A.Ş", "TÜRKİYE SİGORTA AŞ", "TURKIYE SIGORTA A.S", "TURKIYE SIGORTA", "TÜRKİYE SİGORTA" } },
        { CompanyType.TurkNippon, new[] { "TÜRK NİPPON SİGORTA", "TURK NIPPON SIGORTA", "TURKNIPPON" } },
        { CompanyType.Unico, new[] { "UNICO SİGORTA A", "UNİCO SİGORTA A", "UNICO SIGORTA", "UNICO", "UNİCO" } },
        { CompanyType.Zurich, new[] { "ZURICH", "ZURİCH", "ZURICH SİGORTA" } }
    };

    public CompanyDetectorService(ILogger<CompanyDetectorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// PDF metninden şirket tipini tespit eder
    /// </summary>
    public (CompanyType type, string? name, double confidence) DetectCompany(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (CompanyType.Unknown, null, 0);
        }

        // Metni normalize et (büyük harfe çevir, Türkçe karakterleri koru)
        var normalizedText = NormalizeText(text);

        var detections = new List<(CompanyType type, int score, int position)>();

        foreach (var kvp in _companyKeywords)
        {
            var companyType = kvp.Key;
            var keywords = kvp.Value;

            foreach (var keyword in keywords)
            {
                var normalizedKeyword = NormalizeText(keyword);
                var index = normalizedText.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase);

                if (index >= 0)
                {
                    // Skorlama: Kelimenin uzunluğu ve pozisyonu önemli
                    // İlk sayfalarda geçenler daha güvenilir
                    int score = keyword.Length;

                    // İlk 500 karakterde geçiyorsa bonus puan
                    if (index < 500)
                        score += 10;

                    // İlk 1000 karakterde geçiyorsa orta bonus
                    else if (index < 1000)
                        score += 5;

                    // Footer/header bölgesindeki tekrar eden şirket adları için bonus
                    // (genellikle her sayfada tekrarlanır)
                    int occurrenceCount = normalizedText.Split(new[] { normalizedKeyword }, StringSplitOptions.None).Length - 1;
                    if (occurrenceCount > 3)
                        score += 15; // Çok tekrar ediyorsa büyük bonus

                    detections.Add((companyType, score, index));

                    _logger.LogDebug($"Şirket anahtar kelimesi bulundu: {companyType} - '{keyword}' at position {index}, occurrences: {occurrenceCount}");
                }
            }
        }

        if (detections.Count == 0)
        {
            _logger.LogWarning("Hiçbir şirket anahtar kelimesi bulunamadı");
            return (CompanyType.Unknown, null, 0);
        }

        // En yüksek skorlu şirketi seç
        var bestDetection = detections.OrderByDescending(d => d.score).ThenBy(d => d.position).First();

        // Confidence hesapla
        double confidence = CalculateConfidence(bestDetection.score, detections.Count);

        // Şirket ismini orijinal metinden çıkar
        string? companyName = ExtractCompanyName(text, bestDetection.type);

        _logger.LogInformation($"Şirket tespit edildi: {bestDetection.type} (Confidence: {confidence:P2})");

        return (bestDetection.type, companyName, confidence);
    }

    /// <summary>
    /// Metni normalize eder (Türkçe karakter desteği ile)
    /// </summary>
    private string NormalizeText(string text)
    {
        // Türkçe karakterleri koru, sadece büyük harfe çevir
        return text.ToUpperInvariant();
    }

    /// <summary>
    /// Confidence skorunu hesaplar
    /// </summary>
    private double CalculateConfidence(int bestScore, int totalDetections)
    {
        // Base confidence: skor/20 (max kelime uzunluğu ~20)
        double baseConfidence = Math.Min(bestScore / 20.0, 0.8);

        // Bonus: Birden fazla anahtar kelime bulunduysa
        if (totalDetections > 1)
            baseConfidence += 0.1;

        // Bonus: İlk 500 karakterde bulunduysa (score'da zaten var ama ek bonus)
        if (bestScore > 15)
            baseConfidence += 0.1;

        return Math.Min(baseConfidence, 1.0);
    }

    /// <summary>
    /// Orijinal metinden şirket ismini çıkarır
    /// </summary>
    private string? ExtractCompanyName(string text, CompanyType companyType)
    {
        if (!_companyKeywords.TryGetValue(companyType, out var keywords))
            return null;

        // En uzun anahtar kelimeyi kullan (daha spesifik)
        var longestKeyword = keywords.OrderByDescending(k => k.Length).First();

        // Metinde ara
        var index = text.IndexOf(longestKeyword, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            // Anahtar kelimenin etrafındaki metni al (şirket adının tam halini bulmak için)
            var startIndex = Math.Max(0, index - 20);
            var endIndex = Math.Min(text.Length, index + longestKeyword.Length + 30);
            var context = text.Substring(startIndex, endIndex - startIndex);

            // Şirket adı genellikle büyük harflerle ve tam olarak yazılır
            // Basit bir yaklaşım: anahtar kelimenin kendisini döndür
            // (Gelişmiş: regex ile tam şirket adını parse edebiliriz)

            return longestKeyword;
        }

        return companyType.ToString();
    }

    /// <summary>
    /// Poliçe tipini tespit eder
    /// </summary>
    public (PolicyType type, double confidence) DetectPolicyType(string text)
    {
        var normalizedText = NormalizeText(text);

        var policyTypeKeywords = new Dictionary<PolicyType, string[]>
        {
            { PolicyType.Trafik, new[] { "TRAFİK SİGORTASI", "ZORUNLU TRAFİK", "KARAYOLLARI MOTORLU", "TRAFİK POLİÇESİ", "TRAFIK" } },
            { PolicyType.Kasko, new[] { "KASKO", "MOTORLİ ARAÇLAR KASKO", "TAM KASKO", "KASKO SİGORTASI" } },
            { PolicyType.Dask, new[] { "DASK", "ZORUNLU DEPREM", "DEPREM SİGORTASI" } },
            { PolicyType.Konut, new[] { "KONUT SİGORTASI", "EV SİGORTASI", "KONUT POLİÇESİ" } },
            { PolicyType.Hayat, new[] { "YILLIK HAYAT SİGORTASI", "HAYAT SİGORTASI SERTİFİKASI", "HAYAT SİGORTASI", "HAYAT POLİÇESİ" } },
            { PolicyType.Saglik, new[] { "SAĞLIK SİGORTASI", "TAMAMLAYICI SAĞLIK", "ÖZEL SAĞLIK" } },
            { PolicyType.Isyeri, new[] { "İŞYERİ SİGORTASI", "TİCARİ SİGORTA", "İŞYERİ POLİÇESİ" } },
            { PolicyType.FerdiKaza, new[] { "FERDİ KAZA", "KİŞİSEL KAZA", "FERDİ KAZA SİGORTASI" } },
            { PolicyType.Seyahat, new[] { "SEYAHAT SİGORTASI", "YURT DIŞI SAĞLIK", "SEYAHAT POLİÇESİ" } },
            { PolicyType.YabanciSaglik, new[] { "YABANCI SAĞLIK", "YURT DIŞI SAĞLIK", "YAB. SAĞLIK" } },
            { PolicyType.Nakliyat, new[] { "NAKLİYAT SİGORTASI", "EMTİA NAKLİYAT", "TAŞIMACILIK SİGORTASI" } },
            { PolicyType.Yangin, new[] { "YANGIN SİGORTASI", "YANGIN POLİÇESİ", "YANGIN VE HIRSIZLIK" } },
            { PolicyType.Muhendislik, new[] { "MÜHENDİSLİK SİGORTASI", "İNŞAAT SİGORTASI", "MAKİNA KIRILMASI" } },
            { PolicyType.Sorumluluk, new[] { "SORUMLULUK SİGORTASI", "ÜÇÜNCÜ ŞAHIS SORUMLULUK", "MALİ SORUMLULUK" } },
            { PolicyType.Tarım, new[] { "TARIM SİGORTASI", "TARIMSAL ÜRÜN", "HAYVAN SİGORTASI" } }
        };

        var detections = new List<(PolicyType type, int score)>();

        foreach (var kvp in policyTypeKeywords)
        {
            var policyType = kvp.Key;
            var keywords = kvp.Value;

            foreach (var keyword in keywords)
            {
                if (normalizedText.Contains(NormalizeText(keyword)))
                {
                    // Daha uzun ve spesifik keywords daha yüksek skor alır
                    int score = keyword.Length;
                    detections.Add((policyType, score));

                    _logger.LogDebug($"Poliçe tipi anahtar kelimesi bulundu: {policyType} - '{keyword}'");
                }
            }
        }

        if (detections.Count == 0)
        {
            _logger.LogWarning("Hiçbir poliçe tipi anahtar kelimesi bulunamadı");
            return (PolicyType.Unknown, 0);
        }

        // En yüksek skorlu poliçe tipini seç
        var bestDetection = detections.OrderByDescending(d => d.score).First();

        // Confidence: birden fazla anahtar kelime bulunduysa daha yüksek
        var matchCount = detections.Count(d => d.type == bestDetection.type);
        double confidence = Math.Min(0.7 + (matchCount * 0.1), 0.95);

        _logger.LogInformation($"Poliçe tipi tespit edildi: {bestDetection.type} (Confidence: {confidence:P2})");

        return (bestDetection.type, confidence);
    }
}
