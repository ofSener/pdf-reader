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
    /// [DEVRE DIŞI] Tek bir PDF dosyasından poliçe verilerini çıkarır
    /// Bu endpoint kapatılmıştır. Lütfen /extract-from-text kullanın.
    /// </summary>
    [Obsolete("Bu endpoint kapatılmıştır. /extract-from-text kullanın.")]
    [HttpPost("extract")]
    [ProducesResponseType(typeof(object), StatusCodes.Status410Gone)]
    public ActionResult ExtractPolicy(IFormFile file)
    {
        _logger.LogWarning("PDF upload endpoint'i devre dışı - /extract-from-text kullanılmalı");
        return StatusCode(410, new
        {
            error = "Bu endpoint kapatılmıştır",
            message = "PDF upload artık desteklenmiyor. Lütfen PDF'inizi önce text'e çevirip /extract-from-text endpoint'ini kullanın.",
            alternative = "/api/Policy/extract-from-text",
            documentation = "https://aivoice.sigorta.teklifi.al/swagger"
        });
    }

    /// <summary>
    /// [DEVRE DIŞI] Birden fazla PDF dosyasından poliçe verilerini çıkarır
    /// Bu endpoint kapatılmıştır. Lütfen /extract-from-text kullanın.
    /// </summary>
    [Obsolete("Bu endpoint kapatılmıştır. /extract-from-text kullanın.")]
    [HttpPost("extract-batch")]
    [ActionName("ExtractBatchSync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status410Gone)]
    public ActionResult ExtractPolicyBatch(List<IFormFile> files)
    {
        _logger.LogWarning("PDF batch upload endpoint'i devre dışı - /extract-from-text kullanılmalı");
        return StatusCode(410, new
        {
            error = "Bu endpoint kapatılmıştır",
            message = "PDF batch upload artık desteklenmiyor. Lütfen her PDF'i text'e çevirip /extract-from-text ile tek tek gönderin.",
            alternative = "/api/Policy/extract-from-text",
            documentation = "https://aivoice.sigorta.teklifi.al/swagger"
        });
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
    /// Direkt PDF text'inden poliçe verilerini çıkarır (PDF upload gerektirmez)
    /// </summary>
    [HttpPost("extract-from-text")]
    [ProducesResponseType(typeof(ExtractionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExtractionResult>> ExtractFromText([FromBody] TextExtractionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExtractionResult
        {
            FileName = request.FileName ?? "text-input"
        };

        try
        {
            if (string.IsNullOrWhiteSpace(request.PdfText))
            {
                result.Success = false;
                result.Errors.Add("PDF metni boş olamaz");
                return BadRequest(result);
            }

            _logger.LogInformation($"Text extraction başlıyor: {request.FileName ?? "unknown"} ({request.PdfText.Length} karakter)");

            // Text'ten verileri çıkar
            var policyData = await _orchestrator.ExtractPolicyDataAsync(request.PdfText);
            result.Data = policyData;

            // Validate et
            var validationResult = _validator.Validate(policyData);
            result.Errors.AddRange(validationResult.Errors);
            result.Warnings.AddRange(validationResult.Warnings);
            // Not: policyData.Warnings artık yok - tüm warnings validation'dan geliyor

            // Success durumunu belirle
            result.Success = validationResult.IsValid && policyData.ConfidenceScore >= 0.5;

            stopwatch.Stop();
            result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogInformation($"Text extraction tamamlandı: Success: {result.Success}, Confidence: {policyData.ConfidenceScore:P2}, Time: {result.ProcessingTimeMs}ms");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text extraction hatası");

            stopwatch.Stop();
            result.Success = false;
            result.Errors.Add($"İşlem hatası: {ex.Message}");
            result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

            return StatusCode(500, result);
        }
    }

    /// <summary>
    /// [DEVRE DIŞI] Debug: PDF'den sadece raw text çıkarır
    /// Bu endpoint kapatılmıştır.
    /// </summary>
    [Obsolete("Bu endpoint kapatılmıştır.")]
    [HttpPost("debug/extract-text")]
    public ActionResult ExtractTextOnly(IFormFile file)
    {
        _logger.LogWarning("Debug PDF text extract endpoint'i devre dışı");
        return StatusCode(410, new
        {
            error = "Bu endpoint kapatılmıştır",
            message = "PDF upload artık desteklenmiyor."
        });
    }

    /// <summary>
    /// [DEVRE DIŞI] Batch işlemi için job oluşturur
    /// Bu endpoint kapatılmıştır. Lütfen /extract-from-text kullanın.
    /// </summary>
    [Obsolete("Bu endpoint kapatılmıştır. /extract-from-text kullanın.")]
    [HttpPost("extract-batch-async")]
    [ActionName("ExtractBatchAsync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status410Gone)]
    public ActionResult ExtractPolicyBatchAsync(List<IFormFile> files)
    {
        _logger.LogWarning("PDF batch async upload endpoint'i devre dışı - /extract-from-text kullanılmalı");
        return StatusCode(410, new
        {
            error = "Bu endpoint kapatılmıştır",
            message = "PDF batch async upload artık desteklenmiyor. Lütfen her PDF'i text'e çevirip /extract-from-text ile gönderin.",
            alternative = "/api/Policy/extract-from-text",
            documentation = "https://aivoice.sigorta.teklifi.al/swagger"
        });
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
