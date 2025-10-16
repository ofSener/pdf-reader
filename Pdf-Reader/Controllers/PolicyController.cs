using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Pdf_Reader.Models;
using Pdf_Reader.Services;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace Pdf_Reader.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PolicyController : ControllerBase
{
    private readonly ILogger<PolicyController> _logger;
    private readonly PdfTextExtractorService _pdfExtractor;
    private readonly ExtractorOrchestrator _orchestrator;
    private readonly ValidationService _validator;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IConnectionMultiplexer? _redis;

    public PolicyController(
        ILogger<PolicyController> logger,
        PdfTextExtractorService pdfExtractor,
        ExtractorOrchestrator orchestrator,
        ValidationService validator,
        IBackgroundJobClient backgroundJobs,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _pdfExtractor = pdfExtractor;
        _orchestrator = orchestrator;
        _validator = validator;
        _backgroundJobs = backgroundJobs;
        _redis = redis;
    }

    /// <summary>
    /// Tek bir PDF dosyasından poliçe verilerini çıkarır
    /// </summary>
    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ExtractionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtractionResult>> ExtractPolicy([FromForm] IFormFile file)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExtractionResult
        {
            FileName = file.FileName
        };

        try
        {
            // Dosya kontrolü
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Geçersiz dosya yüklendi");
                result.Success = false;
                result.Errors.Add("Geçersiz veya boş dosya");
                return BadRequest(result);
            }

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning($"PDF olmayan dosya yüklendi: {file.FileName}");
                result.Success = false;
                result.Errors.Add("Sadece PDF dosyaları kabul edilir");
                return BadRequest(result);
            }

            // Dosya boyutu kontrolü (max 50MB)
            if (file.Length > 50 * 1024 * 1024)
            {
                _logger.LogWarning($"Çok büyük dosya: {file.Length} bytes");
                result.Success = false;
                result.Errors.Add("Dosya boyutu 50MB'dan küçük olmalı");
                return BadRequest(result);
            }

            _logger.LogInformation($"PDF extraction başlıyor: {file.FileName} ({file.Length} bytes)");

            // 1. PDF'den text çıkar
            string pdfText;
            using (var stream = file.OpenReadStream())
            {
                var (text, pageCount) = await _pdfExtractor.ExtractTextAsync(stream);
                pdfText = text;
                _logger.LogInformation($"PDF text extraction tamamlandı: {pageCount} sayfa");
            }

            if (string.IsNullOrWhiteSpace(pdfText))
            {
                result.Success = false;
                result.Errors.Add("PDF'den text çıkarılamadı. Dosya şifreli veya OCR gerektirebilir.");
                stopwatch.Stop();
                result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
                return Ok(result);
            }

            // 2. Verileri çıkar
            var policyData = await _orchestrator.ExtractPolicyDataAsync(pdfText);
            result.Data = policyData;

            // 3. Validate et
            var validationResult = _validator.Validate(policyData);
            result.Errors.AddRange(validationResult.Errors);
            result.Warnings.AddRange(validationResult.Warnings);
            result.Warnings.AddRange(policyData.Warnings);

            // 4. Success durumunu belirle
            result.Success = validationResult.IsValid && policyData.ConfidenceScore >= 0.5;

            if (!validationResult.IsValid)
            {
                _logger.LogWarning($"Validation başarısız: {file.FileName}");
            }

            stopwatch.Stop();
            result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogInformation($"Extraction tamamlandı: {file.FileName} - Success: {result.Success}, Confidence: {policyData.ConfidenceScore:P2}, Time: {result.ProcessingTimeMs}ms");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Extraction hatası: {file?.FileName}");

            stopwatch.Stop();
            result.Success = false;
            result.Errors.Add($"İşlem hatası: {ex.Message}");
            result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

            return StatusCode(500, result);
        }
    }

    /// <summary>
    /// Birden fazla PDF dosyasından poliçe verilerini çıkarır (Paralel işleme)
    /// NOT: Bu endpoint senkron çalışır, client işlem bitene kadar bekler.
    /// Asenkron batch processing için /extract-batch-async kullanın.
    /// </summary>
    [HttpPost("extract-batch")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BatchExtractionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchExtractionResult>> ExtractPolicyBatch([FromForm] List<IFormFile> files)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var batchResult = new BatchExtractionResult
        {
            TotalFiles = files?.Count ?? 0
        };

        try
        {
            if (files == null || files.Count == 0)
            {
                _logger.LogWarning("Batch request'te dosya yok");
                return BadRequest("En az bir dosya yüklenmelidir");
            }

            if (files.Count > 100)
            {
                _logger.LogWarning($"Çok fazla dosya: {files.Count}");
                return BadRequest("Maksimum 100 dosya aynı anda işlenebilir");
            }

            _logger.LogInformation($"Batch extraction başlıyor: {files.Count} dosya (Paralel mode)");

            // PARALEL PDF TEXT EXTRACTION
            // MaxDegreeOfParallelism: CPU core sayısına göre otomatik ayarlanır
            // IIS'te genelde 4-8 arası ideal
            var maxParallelism = Environment.ProcessorCount; // Otomatik CPU core sayısı
            _logger.LogDebug($"Paralel işleme thread sayısı: {maxParallelism}");

            var extractionTasks = files.Select(async file =>
            {
                var fileStopwatch = Stopwatch.StartNew();

                try
                {
                    if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning($"PDF olmayan dosya atlandı: {file.FileName}");
                        return new ExtractionResult
                        {
                            FileName = file.FileName,
                            Success = false,
                            Errors = new List<string> { "Sadece PDF dosyaları kabul edilir" }
                        };
                    }

                    // 1. PDF'den text çıkar
                    string pdfText;
                    int pageCount;

                    using (var stream = file.OpenReadStream())
                    {
                        var (text, pages) = await _pdfExtractor.ExtractTextAsync(stream);
                        pdfText = text;
                        pageCount = pages;
                    }

                    if (string.IsNullOrWhiteSpace(pdfText))
                    {
                        return new ExtractionResult
                        {
                            FileName = file.FileName,
                            Success = false,
                            Errors = new List<string> { "PDF'den text çıkarılamadı" },
                            ProcessingTimeMs = (int)fileStopwatch.ElapsedMilliseconds
                        };
                    }

                    // 2. Verileri çıkar
                    var policyData = await _orchestrator.ExtractPolicyDataAsync(pdfText);

                    // 3. Validate et
                    var validationResult = _validator.Validate(policyData);

                    // 4. Result oluştur
                    var extractionResult = new ExtractionResult
                    {
                        FileName = file.FileName,
                        Data = policyData,
                        Success = validationResult.IsValid && policyData.ConfidenceScore >= 0.5,
                        ProcessingTimeMs = (int)fileStopwatch.ElapsedMilliseconds
                    };

                    extractionResult.Errors.AddRange(validationResult.Errors);
                    extractionResult.Warnings.AddRange(validationResult.Warnings);
                    extractionResult.Warnings.AddRange(policyData.Warnings);

                    _logger.LogDebug($"Dosya işlendi: {file.FileName} - {extractionResult.ProcessingTimeMs}ms");

                    return extractionResult;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"PDF işleme hatası: {file.FileName}");
                    return new ExtractionResult
                    {
                        FileName = file.FileName,
                        Success = false,
                        Errors = new List<string> { $"PDF işleme hatası: {ex.Message}" },
                        ProcessingTimeMs = (int)fileStopwatch.ElapsedMilliseconds
                    };
                }
            });

            // PARALEL EXECUTION - Task.WhenAll ile tüm dosyalar aynı anda işlenir
            // IIS Thread Pool otomatik olarak load balancing yapar
            var results = await Task.WhenAll(extractionTasks);

            // Sonuçları topla
            foreach (var result in results)
            {
                batchResult.Results.Add(result);

                if (result.Success)
                    batchResult.SuccessCount++;
                else
                    batchResult.FailureCount++;
            }

            overallStopwatch.Stop();
            var avgTimePerFile = results.Length > 0 ? results.Average(r => r.ProcessingTimeMs) : 0;

            _logger.LogInformation(
                $"Batch extraction tamamlandı: {batchResult.TotalFiles} dosya, " +
                $"{batchResult.SuccessCount} başarılı, {batchResult.FailureCount} başarısız, " +
                $"Toplam süre: {overallStopwatch.ElapsedMilliseconds}ms, " +
                $"Ortalama: {avgTimePerFile:F0}ms/dosya, " +
                $"Paralel thread: {maxParallelism}");

            return Ok(batchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch extraction hatası");
            overallStopwatch.Stop();
            return StatusCode(500, $"Batch işlem hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// API health check
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            service = "PDF Policy Extractor API"
        });
    }

    /// <summary>
    /// Desteklenen şirketler listesi
    /// </summary>
    [HttpGet("companies")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetSupportedCompanies()
    {
        var companies = Enum.GetNames(typeof(CompanyType))
            .Where(c => c != "Unknown")
            .OrderBy(c => c)
            .ToList();

        return Ok(companies);
    }

    /// <summary>
    /// Desteklenen poliçe tipleri listesi
    /// </summary>
    [HttpGet("policy-types")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetSupportedPolicyTypes()
    {
        var policyTypes = Enum.GetNames(typeof(PolicyType))
            .Where(p => p != "Unknown")
            .OrderBy(p => p)
            .ToList();

        return Ok(policyTypes);
    }

    /// <summary>
    /// Debug: PDF'den sadece raw text çıkarır
    /// </summary>
    [HttpPost("debug/extract-text")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> ExtractTextOnly([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("Dosya yok");

            string pdfText;
            int pageCount;

            using (var stream = file.OpenReadStream())
            {
                var (text, pages) = await _pdfExtractor.ExtractTextAsync(stream);
                pdfText = text;
                pageCount = pages;
            }

            return Ok(new
            {
                fileName = file.FileName,
                pageCount = pageCount,
                textLength = pdfText.Length,
                text = pdfText
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Batch işlemi için job oluşturur (asenkron - hemen döner)
    /// </summary>
    [HttpPost("extract-batch-async")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BatchJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

            // Dosyaları byte array'e çevir (Hangfire serialize için gerekli)
            var fileData = new List<(string fileName, byte[] fileBytes)>();
            foreach (var file in files)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                fileData.Add((file.FileName, ms.ToArray()));
            }

            // Redis'e job metadata kaydet (opsiyonel)
            if (_redis != null)
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync(
                    $"job:{jobId}:status",
                    "queued",
                    TimeSpan.FromDays(7));
                await db.StringSetAsync(
                    $"job:{jobId}:created",
                    DateTime.UtcNow.ToString("O"),
                    TimeSpan.FromDays(7));
            }

            // Hangfire job'ı kuyruğa ekle
            var hangfireJobId = _backgroundJobs.Enqueue<BatchProcessingJobService>(
                service => service.ProcessBatchAsync(jobId, fileData));

            _logger.LogInformation($"Batch job oluşturuldu: {jobId} (Hangfire: {hangfireJobId}), {files.Count} dosya");

            // Client'a hemen yanıt dön
            return Ok(new BatchJobResponse
            {
                JobId = jobId,
                Status = "queued",
                TotalFiles = files.Count,
                Message = $"İşlem kuyruğa eklendi. Sonuçları GET /api/policy/job/{jobId} endpoint'inden takip edebilirsiniz.",
                EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(files.Count * 0.5), // ~500ms/dosya
                CreatedAt = DateTime.UtcNow
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
    [ProducesResponseType(typeof(BatchJobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchJobStatusResponse>> GetJobStatus(string jobId)
    {
        try
        {
            if (_redis == null)
            {
                return BadRequest("Redis bağlantısı yok. Bu endpoint sadece production'da kullanılabilir.");
            }

            var db = _redis.GetDatabase();

            // Redis'ten job bilgisini al
            var statusKey = $"job:{jobId}:status";
            var resultKey = $"job:{jobId}:result";
            var progressKey = $"job:{jobId}:progress";
            var errorKey = $"job:{jobId}:error";
            var startedAtKey = $"job:{jobId}:started";
            var completedAtKey = $"job:{jobId}:completed";

            var status = await db.StringGetAsync(statusKey);
            if (status.IsNullOrEmpty)
                return NotFound($"Job bulunamadı: {jobId}");

            var response = new BatchJobStatusResponse
            {
                JobId = jobId,
                Status = status.ToString()
            };

            // Tarih bilgilerini al
            var startedAtStr = await db.StringGetAsync(startedAtKey);
            if (!startedAtStr.IsNullOrEmpty && DateTime.TryParse(startedAtStr, out var startedAt))
            {
                response.StartedAt = startedAt;
            }

            var completedAtStr = await db.StringGetAsync(completedAtKey);
            if (!completedAtStr.IsNullOrEmpty && DateTime.TryParse(completedAtStr, out var completedAt))
            {
                response.CompletedAt = completedAt;
            }

            // Progress bilgisini getir
            var progressJson = await db.StringGetAsync(progressKey);
            if (!progressJson.IsNullOrEmpty)
            {
                response.Progress = JsonSerializer.Deserialize<BatchProgressInfo>(progressJson!);
            }

            // Eğer tamamlandıysa sonucu getir
            if (status == "completed")
            {
                var resultJson = await db.StringGetAsync(resultKey);
                if (!resultJson.IsNullOrEmpty)
                {
                    response.Result = JsonSerializer.Deserialize<BatchExtractionResult>(resultJson!);
                }
            }

            // Eğer başarısız olduysa hata mesajını getir
            if (status == "failed")
            {
                var errorMsg = await db.StringGetAsync(errorKey);
                if (!errorMsg.IsNullOrEmpty)
                {
                    response.ErrorMessage = errorMsg.ToString();
                }
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Job status getirme hatası: {jobId}");
            return StatusCode(500, $"Status getirme hatası: {ex.Message}");
        }
    }
}
