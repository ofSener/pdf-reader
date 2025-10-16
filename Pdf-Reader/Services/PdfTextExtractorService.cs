using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace Pdf_Reader.Services;

/// <summary>
/// PDF'den text çıkarma servisi (iText7)
/// </summary>
public class PdfTextExtractorService
{
    private readonly ILogger<PdfTextExtractorService> _logger;

    public PdfTextExtractorService(ILogger<PdfTextExtractorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// PDF dosyasından tüm text'i çıkarır
    /// </summary>
    public async Task<(string text, int pageCount)> ExtractTextAsync(Stream pdfStream)
    {
        try
        {
            // For encrypted PDFs with restrictions, try with owner password (empty or common passwords)
            var readerProperties = new iText.Kernel.Pdf.ReaderProperties();

            // Try to set owner password - empty password often works for restriction-only PDFs
            try
            {
                readerProperties.SetPassword(System.Text.Encoding.UTF8.GetBytes(""));
            }
            catch
            {
                // If setting password fails, continue without it
            }

            using var pdfReader = new PdfReader(pdfStream, readerProperties);
            using var pdfDocument = new PdfDocument(pdfReader);

            var textBuilder = new System.Text.StringBuilder();
            int pageCount = pdfDocument.GetNumberOfPages();

            _logger.LogInformation("PDF extraction started. Total pages: {PageCount}", pageCount);

            for (int i = 1; i <= pageCount; i++)
            {
                var page = pdfDocument.GetPage(i);
                var strategy = new LocationTextExtractionStrategy();
                string pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

                textBuilder.AppendLine($"--- PAGE {i} ---");
                textBuilder.AppendLine(pageText);
                textBuilder.AppendLine();
            }

            string fullText = textBuilder.ToString();
            _logger.LogInformation("PDF extraction completed. Extracted {CharCount} characters", fullText.Length);

            return (fullText, pageCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF");
            throw new InvalidOperationException("PDF text extraction failed", ex);
        }
    }

    /// <summary>
    /// Dosya path'inden text çıkarır
    /// </summary>
    public async Task<(string text, int pageCount)> ExtractTextFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF file not found: {filePath}");

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return await ExtractTextAsync(fileStream);
    }

    /// <summary>
    /// IFormFile'dan text çıkarır (API upload için)
    /// </summary>
    public async Task<(string text, int pageCount)> ExtractTextFromFormFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Invalid file");

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only PDF files are supported");

        using var stream = file.OpenReadStream();
        return await ExtractTextAsync(stream);
    }
}
