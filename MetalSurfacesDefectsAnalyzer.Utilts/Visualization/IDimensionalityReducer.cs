using MetalSurfacesDefectsAnalyzer.Core.Structs;
using MetalSurfacesDefectsAnalyzer.Utilts.Models;

namespace MetalSurfacesDefectsAnalyzer.Utilts.Visualization
{
    public interface IDimensionalityReducer
    {
        Task<DimensionalityReductionResult> ReduceAsync(
            AnalysisResult analysisResult,
            string method = "tsne",
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
