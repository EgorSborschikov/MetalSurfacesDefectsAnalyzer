namespace MetalSurfacesDefectsAnalyzer.Core.Config
{
    public class AppConfig
    {
        public double DefaultEta { get; set; } = 0.04;
        public int FeatureVectorSize { get; set; } = 512;
        public int LatentVectorSize { get; set; } = 32;
        public int NumClusters { get; set; } = 12;
        public int ImageSize { get; set; } = 224;
        public float[] NormalizeMean { get; set; } = { 0.485f, 0.456f, 0.406f };
        public float[] NormalizeStd { get; set; } = { 0.229f, 0.224f, 0.225f };
        public string[] SupportedFormats { get; set; } = { ".png", ".jpg", ".jpeg" };

        public string ResNetModelPath { get; set; } = "resnet18.onnx";
        public string GetVaDEModelPath(double eta) => $"model_eta_{eta:F2}.onnx".Replace(".", "_");

        public static readonly string[] ClassNames = {
            "crease", "crescent_gap", "inclusion", "oil_spot",
            "punching_hole", "rolled_in_scale", "rolled_pit",
            "scratches", "silk_spot", "waist_folding",
            "water_spot", "welding_line"
        };

        public static readonly Dictionary<string, string> ClassNamesRu = new()
        {
            {"crease", "Складка"},
            {"crescent_gap", "Серповидный зазор"},
            {"inclusion", "Включение"},
            {"oil_spot", "Масляное пятно"},
            {"punching_hole", "Пробойное отверстие"},
            {"rolled_in_scale", "Вкатанная окалина"},
            {"rolled_pit", "Прокатная яма"},
            {"scratches", "Царапины"},
            {"silk_spot", "Шелковое пятно"},
            {"waist_folding", "Поясная складка"},
            {"water_spot", "Водяное пятно"},
            {"welding_line", "Сварочный шов"}
        };
    }
}
