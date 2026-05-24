using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Core.Structs
{
    public class ClusterPrototype
    {
        public int ClusterId { get; init; }
        public int ImageCount { get; init; }
        public byte[]? PrototypeImageData { get; set; }
        public ClusterCharacteristics Characteristics { get; init; } = new();
    }

    public class ClusterCharacteristics
    {
        public float AverageConfidence { get; init; }
        public float AverageEntropy { get; init; }
        public float Compactness { get; init; }
        public int Size { get; init; }
        public float HighConfidenceRatio { get; init; }
    }
}
