namespace Pdf_Reader.Services.Extractors;

/// <summary>
/// Field extraction için base interface
/// </summary>
/// <typeparam name="T">Çıkarılacak veri tipi</typeparam>
public interface IFieldExtractor<T> where T : struct
{
    /// <summary>
    /// PDF text'inden belirli bir alanı çıkarır
    /// </summary>
    /// <param name="text">PDF text</param>
    /// <param name="confidence">Güven skoru (0.0 - 1.0)</param>
    /// <returns>Çıkarılan değer veya null</returns>
    T? Extract(string text, out double confidence);

    /// <summary>
    /// Extractor'ın adı
    /// </summary>
    string ExtractorName { get; }
}

/// <summary>
/// String extraction için interface
/// </summary>
public interface IFieldExtractor
{
    string? Extract(string text, out double confidence);
    string ExtractorName { get; }
}
