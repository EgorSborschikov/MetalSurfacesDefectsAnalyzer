using MetalSurfacesDefectsAnalyzer.Core.Models;
using MetalSurfacesDefectsAnalyzer.Core.Structs;

namespace MetalSurfacesDefectsAnalyzer.Core.Services
{
    public interface IInferenceService : IDisposable
    {
        IReadOnlyList<double> AvailableEtas { get; }
        bool IsModelLoaded { get; }
        double CurrentEta { get; }

        Task LoadModelAsync(double eta, CancellationToken cancellationToken = default);
        Task<ClusterResult> InferAsync(DefectImage image, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClusterResult>> InferBatchAsync(
            IReadOnlyList<DefectImage> images,
            IProgress<InferenceProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
