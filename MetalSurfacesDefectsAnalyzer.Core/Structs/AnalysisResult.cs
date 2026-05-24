namespace MetalSurfacesDefectsAnalyzer.Core.Structs
{
    public class AnalysisResult
    {
        public string ModelId { get; set; } = string.Empty;
        public double EtaParameter { get; set; }
        public DateTime AnalysisTime { get; set; }
        public int TotalImages { get; set; }
        public int ProcessedImages { get; set; }
        public List<ClusterResult> Results { get; set; } = new();
        public float[][] LatentVectors { get; init; } = Array.Empty<float[]>();
    }
}
