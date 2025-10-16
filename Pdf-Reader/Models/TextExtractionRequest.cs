namespace Pdf_Reader.Models;

/// <summary>
/// PDF'ten önceden çıkarılmış text ile poliçe verisi çıkarma request'i
/// </summary>
public class TextExtractionRequest
{
    /// <summary>
    /// PDF'ten çıkarılmış text içeriği (gerekli)
    /// </summary>
    public string PdfText { get; set; } = string.Empty;

    /// <summary>
    /// Dosya adı (opsiyonel, tracking için)
    /// </summary>
    public string? FileName { get; set; }
}
