namespace Pdf_Reader.Models;

/// <summary>
/// Batch job oluşturma response'u
/// </summary>
public class BatchJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int TotalFiles { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime EstimatedCompletionTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Batch job status response'u
/// </summary>
public class BatchJobStatusResponse
{
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Job durumu: queued, processing, completed, failed
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Progress bilgisi (işlem devam ediyorsa)
    /// </summary>
    public BatchProgressInfo? Progress { get; set; }

    /// <summary>
    /// Sonuç (işlem tamamlandıysa)
    /// </summary>
    public BatchExtractionResult? Result { get; set; }

    /// <summary>
    /// Hata mesajı (işlem başarısız olduysa)
    /// </summary>
    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Batch processing progress bilgisi
/// </summary>
public class BatchProgressInfo
{
    public string JobId { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public int PercentageComplete { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Batch extraction result
/// </summary>
public class BatchExtractionResult
{
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ExtractionResult> Results { get; set; } = new();
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
