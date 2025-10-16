# PDF Policy Extractor API - .NET 5 Desktop Entegrasyon Kılavuzu

## 📋 İçindekiler
1. [Genel Bakış](#genel-bakış)
2. [API Base URL](#api-base-url)
3. [Kurulum (.NET 5 Desktop App)](#kurulum-net-5-desktop-app)
4. [Endpoint'ler](#endpointler)
5. [Kod Örnekleri](#kod-örnekleri)
6. [Hata Yönetimi](#hata-yönetimi)
7. [Best Practices](#best-practices)

---

## Genel Bakış

PDF Policy Extractor API, sigorta poliçesi PDF dosyalarından otomatik veri çıkarma hizmeti sunar.

### Özellikler
- ✅ 21+ Türk sigorta şirketi desteği
- ✅ Tek ve çoklu PDF işleme
- ✅ Asenkron batch processing
- ✅ Real-time progress tracking
- ✅ %97+ doğruluk oranı

---

## API Base URL

```
Production: https://aivoice.sigorta.teklifi.al
```

### Swagger UI
```
https://aivoice.sigorta.teklifi.al/swagger
```

### Hangfire Dashboard (Job Monitoring)
```
https://aivoice.sigorta.teklifi.al/hangfire
```

---

## Kurulum (.NET 5 Desktop App)

### 1. NuGet Paketleri

```bash
# HttpClient için (genelde zaten var)
Install-Package System.Net.Http

# JSON serileştirme
Install-Package System.Text.Json

# Veya Newtonsoft.Json tercih ediyorsanız
Install-Package Newtonsoft.Json
```

### 2. API Client Sınıfı Oluşturma

Projenize `PdfPolicyApiClient.cs` dosyası ekleyin (aşağıda tam kod)

---

## Endpoint'ler

### 1. Health Check
**GET** `/api/Policy/health`

API'nin çalışır durumda olup olmadığını kontrol eder.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-10-16T08:17:37Z",
  "version": "1.0.0",
  "service": "PDF Policy Extractor API"
}
```

---

### 2. Tek PDF İşleme (Senkron)
**POST** `/api/Policy/extract`

Tek bir PDF dosyasını işler ve sonucu hemen döner.

**Request:**
- Method: POST
- Content-Type: multipart/form-data
- Body: PDF file (max 50MB)

**Response:**
```json
{
  "fileName": "police.pdf",
  "success": true,
  "data": {
    "company": "Anadolu Sigorta",
    "companyType": 6,
    "policyNumber": "1234567890",
    "policyType": "Trafik",
    "startDate": "2024-01-01T00:00:00",
    "endDate": "2025-01-01T00:00:00",
    "tanzimDate": "2023-12-20T00:00:00",
    "netPremium": 1250.50,
    "grossPremium": 1400.00,
    "taxAmount": 149.50,
    "totalAmount": 1400.00,
    "insuredName": "AHMET YILMAZ",
    "insuredTcNo": "12345678901",
    "plateNumber": "34ABC123",
    "vehicleBrand": "TOYOTA",
    "vehicleModel": "COROLLA",
    "confidenceScore": 0.95
  },
  "errors": [],
  "warnings": [],
  "processingTimeMs": 523
}
```

---

### 3. Text-Based Extraction (PDF Upload Olmadan)
**POST** `/api/Policy/extract-from-text`

PDF dosyası upload etmek yerine, önceden çıkarılmış PDF text'ini JSON olarak gönderir. PDF okuma adımını atlar.

**Kullanım Senaryosu:** Yazılım ekibiniz PDF'leri kendi sistemlerinde text'e çeviriyorsa, bu endpoint'i kullanabilirsiniz.

**Request:**
- Method: POST
- Content-Type: application/json
- Body:
```json
{
  "pdfText": "ANADOLU SİGORTA\nPoliçe No: 1234567890\nBaşlangıç: 01.01.2024\n...",
  "fileName": "police_001.pdf" // opsiyonel
}
```

**Response:**
```json
{
  "fileName": "police_001.pdf",
  "success": true,
  "data": {
    "company": "Anadolu Sigorta",
    "policyNumber": "1234567890",
    "policyType": "Trafik",
    "startDate": "2024-01-01T00:00:00",
    "endDate": "2025-01-01T00:00:00",
    "netPremium": 1250.50,
    "grossPremium": 1400.00,
    "insuredName": "AHMET YILMAZ",
    "plateNumber": "34ABC123",
    "confidenceScore": 0.95
  },
  "errors": [],
  "warnings": [],
  "processingTimeMs": 320
}
```


---

### 4. Batch İşlem - Senkron
**POST** `/api/Policy/extract-batch`

Birden fazla PDF'i paralel olarak işler. Client işlem bitene kadar bekler.

**Request:**
- Method: POST
- Content-Type: multipart/form-data
- Body: Multiple PDF files

**Response:**
```json
{
  "totalFiles": 10,
  "successCount": 9,
  "failureCount": 1,
  "results": [
    {
      "fileName": "police1.pdf",
      "success": true,
      "data": { ... },
      "processingTimeMs": 450
    },
    ...
  ]
}
```

**Not:** Max 100 dosya, client işlem bitene kadar bekler.

---

### 5. Batch İşlem - Asenkron (Önerilen)
**POST** `/api/Policy/extract-batch-async`

Batch işlemi için job oluşturur ve hemen döner. İşlem background'da devam eder.

**Request:**
- Method: POST
- Content-Type: multipart/form-data
- Body: Multiple PDF files

**Hemen Dönen Response:**
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

**Kullanım:**
1. Bu endpoint'e PDF'leri gönder
2. JobId'yi al
3. Job durumunu periyodik olarak kontrol et (`GET /api/Policy/job/{jobId}`)

---

### 6. Job Status Sorgulama
**GET** `/api/Policy/job/{jobId}`

Asenkron batch işlemin durumunu ve sonucunu getirir.

**Response (Processing):**
```json
{
  "jobId": "a1b2c3d4-...",
  "status": "processing",
  "progress": {
    "totalFiles": 100,
    "processedFiles": 45,
    "currentFile": "policy_045.pdf",
    "percentageComplete": 45
  },
  "startedAt": "2025-01-16T10:00:00Z"
}
```

**Response (Completed):**
```json
{
  "jobId": "a1b2c3d4-...",
  "status": "completed",
  "result": {
    "totalFiles": 100,
    "successCount": 98,
    "failureCount": 2,
    "results": [ ... ]
  },
  "startedAt": "2025-01-16T10:00:00Z",
  "completedAt": "2025-01-16T10:02:30Z"
}
```

**Status Değerleri:**
- `queued`: İşlem kuyruğa eklendi
- `processing`: İşlem devam ediyor
- `completed`: İşlem tamamlandı
- `failed`: İşlem başarısız

---

### 7. Desteklenen Şirketler Listesi
**GET** `/api/Policy/companies`

API'nin desteklediği sigorta şirketlerini listeler.

**Response:**
```json
[
  "Anadolu",
  "Allianz",
  "Ankara",
  "Axa",
  "Doga",
  "HDI",
  "Hepiyi",
  "Mapfre",
  "Neova",
  "Quick",
  "Ray",
  "Sompo",
  "Unico",
  "Zurich",
  ...
]
```

---

### 8. Desteklenen Poliçe Tipleri
**GET** `/api/Policy/policy-types`

API'nin desteklediği poliçe tiplerini listeler.

**Response:**
```json
[
  "Trafik",
  "Kasko",
  "DASK",
  "Konut",
  "TSS",
  "FerdiKaza",
  "Saglik"
]
```

---

## Kod Örnekleri

### Tam API Client Sınıfı (.NET 5)

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace YourDesktopApp.Services
{
    /// <summary>
    /// PDF Policy Extractor API Client
    /// </summary>
    public class PdfPolicyApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public PdfPolicyApiClient(string baseUrl = "https://aivoice.sigorta.teklifi.al")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromMinutes(5) // Uzun işlemler için
            };
        }

        #region Health Check

        /// <summary>
        /// API'nin çalışır durumda olup olmadığını kontrol eder
        /// </summary>
        public async Task<HealthCheckResponse> CheckHealthAsync()
        {
            var response = await _httpClient.GetAsync("/api/Policy/health");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<HealthCheckResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        #endregion

        #region Tek PDF İşleme

        /// <summary>
        /// Tek bir PDF dosyasını işler (Senkron)
        /// </summary>
        /// <param name="pdfFilePath">PDF dosyasının tam yolu</param>
        public async Task<PolicyExtractionResult> ExtractPolicyAsync(string pdfFilePath)
        {
            if (!File.Exists(pdfFilePath))
                throw new FileNotFoundException("PDF dosyası bulunamadı", pdfFilePath);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(File.ReadAllBytes(pdfFilePath));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "file", Path.GetFileName(pdfFilePath));

            var response = await _httpClient.PostAsync("/api/Policy/extract", content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API Error: {response.StatusCode} - {json}");
            }

            return JsonSerializer.Deserialize<PolicyExtractionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        #endregion

        #region Text-Based Extraction

        /// <summary>
        /// PDF text'inden direkt veri çıkarır (PDF upload gerektirmez)
        /// </summary>
        /// <param name="pdfText">PDF'ten çıkarılmış text</param>
        /// <param name="fileName">Opsiyonel dosya adı</param>
        public async Task<PolicyExtractionResult> ExtractFromTextAsync(string pdfText, string fileName = null)
        {
            var requestBody = new
            {
                pdfText = pdfText,
                fileName = fileName
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/Policy/extract-from-text", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API Error: {response.StatusCode} - {responseJson}");
            }

            return JsonSerializer.Deserialize<PolicyExtractionResult>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        #endregion

        #region Batch İşlem - Senkron

        /// <summary>
        /// Birden fazla PDF'i paralel olarak işler (Senkron - client bekler)
        /// Max 100 dosya
        /// </summary>
        public async Task<BatchExtractionResult> ExtractBatchAsync(List<string> pdfFilePaths)
        {
            if (pdfFilePaths.Count > 100)
                throw new ArgumentException("Maksimum 100 dosya gönderilebilir");

            using var content = new MultipartFormDataContent();

            foreach (var filePath in pdfFilePaths)
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Uyarı: Dosya bulunamadı: {filePath}");
                    continue;
                }

                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "files", Path.GetFileName(filePath));
            }

            var response = await _httpClient.PostAsync("/api/Policy/extract-batch", content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API Error: {response.StatusCode} - {json}");
            }

            return JsonSerializer.Deserialize<BatchExtractionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        #endregion

        #region Batch İşlem - Asenkron (Background Job)

        /// <summary>
        /// Batch işlemi için job oluşturur (Asenkron - hemen döner)
        /// İşlem background'da devam eder
        /// </summary>
        public async Task<BatchJobResponse> ExtractBatchAsyncWithJob(List<string> pdfFilePaths)
        {
            if (pdfFilePaths.Count > 100)
                throw new ArgumentException("Maksimum 100 dosya gönderilebilir");

            using var content = new MultipartFormDataContent();

            foreach (var filePath in pdfFilePaths)
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Uyarı: Dosya bulunamadı: {filePath}");
                    continue;
                }

                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "files", Path.GetFileName(filePath));
            }

            var response = await _httpClient.PostAsync("/api/Policy/extract-batch-async", content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API Error: {response.StatusCode} - {json}");
            }

            return JsonSerializer.Deserialize<BatchJobResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        /// <summary>
        /// Job durumunu sorgular
        /// </summary>
        public async Task<BatchJobStatusResponse> GetJobStatusAsync(string jobId)
        {
            var response = await _httpClient.GetAsync($"/api/Policy/job/{jobId}");
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API Error: {response.StatusCode} - {json}");
            }

            return JsonSerializer.Deserialize<BatchJobStatusResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        /// <summary>
        /// Job tamamlanana kadar bekler ve sonucu döner
        /// Progress callback ile ilerleme takibi yapılabilir
        /// </summary>
        public async Task<BatchExtractionResult> WaitForJobCompletionAsync(
            string jobId,
            Action<BatchProgressInfo> progressCallback = null,
            int pollIntervalSeconds = 2)
        {
            while (true)
            {
                var status = await GetJobStatusAsync(jobId);

                if (status.Status == "completed")
                {
                    return status.Result;
                }
                else if (status.Status == "failed")
                {
                    throw new Exception($"Job başarısız: {status.ErrorMessage}");
                }
                else if (status.Status == "processing" && status.Progress != null)
                {
                    progressCallback?.Invoke(status.Progress);
                }

                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Desteklenen sigorta şirketlerini getirir
        /// </summary>
        public async Task<List<string>> GetSupportedCompaniesAsync()
        {
            var response = await _httpClient.GetAsync("/api/Policy/companies");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<string>>(json);
        }

        /// <summary>
        /// Desteklenen poliçe tiplerini getirir
        /// </summary>
        public async Task<List<string>> GetSupportedPolicyTypesAsync()
        {
            var response = await _httpClient.GetAsync("/api/Policy/policy-types");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<string>>(json);
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #region Response Models

    public class HealthCheckResponse
    {
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public string Version { get; set; }
        public string Service { get; set; }
    }

    public class PolicyExtractionResult
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public PolicyData Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public int ProcessingTimeMs { get; set; }
    }

    public class PolicyData
    {
        public string Company { get; set; }
        public int CompanyType { get; set; }
        public string PolicyNumber { get; set; }
        public string PolicyType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? TanzimDate { get; set; }
        public decimal? NetPremium { get; set; }
        public decimal? GrossPremium { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TotalAmount { get; set; }
        public string InsuredName { get; set; }
        public string InsuredTcNo { get; set; }
        public string PlateNumber { get; set; }
        public string VehicleBrand { get; set; }
        public string VehicleModel { get; set; }
        public double ConfidenceScore { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class BatchExtractionResult
    {
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<PolicyExtractionResult> Results { get; set; } = new List<PolicyExtractionResult>();
    }

    public class BatchJobResponse
    {
        public string JobId { get; set; }
        public string Status { get; set; }
        public int TotalFiles { get; set; }
        public string Message { get; set; }
        public DateTime EstimatedCompletionTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BatchJobStatusResponse
    {
        public string JobId { get; set; }
        public string Status { get; set; }
        public BatchProgressInfo Progress { get; set; }
        public BatchExtractionResult Result { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BatchProgressInfo
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentFile { get; set; }
        public int PercentageComplete { get; set; }
    }

    #endregion
}
```

---

### Kullanım Örnekleri

#### Örnek 1: Tek PDF İşleme

```csharp
using (var apiClient = new PdfPolicyApiClient())
{
    try
    {
        // Health check
        var health = await apiClient.CheckHealthAsync();
        Console.WriteLine($"API Status: {health.Status}");

        // Tek PDF işle
        var result = await apiClient.ExtractPolicyAsync(@"C:\Policies\police.pdf");

        if (result.Success)
        {
            Console.WriteLine($"Şirket: {result.Data.Company}");
            Console.WriteLine($"Poliçe No: {result.Data.PolicyNumber}");
            Console.WriteLine($"Brüt Prim: {result.Data.GrossPremium:C2}");
            Console.WriteLine($"Plaka: {result.Data.PlateNumber}");
        }
        else
        {
            Console.WriteLine("Hata: " + string.Join(", ", result.Errors));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Hata: {ex.Message}");
    }
}
```

#### Örnek 2: Batch İşlem - Senkron

```csharp
using (var apiClient = new PdfPolicyApiClient())
{
    var pdfFiles = new List<string>
    {
        @"C:\Policies\policy1.pdf",
        @"C:\Policies\policy2.pdf",
        @"C:\Policies\policy3.pdf"
    };

    var batchResult = await apiClient.ExtractBatchAsync(pdfFiles);

    Console.WriteLine($"Toplam: {batchResult.TotalFiles}");
    Console.WriteLine($"Başarılı: {batchResult.SuccessCount}");
    Console.WriteLine($"Başarısız: {batchResult.FailureCount}");

    foreach (var result in batchResult.Results)
    {
        Console.WriteLine($"\n{result.FileName}:");
        if (result.Success)
        {
            Console.WriteLine($"  ✓ Şirket: {result.Data.Company}");
            Console.WriteLine($"  ✓ Poliçe: {result.Data.PolicyNumber}");
        }
        else
        {
            Console.WriteLine($"  ✗ Hata: {string.Join(", ", result.Errors)}");
        }
    }
}
```

#### Örnek 3: Batch İşlem - Asenkron (Progress Bar ile)

```csharp
using (var apiClient = new PdfPolicyApiClient())
{
    // Tüm PDF'leri bul
    var pdfFiles = Directory.GetFiles(@"C:\Policies", "*.pdf").ToList();
    Console.WriteLine($"{pdfFiles.Count} dosya bulundu.");

    // Job oluştur
    var jobResponse = await apiClient.ExtractBatchAsyncWithJob(pdfFiles);
    Console.WriteLine($"Job oluşturuldu: {jobResponse.JobId}");
    Console.WriteLine($"Tahmini süre: {jobResponse.EstimatedCompletionTime:HH:mm:ss}");

    // Progress callback
    void OnProgress(BatchProgressInfo progress)
    {
        var percent = progress.PercentageComplete;
        Console.Write($"\r[{new string('█', percent / 2)}{new string('░', 50 - percent / 2)}] {percent}% ({progress.ProcessedFiles}/{progress.TotalFiles})");
    }

    // Job tamamlanana kadar bekle
    var result = await apiClient.WaitForJobCompletionAsync(
        jobResponse.JobId,
        progressCallback: OnProgress,
        pollIntervalSeconds: 2
    );

    Console.WriteLine($"\n\nTamamlandı!");
    Console.WriteLine($"Başarılı: {result.SuccessCount}/{result.TotalFiles}");

    // Sonuçları işle
    foreach (var item in result.Results.Where(r => r.Success))
    {
        // Veritabanına kaydet, rapor oluştur, vb.
        SaveToDatabase(item.Data);
    }
}
```

#### Örnek 4: WinForms ile Entegrasyon

```csharp
// Form1.cs
public partial class Form1 : Form
{
    private PdfPolicyApiClient _apiClient;

    public Form1()
    {
        InitializeComponent();
        _apiClient = new PdfPolicyApiClient();
    }

    private async void btnUploadPdf_Click(object sender, EventArgs e)
    {
        using (var openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "PDF Dosyaları|*.pdf";
            openFileDialog.Multiselect = false;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // UI'yi devre dışı bırak
                    btnUploadPdf.Enabled = false;
                    progressBar1.Style = ProgressBarStyle.Marquee;
                    lblStatus.Text = "PDF işleniyor...";

                    // API'ye gönder
                    var result = await _apiClient.ExtractPolicyAsync(openFileDialog.FileName);

                    // Sonuçları göster
                    if (result.Success)
                    {
                        txtCompany.Text = result.Data.Company;
                        txtPolicyNumber.Text = result.Data.PolicyNumber;
                        txtGrossPremium.Text = result.Data.GrossPremium?.ToString("C2");
                        txtPlate.Text = result.Data.PlateNumber;

                        lblStatus.Text = "Başarılı!";
                        MessageBox.Show("Poliçe başarıyla işlendi!", "Başarılı",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        lblStatus.Text = "Hata oluştu";
                        MessageBox.Show(string.Join("\n", result.Errors), "Hata",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "Hata oluştu";
                    MessageBox.Show($"Hata: {ex.Message}", "Hata",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    // UI'yi tekrar aktif et
                    btnUploadPdf.Enabled = true;
                    progressBar1.Style = ProgressBarStyle.Blocks;
                }
            }
        }
    }

    private async void btnBatchProcess_Click(object sender, EventArgs e)
    {
        using (var folderDialog = new FolderBrowserDialog())
        {
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                var pdfFiles = Directory.GetFiles(folderDialog.SelectedPath, "*.pdf").ToList();

                if (pdfFiles.Count == 0)
                {
                    MessageBox.Show("Klasörde PDF dosyası bulunamadı!", "Uyarı",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    btnBatchProcess.Enabled = false;
                    progressBar1.Value = 0;
                    lblStatus.Text = $"{pdfFiles.Count} dosya işleniyor...";

                    // Job oluştur
                    var jobResponse = await _apiClient.ExtractBatchAsyncWithJob(pdfFiles);

                    // Progress ile takip et
                    var result = await _apiClient.WaitForJobCompletionAsync(
                        jobResponse.JobId,
                        progress =>
                        {
                            // UI thread'de progress bar'ı güncelle
                            this.Invoke((MethodInvoker)delegate
                            {
                                progressBar1.Value = progress.PercentageComplete;
                                lblStatus.Text = $"{progress.ProcessedFiles}/{progress.TotalFiles} dosya işlendi";
                            });
                        }
                    );

                    // Sonuçları göster
                    var message = $"Toplam: {result.TotalFiles}\n" +
                                  $"Başarılı: {result.SuccessCount}\n" +
                                  $"Başarısız: {result.FailureCount}";

                    MessageBox.Show(message, "Tamamlandı",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // DataGridView'e yükle
                    dgvResults.DataSource = result.Results
                        .Select(r => new
                        {
                            DosyaAdı = r.FileName,
                            Durum = r.Success ? "✓" : "✗",
                            Şirket = r.Data?.Company,
                            PoliçeNo = r.Data?.PolicyNumber,
                            BrütPrim = r.Data?.GrossPremium
                        })
                        .ToList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Hata",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnBatchProcess.Enabled = true;
                    lblStatus.Text = "Hazır";
                }
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _apiClient?.Dispose();
        base.OnFormClosing(e);
    }
}
```

#### Örnek 5: Text-Based Extraction (PDF Upload Olmadan)

```csharp
using (var apiClient = new PdfPolicyApiClient())
{
    // Senaryo: PDF'leri zaten text'e çevirdiniz
    string pdfText = @"
        ANADOLU SİGORTA A.Ş.
        TRAFİK SİGORTASI POLİÇESİ

        Poliçe No: 1234567890
        Başlangıç Tarihi: 01.01.2024
        Bitiş Tarihi: 01.01.2025

        Sigortalı: AHMET YILMAZ
        TC No: 12345678901
        Plaka: 34ABC123

        Net Prim: 1.250,50 TL
        Brüt Prim: 1.400,00 TL
    ";

    try
    {
        // Text'ten direkt veri çıkar
        var result = await apiClient.ExtractFromTextAsync(
            pdfText: pdfText,
            fileName: "policy_001.pdf" // Opsiyonel
        );

        if (result.Success)
        {
            Console.WriteLine($"✓ Şirket: {result.Data.Company}");
            Console.WriteLine($"✓ Poliçe No: {result.Data.PolicyNumber}");
            Console.WriteLine($"✓ Sigortalı: {result.Data.InsuredName}");
            Console.WriteLine($"✓ Plaka: {result.Data.PlateNumber}");
            Console.WriteLine($"✓ Brüt Prim: {result.Data.GrossPremium:C2}");
            Console.WriteLine($"⏱ İşlem süresi: {result.ProcessingTimeMs}ms");
        }
        else
        {
            Console.WriteLine("Hata: " + string.Join(", ", result.Errors));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Hata: {ex.Message}");
    }
}
```

**Avantajları:**
- PDF upload gerektirmez (bandwidth tasarrufu)
- Daha hızlı (PDF parsing adımı atlanır)
- Kendi PDF-to-text çözümünüzü kullanabilirsiniz
- Büyük dosyalar için daha verimli

**Kullanım Senaryoları:**
1. PDF'leri zaten başka bir sistemde text'e çeviriyorsanız
2. PDF'ler çok büyükse ve network bandwidth sınırlıysa
3. Kendi OCR/PDF parser çözümünüzü kullanmak istiyorsanız
4. Text verisi zaten bir veritabanında/cache'te mevcutsa

---

## Hata Yönetimi

### HTTP Status Codes

- **200 OK**: Başarılı
- **400 Bad Request**: Geçersiz istek (dosya yok, format hatalı)
- **404 Not Found**: Job bulunamadı
- **500 Internal Server Error**: Sunucu hatası

### Hata Response Formatı

```json
{
  "error": true,
  "message": "Sunucu hatası oluştu",
  "details": "Detaylı hata mesajı",
  "timestamp": "2025-10-16T08:17:37Z"
}
```

### Try-Catch Pattern

```csharp
try
{
    var result = await apiClient.ExtractPolicyAsync(pdfPath);

    if (!result.Success)
    {
        // İşlem başarısız ama API yanıt verdi
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"Hata: {error}");
        }

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"Uyarı: {warning}");
        }
    }
}
catch (HttpRequestException ex)
{
    // Network veya HTTP hatası
    Console.WriteLine($"API Hatası: {ex.Message}");
}
catch (FileNotFoundException ex)
{
    // Dosya bulunamadı
    Console.WriteLine($"Dosya Hatası: {ex.Message}");
}
catch (Exception ex)
{
    // Diğer hatalar
    Console.WriteLine($"Beklenmeyen Hata: {ex.Message}");
}
```

---

## Best Practices

### 1. HttpClient Yönetimi
```csharp
// ❌ YANLIŞ - Her istekte yeni HttpClient
public async Task<PolicyExtractionResult> ExtractPolicy(string path)
{
    using (var client = new HttpClient()) // Her seferinde yeni client!
    {
        // ...
    }
}

// ✓ DOĞRU - Tek HttpClient, tüm uygulama boyunca
private static readonly PdfPolicyApiClient _apiClient = new PdfPolicyApiClient();
```

### 2. Timeout Ayarları
```csharp
// Uzun batch işlemler için timeout'u artırın
var apiClient = new PdfPolicyApiClient
{
    Timeout = TimeSpan.FromMinutes(10)
};
```

### 3. Retry Mekanizması
```csharp
public async Task<PolicyExtractionResult> ExtractWithRetry(
    string pdfPath,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await _apiClient.ExtractPolicyAsync(pdfPath);
        }
        catch (HttpRequestException) when (i < maxRetries - 1)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i))); // Exponential backoff
        }
    }
    throw new Exception("Retry limit exceeded");
}
```

### 4. Paralel İşlem için Asenkron Kullanın
```csharp
// ❌ YANLIŞ - Senkron batch (100 dosya = 50 saniye)
var result = await apiClient.ExtractBatchAsync(files); // Client bekler

// ✓ DOĞRU - Asenkron batch (100 dosya = hemen döner, background'da işlenir)
var job = await apiClient.ExtractBatchAsyncWithJob(files); // Hemen döner
var result = await apiClient.WaitForJobCompletionAsync(job.JobId); // İsterseniz bekleyin
```

### 5. Progress Tracking
```csharp
// Progress bar güncelleme
await apiClient.WaitForJobCompletionAsync(jobId, progress =>
{
    // UI thread'de çalıştır
    Application.Current.Dispatcher.Invoke(() =>
    {
        ProgressBar.Value = progress.PercentageComplete;
        StatusLabel.Text = $"{progress.ProcessedFiles}/{progress.TotalFiles}";
    });
});
```

### 6. Veritabanı Entegrasyonu
```csharp
var result = await apiClient.ExtractPolicyAsync(pdfPath);

if (result.Success)
{
    // Entity Framework ile kaydet
    var policy = new Policy
    {
        Company = result.Data.Company,
        PolicyNumber = result.Data.PolicyNumber,
        GrossPremium = result.Data.GrossPremium,
        PlateNumber = result.Data.PlateNumber,
        ProcessedDate = DateTime.Now,
        PdfFileName = result.FileName
    };

    dbContext.Policies.Add(policy);
    await dbContext.SaveChangesAsync();
}
```

---

## Sık Sorulan Sorular (FAQ)

### Q: Maksimum dosya boyutu nedir?
**A:** 50MB per PDF file.

### Q: Aynı anda kaç dosya gönderebilirim?
**A:** Maksimum 100 dosya per batch request.

### Q: Asenkron batch işlem ne kadar sürer?
**A:** ~500ms per PDF. 100 dosya için yaklaşık 50 saniye.

### Q: Job sonuçları ne kadar saklanır?
**A:** 7 gün (Redis'te).

### Q: Hangfire dashboard'a kimler erişebilir?
**A:** Şu an herkes. Production'da authentication eklenecek.

### Q: API rate limit var mı?
**A:** Şu an yok, ileride eklenebilir.

---

## Destek ve İletişim

- **Swagger UI**: https://aivoice.sigorta.teklifi.al/swagger
- **Hangfire Dashboard**: https://aivoice.sigorta.teklifi.al/hangfire
- **GitHub**: https://github.com/ofSener/pdf-reader

---

## Versiyon Notları

### v1.0.0 (2025-10-16)
- ✅ İlk production release
- ✅ 21+ sigorta şirketi desteği
- ✅ Asenkron batch processing
- ✅ Hangfire job monitoring
- ✅ %97+ doğruluk oranı
