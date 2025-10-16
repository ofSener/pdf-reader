# Windows Server 2022 + IIS Deployment Guide

Bu dokümantasyon, PDF Reader API'sini Windows Server 2022 üzerinde IIS ile yayınlamak için gerekli tüm adımları içerir.

## 📋 Gereksinimler

### 1. Sistem Gereksinimleri

#### Minimum:
- **OS**: Windows Server 2022
- **RAM**: 4 GB
- **CPU**: 2 Core
- **Disk**: 10 GB boş alan

#### Önerilen:
- **OS**: Windows Server 2022 (Latest updates)
- **RAM**: 8 GB+
- **CPU**: 4+ Core (Batch işlemler için)
- **Disk**: 20 GB+ SSD

### 2. Yazılım Gereksinimleri

#### A. .NET Runtime (ZORUNLU)
- **.NET 9.0 Runtime** (Hosting Bundle)
- İndirme: https://dotnet.microsoft.com/download/dotnet/9.0
- Dosya: `dotnet-hosting-9.0.x-win.exe`

**Kurulum:**
```powershell
# PowerShell (Administrator olarak çalıştırın)
# İndirilen dosyayı çalıştırın
.\dotnet-hosting-9.0.x-win.exe /quiet /norestart

# Kurulumu doğrulayın
dotnet --list-runtimes
# Çıktıda "Microsoft.AspNetCore.App 9.0.x" görünmeli
```

#### B. IIS (Internet Information Services)

**IIS Kurulumu:**
```powershell
# PowerShell (Administrator)
Install-WindowsFeature -name Web-Server -IncludeManagementTools
Install-WindowsFeature -name Web-WebSockets
Install-WindowsFeature -name Web-Asp-Net45
```

**Gerekli IIS Modülleri:**
- IIS Core
- ASP.NET Core Module V2 (.NET Hosting Bundle ile gelir)
- WebSockets (opsiyonel)
- Static Content
- Default Document

### 3. Güvenlik Gereksinimleri

#### Firewall Kuralları:
```powershell
# HTTP (80) portunu aç
New-NetFirewallRule -DisplayName "HTTP Inbound" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow

# HTTPS (443) portunu aç
New-NetFirewallRule -DisplayName "HTTPS Inbound" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
```

#### SSL Sertifikası (Production için ZORUNLU):
- Let's Encrypt (ücretsiz)
- Commercial SSL sertifikası
- Self-signed certificate (sadece test için)

## 🔧 Deployment Adımları

### Adım 1: Projeyi Publish Edin

**Geliştirme Makinesinde:**
```bash
# Projeyi publish edin (Release mode)
cd /path/to/Pdf-Reader
dotnet publish -c Release -o ./publish

# publish klasörü oluşturulacak:
# - Pdf-Reader.dll
# - appsettings.json
# - web.config (otomatik oluşturulur)
# - Tüm dependencies
```

### Adım 2: Dosyaları Sunucuya Kopyalayın

**Hedef Klasör Yapısı:**
```
C:\inetpub\
└── PdfReaderAPI\
    ├── Pdf-Reader.dll
    ├── appsettings.json
    ├── web.config
    ├── appsettings.Production.json (opsiyonel)
    └── logs\ (klasör oluşturun)
```

**PowerShell ile kopyalama:**
```powershell
# Hedef klasörü oluşturun
New-Item -ItemType Directory -Path "C:\inetpub\PdfReaderAPI" -Force

# Dosyaları kopyalayın
Copy-Item -Path "\\source\publish\*" -Destination "C:\inetpub\PdfReaderAPI\" -Recurse -Force

# Logs klasörü oluşturun
New-Item -ItemType Directory -Path "C:\inetpub\PdfReaderAPI\logs" -Force
```

### Adım 3: IIS Application Pool Oluşturun

