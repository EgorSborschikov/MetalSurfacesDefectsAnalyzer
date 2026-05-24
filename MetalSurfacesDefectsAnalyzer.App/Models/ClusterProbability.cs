using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MetalSurfacesDefectsAnalyzer.App.Models
{
    public class ClusterProbabilityBar
    {
        public int ClusterId { get; init; }
        public float Probability { get; init; }
        public SolidColorBrush BarColor { get; init; } = new(Colors.Gray);
    }
}
