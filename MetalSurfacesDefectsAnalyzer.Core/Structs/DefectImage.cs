namespace MetalSurfacesDefectsAnalyzer.Core.Structs
{
    public class DefectImage
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public float[]? FeatureVector { get; set; }
        public bool IsProcessed { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