**IIS Manager ile:**
1. IIS Manager'ı açın (`inetmgr`)
2. Application Pools → Add Application Pool
3. Ayarlar:
   - **Name**: `PdfReaderAPI`
   - **.NET CLR Version**: `No Managed Code` (ZORUNLU - .NET Core kendi runtime'ını kullanır)
   - **Managed Pipeline Mode**: `Integrated`
   - **Start Immediately**: ✓

**PowerShell ile:**
```powershell
Import-Module WebAdministration

# Application Pool oluştur
New-WebAppPool -Name "PdfReaderAPI"

# Ayarları yapılandır
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "managedPipelineMode" -Value "Integrated"

# Identity ayarla (güvenlik için)
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

# Performance ayarları
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "recycling.periodicRestart.time" -Value "00:00:00"
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "processModel.idleTimeout" -Value "00:00:00"
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "startMode" -Value "AlwaysRunning"
```

**ÖNEMLI - Batch İşlemler için App Pool Ayarları:**
```powershell
# Queue Length (eşzamanlı request sayısı)
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "queueLength" -Value "5000"

# CPU Limit (unlimited)
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "cpu.limit" -Value "0"

# Memory Limit (isteğe bağlı, örn: 2GB)
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "recycling.periodicRestart.privateMemory" -Value "2097152"

# Timeout ayarları
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "processModel.shutdownTimeLimit" -Value "00:01:00"
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "processModel.startupTimeLimit" -Value "00:01:00"
```

### Adım 4: IIS Website/Application Oluşturun

**IIS Manager ile:**
1. Sites → Add Website
2. Ayarlar:
   - **Site Name**: `PdfReaderAPI`
   - **Application Pool**: `PdfReaderAPI`
   - **Physical Path**: `C:\inetpub\PdfReaderAPI`
   - **Binding**:
     - Type: `http`
     - Port: `80` (veya istediğiniz port)
     - Host name: `api.yourcompany.com` (opsiyonel)

**PowerShell ile:**
```powershell
# Website oluştur
New-Website -Name "PdfReaderAPI" `
    -PhysicalPath "C:\inetpub\PdfReaderAPI" `
    -ApplicationPool "PdfReaderAPI" `
    -Port 80

# HTTPS binding ekle (SSL sertifika hash'i ile)
New-WebBinding -Name "PdfReaderAPI" -Protocol "https" -Port 443 -SslFlags 0

# SSL sertifikayı bağla
$cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*yourcompany.com*"}
New-Item -Path "IIS:\SslBindings\0.0.0.0!443" -Value $cert
```

### Adım 5: İzinleri Ayarlayın

**ZORUNLU - Application Pool Identity'ye izinler verin:**
```powershell
# Application klasörüne Read/Execute izni
$acl = Get-Acl "C:\inetpub\PdfReaderAPI"
$identity = "IIS AppPool\PdfReaderAPI"
$fileSystemRights = "ReadAndExecute"
$type = "Allow"

$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, $fileSystemRights, $type)
$acl.SetAccessRule($accessRule)
Set-Acl "C:\inetpub\PdfReaderAPI" $acl

# Logs klasörüne Write izni
$acl = Get-Acl "C:\inetpub\PdfReaderAPI\logs"
$identity = "IIS AppPool\PdfReaderAPI"
$fileSystemRights = "Modify"
$type = "Allow"

$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, $fileSystemRights, $type)
$acl.SetAccessRule($accessRule)
Set-Acl "C:\inetpub\PdfReaderAPI\logs" $acl
```

### Adım 6: web.config Ayarları

`C:\inetpub\PdfReaderAPI\web.config` dosyasını kontrol edin:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\Pdf-Reader.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>

      <!-- Request limits for batch processing -->
      <security>
        <requestFiltering>
          <requestLimits maxAllowedContentLength="524288000" /> <!-- 500 MB -->
        </requestFiltering>
      </security>
    </system.webServer>
  </location>
</configuration>
```

**BATCH İŞLEMLER İÇİN ÖNEMLI:**
```xml
<!-- web.config içine ekleyin -->
<system.webServer>
  <!-- Request timeout ayarları -->
  <asp scriptTimeout="00:10:00" />

  <!-- Compression (performans için) -->
  <httpCompression>
    <dynamicTypes>
      <add mimeType="application/json" enabled="true" />
    </dynamicTypes>
  </httpCompression>

  <!-- CORS ayarları (gerekirse) -->
  <httpProtocol>
    <customHeaders>
      <add name="Access-Control-Allow-Origin" value="*" />
      <add name="Access-Control-Allow-Methods" value="GET, POST, OPTIONS" />
      <add name="Access-Control-Allow-Headers" value="Content-Type" />
    </customHeaders>
  </httpProtocol>
</system.webServer>
```

### Adım 7: appsettings.Production.json

Production ayarlarını ekleyin:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Pdf_Reader": "Information"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 524288000,
      "RequestHeadersTimeout": "00:05:00"
    }
  }
}
```

## 🚀 Deployment Sonrası Kontroller

### 1. Servisi Test Edin

```powershell
# Browser veya PowerShell ile test edin
Invoke-WebRequest -Uri "http://localhost/health" -Method GET

