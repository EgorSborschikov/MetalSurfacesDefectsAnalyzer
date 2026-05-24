namespace MetalSurfacesDefectsAnalyzer.Core.Models
{
    public class InferenceProgress
    {
        public int Processed { get; init; }
        public int Total { get; init; }
        public string? CurrentFile { get; init; }
    }
}
