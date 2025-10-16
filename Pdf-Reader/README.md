# PDF Reader - Türk Sigorta Poliçesi Okuma API

.NET 9.0 tabanlı, Türk sigorta şirketlerinin PDF poliçelerinden otomatik veri çıkarma API'si.

## Özellikler

- **Çoklu Şirket Desteği**: 21+ Türk sigorta şirketi
- **Çoklu Poliçe Tipi**: Trafik, Kasko, DASK, Konut, TSS, Ferdi Kaza, vb.
- **Yüksek Doğruluk**: %97+ başarı oranı
- **Otomatik Validation**: Çıkarılan verilerin otomatik doğrulanması
- **RESTful API**: Kolay entegrasyon
- **Swagger Dokümantasyonu**: Otomatik API dokümantasyonu
- **🔥 Background Job Processing**: Hangfire + Redis ile asenkron batch işleme
- **📊 Real-time Dashboard**: Hangfire dashboard ile job monitoring
- **⚡ Paralel İşleme**: CPU core sayısına göre otomatik paralel processing
- **🔄 Progress Tracking**: Batch işlemlerde gerçek zamanlı ilerleme takibi

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

### API Endpoints

#### 1. Tek PDF Çıkarma (Senkron)
```http
POST /api/policy/extract
Content-Type: multipart/form-data

file: <pdf-file>
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
  "warnings": []
}
```

#### 2. Batch İşlem - Senkron (Client Bekler)
```http
POST /api/policy/extract-batch
Content-Type: multipart/form-data

files: <multiple-pdf-files>
```

#### 3. 🔥 Batch İşlem - Asenkron (Background Job)
```http
POST /api/policy/extract-batch-async
Content-Type: multipart/form-data

files: <multiple-pdf-files>
```

**Hemen Dönen Yanıt:**
```json
{
  "jobId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "queued",
  "totalFiles": 100,
  "message": "İşlem kuyruğa eklendi. Sonuçları GET /api/policy/job/{jobId} endpoint'inden takip edebilirsiniz.",
  "estimatedCompletionTime": "2025-01-16T10:05:00Z",
  "createdAt": "2025-01-16T10:00:00Z"
}
```

#### 4. 📊 Job Status Sorgulama
```http
GET /api/policy/job/{jobId}
```

**Yanıt (Processing):**
```json
{
  "jobId": "a1b2c3d4-...",
  "status": "processing",
  "progress": {
    "totalFiles": 100,
    "processedFiles": 45,
    "currentFile": "policy_045.pdf",
    "percentageComplete": 45
  }
}
```

**Yanıt (Completed):**
```json
{
  "jobId": "a1b2c3d4-...",
  "status": "completed",
  "result": {
    "totalFiles": 100,
    "successCount": 98,
    "failureCount": 2,
    "results": [...]
  }
}
```

#### 5. Sağlık Kontrolü
```http
GET /health
```

#### 6. 📊 Hangfire Dashboard
```
http://your-server:5109/hangfire
```
- Job queue monitoring
- Başarılı/başarısız job'lar
- Retry mekanizması
- Real-time statistics

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
