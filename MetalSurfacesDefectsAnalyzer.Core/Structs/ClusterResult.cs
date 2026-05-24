namespace MetalSurfacesDefectsAnalyzer.Core.Structs
{
    public class ClusterResult
    {
        public string FileName { get; set; } = string.Empty;
        public int PrimaryCluster { get; set; }
        public float[] ClusterProbabilities { get; set; } = Array.Empty<float>();
        public float Uncertainty { get; set; }
        public float Confidence { get; set; }
        public float[]? LatentVector { get; set; }
    }
}
