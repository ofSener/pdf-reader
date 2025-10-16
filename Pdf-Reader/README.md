# PDF Reader - Türk Sigorta Poliçesi Okuma API

.NET 9.0 tabanlı, Türk sigorta şirketlerinin PDF text'lerinden otomatik veri çıkarma API'si.

## ⚠️ ÖNEMLİ DEĞİŞİKLİK

**Bu API artık PDF dosyası kabul etmemektedir.** PDF'lerinizi text'e çevirip JSON olarak göndermeniz gerekmektedir.

## Özellikler

- **Text-Based Processing**: PDF text'i JSON ile gönderilir (PDF upload yok)
- **Çoklu Şirket Desteği**: 21+ Türk sigorta şirketi
- **Çoklu Poliçe Tipi**: Trafik, Kasko, DASK, Konut, TSS, Ferdi Kaza, vb.
- **Yüksek Doğruluk**: %97+ başarı oranı
- **Otomatik Validation**: Çıkarılan verilerin otomatik doğrulanması
- **RESTful API**: Kolay entegrasyon
- **Swagger Dokümantasyonu**: Otomatik API dokümantasyonu
- **⚡ Hızlı İşleme**: PDF parsing adımı atlandığı için daha hızlı
- **📦 Düşük Bandwidth**: Text << PDF binary

## Desteklenen Sigorta Şirketleri

| Şirket | Trafik | Kasko | DASK | Konut | Diğer |
|--------|--------|-------|------|-------|-------|
| Anadolu | ✓ | ✓ | ✓ | ✓ | ✓ |
| Allianz | ✓ | ✓ | ✓ | ✓ | ✓ |
| Doğa | ✓ | ✓ | ✓ | ✓ | ✓ |
| Ankara | ✓ | ✓ | ✓ | ✓ | ✓ |
| Neova | ✓ | ✓ | ✓ | ✓ | ✓ |
| Hepiyi | ✓ | ✓ | - | - | ✓ |
| Quick | ✓ | - | - | - | ✓ |
| Ray | ✓ | ✓ | ✓ | ✓ | - |
| Sompo | ✓ | ✓ | ✓ | ✓ | ✓ |
| Zurich | ✓ | ✓ | - | - | - |
| Mapfre | ✓ | - | - | - | - |
| Unico | ✓ | - | - | - | ✓ |
| Axa | ✓ | ✓ | - | - | - |
| HDI | ✓ | - | - | - | - |
| +7 diğer | ✓ | - | - | - | - |

## Çıkarılan Bilgiler

### Temel Bilgiler
- Sigorta Şirketi
- Poliçe Numarası
- Poliçe Tipi
- Başlangıç/Bitiş Tarihleri
- Tanzim Tarihi

### Mali Bilgiler
- Net Prim
- Brüt Prim
- Gider Vergisi
- Toplam Tutar

### Sigortalı Bilgileri
- Ad Soyad
- TC Kimlik No
- İletişim Bilgileri

### Araç Bilgileri (Trafik/Kasko için)
- Plaka
- Marka/Model
- Şasi/Motor Numarası

## Kurulum

### Gereksinimler
- .NET 9.0 SDK
- iTextSharp (PDF okuma)
- Serilog (logging)

### Adımlar

1. Projeyi klonlayın:
```bash
git clone <repository-url>
cd Pdf-Reader
```

2. Bağımlılıkları yükleyin:
```bash
dotnet restore
```

3. Projeyi derleyin:
```bash
dotnet build
```

4. Uygulamayı çalıştırın:
```bash
dotnet run
```

API varsayılan olarak `http://localhost:5000` adresinde çalışacaktır.

## Kullanım

### Ana Endpoint

#### Text-Based Extraction (Tek Endpoint)
```http
POST /api/Policy/extract-from-text
Content-Type: application/json

{
  "pdfText": "ANADOLU SİGORTA\nPoliçe No: 1234567890\n...",
  "fileName": "policy_001.pdf"  // opsiyonel
}
```

**Yanıt:**
```json
{
  "success": true,
  "data": {
    "company": "Anadolu Sigorta",
    "companyType": 6,
    "policyNumber": "1234567890",
    "policyType": "Trafik",
    "startDate": "2024-01-01T00:00:00",
    "endDate": "2025-01-01T00:00:00",
    "netPremium": 1250.50,
    "grossPremium": 1400.00,
    "insuredName": "AHMET YILMAZ",
    "insuredTcNo": "12345678901",
    "plateNumber": "34ABC123",
    "confidenceScore": 0.95
  },
  "errors": [],
  "warnings": [],
  "processingTimeMs": 320
}
```

