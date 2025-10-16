namespace Pdf_Reader.Models;

/// <summary>
/// PDF extraction sonucu
/// </summary>
public class ExtractionResult
{
    public bool Success { get; set; }
    public PolicyData? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public int ProcessingTimeMs { get; set; }
    public int PageCount { get; set; }
    public string? RawText { get; set; } // Debug için
}