# Beklenen yanıt:
# {
#   "status": "healthy",
#   "timestamp": "2024-01-01T00:00:00Z",
#   "version": "1.0.0"
# }
```

### 2. Event Viewer'da Hataları Kontrol Edin

```powershell
# PowerShell
Get-EventLog -LogName Application -Source "IIS AspNetCore Module V2" -Newest 10
```

### 3. stdout Loglarını Kontrol Edin

```powershell
# Logs klasöründeki en son log dosyasını görüntüleyin
Get-Content "C:\inetpub\PdfReaderAPI\logs\stdout_*.log" -Tail 50
```

## ⚡ Performance Optimizasyonu

### 1. IIS Request Timeout Ayarları

```xml
<!-- web.config -->
<system.webServer>
  <asp scriptTimeout="00:10:00" />
</system.webServer>
```

### 2. Application Pool Recycling

```powershell
# Otomatik recycling'i devre dışı bırakın (batch işlemler için)
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "recycling.periodicRestart.time" -Value "00:00:00"

# Bellek limiti ayarlayın (opsiyonel)
Set-ItemProperty "IIS:\AppPools\PdfReaderAPI" -Name "recycling.periodicRestart.privateMemory" -Value "4194304" # 4GB
```

### 3. Thread Pool Ayarları

`appsettings.Production.json` içine ekleyin:

```json
{
  "BatchProcessing": {
    "MaxDegreeOfParallelism": 4,
    "MaxBatchSize": 100
  }
}
```

## 🔒 Güvenlik

### 1. HTTPS Zorunlu Tutun

```xml
<!-- web.config -->
<system.webServer>
  <rewrite>
    <rules>
      <rule name="HTTPS Redirect" stopProcessing="true">
        <match url="(.*)" />
        <conditions>
          <add input="{HTTPS}" pattern="off" />
        </conditions>
        <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

### 2. API Key Authentication (önerilen)

Middleware ekleyin:
```csharp
// Program.cs
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
```

### 3. Rate Limiting

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

## 📊 Monitoring

### 1. Application Insights (önerilen)

```bash
# NuGet package ekleyin
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

### 2. Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks();
app.MapHealthChecks("/health");
```

### 3. Performance Counters

```powershell
# IIS Performance counters'ı etkinleştirin
Get-Counter "\ASP.NET Applications(*)\Requests/Sec"
Get-Counter "\Process(w3wp)\% Processor Time"
Get-Counter "\Process(w3wp)\Private Bytes"
```

## 🔄 Update/Deployment Süreci

### Zero-Downtime Deployment

```powershell
# 1. Yeni versiyonu farklı klasöre kopyalayın
Copy-Item -Path "\\source\publish\*" -Destination "C:\inetpub\PdfReaderAPI_v2\" -Recurse

# 2. App Pool'u durdurun
Stop-WebAppPool -Name "PdfReaderAPI"

# 3. Dosyaları değiştirin
Remove-Item "C:\inetpub\PdfReaderAPI\*" -Recurse -Force
Copy-Item -Path "C:\inetpub\PdfReaderAPI_v2\*" -Destination "C:\inetpub\PdfReaderAPI\" -Recurse

# 4. App Pool'u başlatın
Start-WebAppPool -Name "PdfReaderAPI"

# 5. Test edin
Invoke-WebRequest -Uri "http://localhost/health"
```

## 🐛 Troubleshooting

### Problem: 500.30 - ASP.NET Core app failed to start

**Çözüm:**
```powershell
# 1. .NET Runtime'ı kontrol edin
dotnet --list-runtimes

# 2. Stdout loglarını kontrol edin
Get-Content "C:\inetpub\PdfReaderAPI\logs\stdout_*.log"

# 3. Event Viewer'ı kontrol edin
Get-EventLog -LogName Application -Source "IIS AspNetCore Module V2" -Newest 10
```

### Problem: 502.5 - Process Failure

**Çözüm:**
- App Pool identity izinlerini kontrol edin
- web.config'i kontrol edin
- .NET Runtime versiyonunu doğrulayın

### Problem: Request timeout

**Çözüm:**
```xml
<!-- web.config -->
<system.webServer>
  <asp scriptTimeout="00:10:00" />
</system.webServer>
```

## 📚 Ek Kaynaklar

- [ASP.NET Core IIS Deployment](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/)
- [Windows Server Security Best Practices](https://docs.microsoft.com/en-us/windows-server/security/)
- [IIS Performance Tuning](https://docs.microsoft.com/en-us/windows-server/administration/performance-tuning/role/web-server/)
