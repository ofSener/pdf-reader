# Worker Pool, Redis ve Load Balancing Kılavuzu

Bu dokümantasyon, PDF Reader API'sinin production ortamında yüksek yük altında çalışabilmesi için **Worker Pool**, **Redis Queue** ve **Load Balancing** yapılandırmasını açıklar.

## 📊 Mevcut Durum vs İdeal Mimari

### Şu Anki Mimari (Mevcut Kod)

```
Client → HTTP Request → API Controller → Task.WhenAll (Paralel)
                                              ↓
                                    HTTP Response (blocking)
```

**Sorunlar:**
- ❌ Client 10 dakika boyunca beklemek zorunda (timeout)
- ❌ Sunucu restart olursa işlemler kaybolur
- ❌ Load balancing yapılamaz
- ❌ Progress tracking yok
- ❌ Retry mekanizması yok

### İdeal Mimari (Önerilen)

```
Client → HTTP Request → Job ID döndür (hemen)
              ↓
         Redis Queue
              ↓
    Background Worker Pool
              ↓
    Results → Redis Cache
              ↓
Client → GET /api/job/{id} → Result
```

**Avantajlar:**
- ✅ Client hemen response alır
- ✅ İşlemler background'da devam eder
- ✅ Sunucu restart olsa bile işlemler kaybolmaz
- ✅ Progress tracking yapılabilir
- ✅ Retry mekanizması eklenebilir
- ✅ Horizontal scaling yapılabilir (multiple workers)

---

## 🎯 İmplementasyon Seçenekleri

### Seçenek 1: Hangfire + Redis (ÖNERİLEN)

**Kullanım Senaryosu:** Production, yüksek yük, enterprise çözüm

**Avantajlar:**
- ✅ Ready-to-use dashboard
- ✅ Retry mekanizması built-in
- ✅ Recurring jobs desteği
- ✅ Redis persistence
- ✅ Distributed locking

**Kurulum:**

#### 1. NuGet Paketleri

```bash
dotnet add package Hangfire.Core --version 1.8.14
dotnet add package Hangfire.AspNetCore --version 1.8.14
dotnet add package Hangfire.Redis.StackExchange --version 1.10.2
dotnet add package StackExchange.Redis --version 2.7.33
```

#### 2. appsettings.json Güncellemesi

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false"
  },
  "Hangfire": {
    "WorkerCount": 8,
    "PollingInterval": "00:00:01"
  }
}
```

#### 3. Program.cs Güncellemesi

```csharp
using Hangfire;
using Hangfire.Redis.StackExchange;

// Redis connection
var redisConnection = ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

// Hangfire configuration
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseRedisStorage(redisConnection, new RedisStorageOptions
    {
        Prefix = "hangfire:pdfreader:",
        InvisibilityTimeout = TimeSpan.FromMinutes(30)
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 8; // CPU core sayısına göre ayarlayın
    options.SchedulePollingInterval = TimeSpan.FromSeconds(1);
    options.ServerName = Environment.MachineName;
});

// Batch processing job servis
builder.Services.AddScoped<BatchProcessingJobService>();

var app = builder.Build();

// Hangfire Dashboard (production'da authentication ekleyin!)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.Run();

// Simple authorization filter (production'da gerçek auth kullanın)
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // TODO: Production'da gerçek authentication ekleyin
        var httpContext = context.GetHttpContext();
        return httpContext.Request.IsLocal(); // Sadece localhost'tan erişim
    }
}
```

#### 4. Yeni Controller Endpoint'leri

**PolicyController.cs** içine ekleyin:

```csharp
using Hangfire;
using StackExchange.Redis;

private readonly IBackgroundJobClient _backgroundJobs;
private readonly IConnectionMultiplexer _redis;

public PolicyController(
    // ... mevcut parametreler
    IBackgroundJobClient backgroundJobs,
    IConnectionMultiplexer redis)
{
    // ... mevcut kod
    _backgroundJobs = backgroundJobs;
    _redis = redis;
}