**Not:** PDF dosyası upload etmek mümkün değildir. PDF'lerinizi önce text'e çevirip bu endpoint'e gönderin.

#### 2. Sağlık Kontrolü
```http
GET /api/Policy/health
```

### Swagger UI

API dokümantasyonuna şu adresten erişebilirsiniz:
```
http://localhost:5000/swagger
```

## Mimari

```
Pdf-Reader/
├── Controllers/           # API Controllers
│   └── PolicyController.cs
├── Models/               # Data Models
│   ├── PolicyData.cs
│   ├── CompanyType.cs
│   └── PolicyType.cs
├── Services/             # İş Mantığı
│   ├── PdfTextExtractorService.cs
│   ├── ExtractorOrchestrator.cs
│   ├── CompanyDetectorService.cs
│   ├── ValidationService.cs
│   └── Extractors/       # Alan Çıkarıcılar
│       ├── DateExtractor.cs
│       ├── MoneyExtractor.cs
│       ├── PolicyNumberExtractor.cs
│       ├── PlateNumberExtractor.cs
│       ├── NameExtractor.cs
│       └── TcNoExtractor.cs
├── Helpers/
│   └── RegexPatterns.cs  # Regex Şablonları
└── Program.cs
```

## Validation

Sistem otomatik olarak şu kontrolleri yapar:

- **Zorunlu Alan Kontrolleri**: Şirket, poliçe no, tarihler, primler
- **Tarih Validasyonu**: Mantıksal tarih kontrolü, süre hesaplama
- **Para Validasyonu**: Negatif değerler, brüt >= net kontrolü
- **TC No Validasyonu**: 11 hane, algoritma kontrolü
- **Plaka Validasyonu**: Format ve il kodu kontrolü
- **İptal/Zeyil Belgesi Tespiti**: Özel validasyon kuralları

## Performans

- **İşlem Süresi**: ~500ms/PDF (ortalama)
- **Başarı Oranı**: %97.2 (446/459 PDF)
- **Bellek Kullanımı**: <100MB (normal operasyon)
- **Eşzamanlı İstek**: 10+ concurrent request

## Hata Yönetimi

API her zaman JSON response döner:

```json
{
  "success": false,
  "data": null,
  "errors": [
    "Sigorta şirketi tespit edilemedi",
    "Başlangıç tarihi bulunamadı"
  ],
  "warnings": [
    "Confidence skoru düşük: 65%"
  ]
}
```

## Loglama

Serilog kullanılarak detaylı loglama:

- **Information**: Normal operasyonlar
- **Warning**: Düşük confidence, eksik alanlar
- **Error**: İşlem hataları
- **Debug**: Detaylı extraction bilgileri

## Güvenlik

- **File Upload Validation**: Sadece PDF dosyaları
- **File Size Limit**: Maksimum 10MB
- **Input Sanitization**: Tüm input'lar temizlenir
- **Error Handling**: Güvenli hata mesajları

## Windows Server Deployment

### Seçenek 1: Tam Otomatik Kurulum (Yeni Deployment)

Yeni bir deployment için (IIS, Application Pool, Site oluşturur):

```cmd
cd C:\Deploy\Pdf-Reader
deploy-windows.bat
```

**Ne yapar?**
- IIS kurulumu (yoksa)
- Redis/Memurai kurulumu (opsiyonel)
- Application Pool oluşturma
- Site oluşturma (Port 5109)
- Firewall kuralı ekleme

**Detaylı bilgi**: `WINDOWS_DEPLOYMENT_QUICKSTART.md`

### Seçenek 2: Basit Güncelleme (Mevcut Deployment)

Mevcut deployment'ınızı güncellemek için (port, site değişmez):

```cmd
cd C:\Deploy\Pdf-Reader
update-existing-deployment.bat
```

**Ne yapar?**
- Projeyi build eder
- Dosyaları mevcut klasöre kopyalar
- Application Pool'u restart eder
- **Port, Site, IIS ayarları değişmez**

**Önerilen**: Mevcut bir setup'ınız varsa (443 portunda çalışıyorsa) bu seçeneği kullanın.

### Port ve Güvenlik

**Production için Port 443 (HTTPS) önerilir:**
- ✅ SSL/TLS şifreli iletişim
- ✅ Standart web portu
- ✅ Firewall friendly

**Port 5109 (HTTP) sadece development/internal için:**
- ⚠️ Şifrelenmemiş
- ⚠️ Production'da önerilmez

**Mevcut setup'ınız varsa** (443 portunda), `update-existing-deployment.bat` kullanın.

## Lisans

Bu proje özel kullanım içindir.

## İletişim

Sorularınız için: [iletişim bilgisi]
