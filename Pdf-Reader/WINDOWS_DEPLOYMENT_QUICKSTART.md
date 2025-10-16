# Windows Server - Hızlı Deployment Kılavuzu

Bu kılavuz, PDF Reader API'sini Windows Server'a deploy etmek için **3 adımlık** basit bir yol sunar.

## 🎯 Gereksinimler (Minimum)

- Windows Server 2022 (veya Windows 10/11)
- .NET 9.0 Runtime (**ZORUNLU**)
- Administrator yetkisi

## 📦 Adım 1: Projeyi Sunucuya Kopyalayın

Tüm proje klasörünü (Pdf-Reader/) Windows Server'a kopyalayın.

```
Örnek: C:\Deploy\Pdf-Reader\
```

## 🚀 Adım 2: Deployment Script'ini Çalıştırın

**Administrator olarak** Command Prompt veya PowerShell açın:

```cmd
cd C:\Deploy\Pdf-Reader
deploy-windows.bat
```

Script otomatik olarak şunları yapar:

1. ✅ Sistem kontrolü (.NET 9.0 varlığı)
2. ✅ Redis/Memurai kurulum kontrolü (opsiyonel)
3. ✅ IIS kurulum kontrolü ve kurulum (gerekirse)
4. ✅ Projeyi build ve publish eder
5. ✅ IIS Application Pool oluşturur
6. ✅ IIS Site oluşturur (Port: 5109)
7. ✅ Firewall kuralı ekler
8. ✅ İzinleri ayarlar

## 🧪 Adım 3: Test Edin

### 1. Health Check
```
http://localhost:5109/health
```

**Beklenen Yanıt:**
```json
{
  "status": "healthy",
  "timestamp": "2025-01-16T10:00:00Z",
  "version": "1.0.0",
  "service": "PDF Policy Extractor API"
}
```

### 2. Swagger UI
```
http://localhost:5109/swagger
```

### 3. Hangfire Dashboard
```
http://localhost:5109/hangfire
```

---

## 📝 Script Ne Yapar?

### Otomatik Yapılanlar:
- IIS kurulumu (yoksa)
- Application Pool oluşturma (No Managed Code)
- Site oluşturma (Port 5109)
- Dosya izinleri (IIS AppPool\PdfReaderAPI)
- Firewall kuralı (Port 5109)
- Logs klasörü oluşturma

### Manuel Yapmanız Gerekenler:
- ❌ YOK! Script her şeyi halleder.

---

## 🔴 Redis Gerekli mi?

### Development/Test: ❌ **Gerekli Değil**
- Script Redis yoksa in-memory storage kullanır
- Hangfire joblar bellekte saklanır
- Sunucu restart'ta job geçmişi kaybolur

### Production: ✅ **Şiddetle Önerilir**
- Script Memurai (Redis for Windows) kurar
- Job'lar Redis'te saklanır
- Sunucu restart'ta job geçmişi korunur
- Progress tracking çalışır

**Redis Kurulum:**
Script içinde "Y" seçin veya manuel olarak:
```
https://www.memurai.com/get-memurai
```

---

## 📂 Deployment Sonrası Dosya Yapısı

```
C:\inetpub\PdfReaderAPI\
├── Pdf-Reader.dll            (Ana uygulama)
├── appsettings.json          (Yapılandırma)
├── web.config                (IIS yapılandırması)
├── logs\                     (Application logları)
│   └── pdf-reader-20250116.txt
└── wwwroot\                  (Statik dosyalar)
```

---

## 🔧 Sorun Giderme

### Problem 1: "Proje build edilemedi"

**Çözüm:**
```cmd
dotnet --version
```
.NET 9.0 kurulu mu kontrol edin. Değilse:
```
https://dotnet.microsoft.com/download/dotnet/9.0
```

### Problem 2: "IIS bulunamadı"

**Çözüm:**
Script IIS kurmayı teklif edecektir. "Y" tuşuna basın.

### Problem 3: "Site çalışmıyor (HTTP 500)"

**Kontrol edin:**
```cmd
# Event Viewer
eventvwr

# Logs
type C:\inetpub\PdfReaderAPI\logs\pdf-reader-*.txt

# IIS durumu
inetmgr
```

### Problem 4: "Redis bağlantı hatası"

**Çözüm 1 (Önerilen):**
Memurai kurun ve başlatın:
```cmd
sc query Memurai
net start Memurai
```

**Çözüm 2 (Geçici):**
Redis olmadan çalıştır (in-memory mode). Script otomatik fallback yapar.

---

## 🌐 Dışarıdan Erişim (Network/Internet)

### Aynı Network'ten Erişim:

1. Windows Firewall'da port 5109'u açın (script otomatik yapar)
2. IIS Site Bindings'e IP adresi ekleyin:

```powershell
New-WebBinding -Name "PdfReaderAPI" -Protocol "http" -Port 5109 -IPAddress "*"
```

3. Client'tan test edin:
```
http://SERVER_IP:5109/health
```

### Internet'ten Erişim (Production):

1. Domain/SSL sertifikası edinin
2. IIS'te HTTPS binding ekleyin
3. Firewall'da 443 portunu açın
4. Authentication ekleyin (API Key, JWT, vb.)

---

## 📊 Performans Monitoring

### 1. Hangfire Dashboard
```
http://localhost:5109/hangfire
```
- Job queue durumu
- Başarılı/başarısız job'lar
- Worker count ve aktivite

### 2. IIS Manager
```cmd
inetmgr
```
- Application Pool durumu
- Worker process CPU/Memory
- Site durumu

### 3. Application Logs
```
C:\inetpub\PdfReaderAPI\logs\
```

---

## 🔄 Update/Yeniden Deploy

Yeni versiyon deploy etmek için:

```cmd
cd C:\Deploy\Pdf-Reader
deploy-windows.bat
```

Script:
1. Eski publish'i temizler
2. Yeni build yapar
3. Dosyaları günceller
4. IIS Application Pool'u restart eder

**Zero-downtime deployment için:**
Farklı Application Pool kullanın veya load balancer arkasına alın.

---

## 💡 Production Checklist

Deployment öncesi kontrol edin:

- [ ] .NET 9.0 Runtime kurulu
- [ ] Redis/Memurai kurulu ve çalışıyor
- [ ] Firewall kuralları ayarlandı
- [ ] SSL sertifikası yüklendi (HTTPS için)
- [ ] Authentication yapılandırıldı
- [ ] Backup stratejisi hazır
- [ ] Monitoring kuruldu
- [ ] Log rotation yapılandırıldı

---

## 📞 Yardım

### Log Konumları:
```
Application Logs:    C:\inetpub\PdfReaderAPI\logs\
IIS Logs:           C:\inetpub\logs\LogFiles\
Event Viewer:       Windows Logs > Application
```

### Servis Kontrolleri:
```cmd
# IIS durumu
sc query W3SVC

# Memurai (Redis) durumu
sc query Memurai

# IIS restart
iisreset
```

### Daha Fazla Bilgi:
- IIS Deployment Guide: `IIS_DEPLOYMENT_GUIDE.md`
- Worker Pool/Redis Guide: `WORKER_POOL_REDIS_GUIDE.md`
- API Documentation: http://localhost:5109/swagger

---

## 🎉 Tebrikler!

PDF Reader API başarıyla deploy edildi ve çalışıyor!

**Test endpoint'leri:**
- Health Check: `GET /health`
- Single PDF: `POST /api/policy/extract`
- Batch (Sync): `POST /api/policy/extract-batch`
- Batch (Async): `POST /api/policy/extract-batch-async`
- Job Status: `GET /api/policy/job/{jobId}`
