using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Utilts.Models
{
    public class DimensionalityReductionResult
    {
        public string Method { get; init; } = string.Empty;
        public double[][] Points2D { get; init; } = Array.Empty<double[]>();
        public int[] ClusterAssignments { get; init; } = Array.Empty<int>();
        public string[] FileNames { get; init; } = Array.Empty<string>();
        public float[] Confidences { get; init; } = Array.Empty<float>();
        public Dictionary<int, double[]> ClusterCentroids2D { get; init; } = new();
        public TimeSpan ComputationTime { get; init; }
    }
}