/// <summary>
/// Batch işlemi için job oluşturur (asenkron - hemen döner)
/// </summary>
[HttpPost("extract-batch-async")]
[Consumes("multipart/form-data")]
public async Task<ActionResult<BatchJobResponse>> ExtractPolicyBatchAsync([FromForm] List<IFormFile> files)
{
    try
    {
        if (files == null || files.Count == 0)
            return BadRequest("En az bir dosya yüklenmelidir");

        if (files.Count > 100)
            return BadRequest("Maksimum 100 dosya aynı anda işlenebilir");

        // Job ID oluştur
        var jobId = Guid.NewGuid().ToString();

        // Dosyaları byte array'e çevir (Redis'e kaydedilecek)
        var fileData = new List<(string fileName, byte[] fileBytes)>();
        foreach (var file in files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            fileData.Add((file.FileName, ms.ToArray()));
        }

        // Hangfire job'ı kuyruğa ekle
        var hangfireJobId = _backgroundJobs.Enqueue<BatchProcessingJobService>(
            service => service.ProcessBatchAsync(jobId, fileData, null));

        _logger.LogInformation($"Batch job oluşturuldu: {jobId} (Hangfire: {hangfireJobId})");

        // Client'a hemen yanıt dön
        return Ok(new BatchJobResponse
        {
            JobId = jobId,
            Status = "queued",
            TotalFiles = files.Count,
            Message = "İşlem kuyruğa eklendi. Sonuçları /api/policy/job/{jobId} endpoint'inden takip edebilirsiniz.",
            EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(files.Count * 0.5) // ~500ms/dosya
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Batch job oluşturma hatası");
        return StatusCode(500, $"Job oluşturma hatası: {ex.Message}");
    }
}

/// <summary>
/// Job durumunu ve sonucunu getirir
/// </summary>
[HttpGet("job/{jobId}")]
public async Task<ActionResult<BatchJobStatusResponse>> GetJobStatus(string jobId)
{
    try
    {
        var db = _redis.GetDatabase();

        // Redis'ten job bilgisini al
        var statusKey = $"job:{jobId}:status";
        var resultKey = $"job:{jobId}:result";

        var status = await db.StringGetAsync(statusKey);
        if (status.IsNullOrEmpty)
            return NotFound($"Job bulunamadı: {jobId}");

        var response = new BatchJobStatusResponse
        {
            JobId = jobId,
            Status = status.ToString()
        };

        // Eğer tamamlandıysa sonucu getir
        if (status == "completed")
        {
            var resultJson = await db.StringGetAsync(resultKey);
            if (!resultJson.IsNullOrEmpty)
            {
                response.Result = System.Text.Json.JsonSerializer.Deserialize<BatchExtractionResult>(resultJson!);
            }
        }

        // Progress bilgisini getir
        var progressKey = $"job:{jobId}:progress";
        var progressJson = await db.StringGetAsync(progressKey);
        if (!progressJson.IsNullOrEmpty)
        {
            response.Progress = System.Text.Json.JsonSerializer.Deserialize<BatchProgressInfo>(progressJson!);
        }

        return Ok(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Job status getirme hatası: {jobId}");
        return StatusCode(500, $"Status getirme hatası: {ex.Message}");
    }
}

// Response modelleri
public class BatchJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime EstimatedCompletionTime { get; set; }
}

public class BatchJobStatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // queued, processing, completed, failed
    public BatchProgressInfo? Progress { get; set; }
    public BatchExtractionResult? Result { get; set; }
}
```

#### 5. BatchProcessingJobService Güncellemesi

`ProcessBatchAsync` metodunu Redis ile entegre edin:

```csharp
// Redis'e progress yaz
if (progress != null)
{
    var db = redis.GetDatabase();
    await db.StringSetAsync(
        $"job:{jobId}:progress",
        JsonSerializer.Serialize(progressInfo),
        TimeSpan.FromHours(24));
}

// İşlem tamamlandığında Redis'e yaz
await db.StringSetAsync($"job:{jobId}:status", "completed");
await db.StringSetAsync(
    $"job:{jobId}:result",
    JsonSerializer.Serialize(result),
    TimeSpan.FromDays(7)); // 7 gün saklansın
```

---

### Seçenek 2: .NET Background Service + Channels (Basit)

**Kullanım Senaryosu:** Basit projeler, Redis olmadan çalışma

**Avantajlar:**
- ✅ Ek dependency yok
- ✅ Kolay implementasyon
- ❌ Sunucu restart'ta işlemler kaybolur
- ❌ Dashboard yok

**Implementasyon:**

```csharp
// Services/BackgroundBatchProcessor.cs
public class BackgroundBatchProcessor : BackgroundService
{
    private readonly Channel<BatchJob> _channel;

    public BackgroundBatchProcessor()
    {
        _channel = Channel.CreateUnbounded<BatchJob>();
    }

    public void QueueJob(BatchJob job)
    {
        _channel.Writer.TryWrite(job);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            // Process job
            await ProcessBatchJob(job);
        }
    }
}
```

---

## 🔄 Load Balancing Yapılandırması

### IIS Application Request Routing (ARR)

**Senaryo:** Tek Windows Server, multiple app instance'lar

```powershell
# IIS ARR kurulumu
# https://www.iis.net/downloads/microsoft/application-request-routing

