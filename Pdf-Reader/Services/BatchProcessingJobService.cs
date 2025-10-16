using Pdf_Reader.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace Pdf_Reader.Services;

/// <summary>
/// Background batch processing için job servis
/// </summary>
public class BatchProcessingJobService
{
    private readonly ILogger<BatchProcessingJobService> _logger;
    private readonly PdfTextExtractorService _pdfExtractor;
    private readonly ExtractorOrchestrator _orchestrator;
    private readonly ValidationService _validator;
    private readonly IConnectionMultiplexer? _redis;

    public BatchProcessingJobService(
        ILogger<BatchProcessingJobService> logger,
        PdfTextExtractorService pdfExtractor,
        ExtractorOrchestrator orchestrator,
        ValidationService validator,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _pdfExtractor = pdfExtractor;
        _orchestrator = orchestrator;
        _validator = validator;
        _redis = redis;
    }

    /// <summary>
    /// Background job olarak çalışacak batch processing metodu
    /// </summary>
    public async Task<BatchExtractionResult> ProcessBatchAsync(
        string jobId,
        List<(string fileName, byte[] fileBytes)> files)
    {
        _logger.LogInformation($"[Job {jobId}] Batch processing başladı: {files.Count} dosya");

        // Redis database (opsiyonel - development'ta null olabilir)
        var db = _redis?.GetDatabase();

        // Job durumunu Redis'e kaydet (opsiyonel)
        if (db != null)
        {
            await db.StringSetAsync($"job:{jobId}:status", "processing");
            await db.StringSetAsync($"job:{jobId}:started", DateTime.UtcNow.ToString("O"));
        }

        var result = new BatchExtractionResult
        {
            TotalFiles = files.Count
        };

        var processedCount = 0;
        var maxParallelism = Environment.ProcessorCount;

        // Paralel processing
        var tasks = files.Select(async (file, index) =>
        {
            try
            {
                var (fileName, fileBytes) = file;

                // 1. PDF'den text çıkar
                using var stream = new MemoryStream(fileBytes);
                var (text, pageCount) = await _pdfExtractor.ExtractTextAsync(stream);

                if (string.IsNullOrWhiteSpace(text))
                {
                    return new ExtractionResult
                    {
                        FileName = fileName,
                        Success = false,
                        Errors = new List<string> { "PDF'den text çıkarılamadı" }
                    };
                }

                // 2. Verileri çıkar
                var policyData = await _orchestrator.ExtractPolicyDataAsync(text);

                // 3. Validate et
                var validationResult = _validator.Validate(policyData);

                // 4. Result oluştur
                var extractionResult = new ExtractionResult
                {
                    FileName = fileName,
                    Data = policyData,
                    Success = validationResult.IsValid && policyData.ConfidenceScore >= 0.5
                };

                extractionResult.Errors.AddRange(validationResult.Errors);
                extractionResult.Warnings.AddRange(validationResult.Warnings);

                // Progress bildir
                Interlocked.Increment(ref processedCount);
                var progressInfo = new BatchProgressInfo
                {
                    JobId = jobId,
                    TotalFiles = files.Count,
                    ProcessedFiles = processedCount,
                    CurrentFile = fileName,
                    PercentageComplete = (int)((processedCount / (double)files.Count) * 100)
                };

                // Redis'e progress yaz (opsiyonel)
                if (db != null)
                {
                    try
                    {
                        await db.StringSetAsync(
                            $"job:{jobId}:progress",
                            JsonSerializer.Serialize(progressInfo),
                            TimeSpan.FromDays(7));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"[Job {jobId}] Progress Redis'e yazılamadı");
                    }
                }

                _logger.LogDebug($"[Job {jobId}] Dosya işlendi ({processedCount}/{files.Count}): {fileName}");

                return extractionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Job {jobId}] PDF işleme hatası: {file.fileName}");
                return new ExtractionResult
                {
                    FileName = file.fileName,
                    Success = false,
                    Errors = new List<string> { $"İşlem hatası: {ex.Message}" }
                };
            }
        });

        // Tüm dosyaları işle
        var results = await Task.WhenAll(tasks);

        // Sonuçları topla
        foreach (var extractionResult in results)
        {
            result.Results.Add(extractionResult);
            if (extractionResult.Success)
                result.SuccessCount++;
            else
                result.FailureCount++;
        }

        _logger.LogInformation(
            $"[Job {jobId}] Batch processing tamamlandı: " +
            $"{result.SuccessCount} başarılı, {result.FailureCount} başarısız");

        // Job durumunu ve sonucu Redis'e kaydet (opsiyonel)
        if (db != null)
        {
            try
            {
                result.CompletedAt = DateTime.UtcNow;

                await db.StringSetAsync($"job:{jobId}:status", "completed");
                await db.StringSetAsync($"job:{jobId}:completed", DateTime.UtcNow.ToString("O"));
                await db.StringSetAsync(
                    $"job:{jobId}:result",
                    JsonSerializer.Serialize(result),
                    TimeSpan.FromDays(7)); // Sonuçlar 7 gün sakla
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[Job {jobId}] Sonuç Redis'e yazılamadı");
            }
        }

        return result;
    }
}
