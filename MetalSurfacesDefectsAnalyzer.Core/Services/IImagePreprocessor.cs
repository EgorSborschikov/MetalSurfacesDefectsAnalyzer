using MetalSurfacesDefectsAnalyzer.Core.Structs;

namespace MetalSurfacesDefectsAnalyzer.Core.Services
{
    public interface IImagePreprocessor
    {
        // Загрузка и предобработка изображения
        Task<DefectImage> ProcessAsync(string imagePath);

        // Пакетная предобработка
        Task<IReadOnlyList<DefectImage>> ProcessBatchAsync(
            IEnumerable<string> imagePaths,
            IProgress<ProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }

    public class ProcessingProgress
    {
        public int Processed { get; init; }
        public int Total { get; init; }
        public string? CurrentFile { get; init; }
        public double Percentage => Total > 0 ? (double)Processed / Total * 100 : 0;
    }
}