# Web Farm oluştur
New-WebFarm -Name "PdfReaderFarm"
Add-WebFarmServer -Name "PdfReaderFarm" -Address "localhost" -HttpPort 5001
Add-WebFarmServer -Name "PdfReaderFarm" -Address "localhost" -HttpPort 5002
Add-WebFarmServer -Name "PdfReaderFarm" -Address "localhost" -HttpPort 5003
```

### Nginx Load Balancer (Linux/Docker)

```nginx
upstream pdfreader_backend {
    least_conn; # Load balancing algoritması

    server pdfreader1:5000 weight=1;
    server pdfreader2:5000 weight=1;
    server pdfreader3:5000 weight=1;
}

server {
    listen 80;
    server_name api.example.com;

    location / {
        proxy_pass http://pdfreader_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### Azure Load Balancer + App Service

```bash
# Azure App Service Scale Out
az appservice plan update \
    --name PdfReaderPlan \
    --resource-group PdfReaderRG \
    --number-of-workers 3

# Auto-scaling kuralı
az monitor autoscale create \
    --resource-group PdfReaderRG \
    --resource PdfReaderApp \
    --min-count 2 \
    --max-count 10 \
    --count 3
```

---

## 📈 Performance Test Senaryoları

### Senaryo 1: 100 PDF Batch (Mevcut Kod)

```bash
# Test scripti
time curl -X POST "http://localhost:5109/api/policy/extract-batch" \
  -H "Content-Type: multipart/form-data" \
  -F "files=@file1.pdf" \
  ... (100 files)

# Beklenen: ~10-15 saniye (4-core CPU)
# Problem: Client beklemek zorunda
```

### Senaryo 2: 100 PDF Batch (Hangfire + Redis)

```bash
# 1. Job oluştur (hemen döner)
RESPONSE=$(curl -X POST "http://localhost:5109/api/policy/extract-batch-async" \
  -H "Content-Type: multipart/form-data" \
  -F "files=@file1.pdf" \
  ... (100 files))

JOB_ID=$(echo $RESPONSE | jq -r '.jobId')
echo "Job ID: $JOB_ID"

# 2. Status'u takip et
while true; do
  STATUS=$(curl "http://localhost:5109/api/policy/job/$JOB_ID" | jq -r '.status')
  echo "Status: $STATUS"

  if [ "$STATUS" == "completed" ]; then
    break
  fi

  sleep 2
done

# 3. Sonucu al
curl "http://localhost:5109/api/policy/job/$JOB_ID" | jq '.result'
```

---

## 🔧 Redis Kurulumu

### Windows Server

```powershell
# 1. Redis for Windows (Memurai - Redis uyumlu)
# https://www.memurai.com/get-memurai

# 2. İndir ve kur
.\memurai-setup.exe /quiet

# 3. Servisi başlat
Start-Service Memurai

# 4. Test et
redis-cli ping
# Yanıt: PONG
```

### Docker (Geliştirme)

```bash
# Redis container başlat
docker run -d \
  --name redis-pdfreader \
  -p 6379:6379 \
  redis:7-alpine \
  redis-server --appendonly yes

# Test et
docker exec -it redis-pdfreader redis-cli ping
```

### Azure Redis Cache (Production)

```bash
az redis create \
  --name pdfreader-redis \
  --resource-group PdfReaderRG \
  --location westeurope \
  --sku Standard \
  --vm-size c1

# Connection string al
az redis list-keys \
  --name pdfreader-redis \
  --resource-group PdfReaderRG
```

---

## 🎯 Önerilen Yapılandırma (Production)

### Küçük/Orta Ölçekli Sistem (< 1000 PDF/saat)

```
1x Windows Server 2022 (8 vCore, 16GB RAM)
1x Redis instance (2GB RAM)
IIS + Hangfire (8 worker)
```

**Maliyet:** ~$200-300/ay (Azure)

### Büyük Ölçekli Sistem (> 5000 PDF/saat)

```
3x Windows Server 2022 (16 vCore, 32GB RAM)
1x Azure Redis Cache (Standard C2 - 6GB)
Azure Load Balancer
Hangfire (16 worker x 3 = 48 worker)
```

**Maliyet:** ~$1000-1500/ay (Azure)

---

## 📊 Monitoring ve Dashboard

### Hangfire Dashboard

```
http://your-api.com/hangfire

- Job queue görüntüleme
- Başarılı/başarısız işlemler
- Retry mekanizması
- Real-time statistics
```

### Redis Monitoring

```bash
# Redis CLI ile monitoring
redis-cli --stat

# Memory kullanımı
redis-cli INFO memory

# Queue uzunluğu
redis-cli LLEN hangfire:pdfreader:queue:default
```

### Application Insights (Azure)

```csharp
builder.Services.AddApplicationInsightsTelemetry();

// Custom metrics
telemetryClient.TrackMetric("BatchProcessing.Duration", duration);
telemetryClient.TrackMetric("BatchProcessing.SuccessRate", successRate);
```

---

## ❓ SSS (Sık Sorulan Sorular)

### S1: Redis olmadan kullanabilir miyim?

**C:** Evet, .NET Channels + Background Service kullanabilirsiniz ancak:
- Sunucu restart'ta işlemler kaybolur
- Distributed processing yapılamaz
- Persistence yok

### S2: Kaç worker kullanmalıyım?

**C:** CPU core sayısı ile başlayın:
- 4 core → 4-8 worker
- 8 core → 8-16 worker
- 16 core → 16-32 worker

### S3: Redis memory ne kadar olmalı?

**C:** Job boyutuna göre:
- 100 PDF batch, her biri 1MB → ~100MB RAM
- 1000 job/gün → ~2-4GB RAM yeterli

### S4: Hangfire vs Custom Background Service?

**C:**
- **Hangfire**: Production-ready, dashboard, retry, monitoring
- **Custom**: Basit projeler, ek dependency istemeyenler

---

## 🚀 Sonuç ve Tavsiye

**Sizin senaryonuz için:**

1. ✅ **Hangfire + Redis** kullanın
2. ✅ Windows Server'a **Memurai** (Redis) kurun
3. ✅ IIS'te **8 worker** ile başlayın
4. ✅ `/hangfire` dashboard'u monitoring için kullanın
5. ✅ Production'da **load balancer** ekleyin (opsiyonel)

**İmplementasyon sırası:**
1. Redis kur → Test et
2. Hangfire NuGet paketlerini ekle
3. Program.cs'yi güncelle
4. BatchProcessingJobService'i entegre et
5. Yeni endpoint'leri test et
6. Dashboard'u monitoring için kullan

İhtiyacınıza göre bu guide'ı takip edebilirsiniz. Detaylı sorularınız için hazırım!
