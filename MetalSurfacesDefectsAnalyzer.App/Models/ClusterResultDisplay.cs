using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MetalSurfacesDefectsAnalyzer.App.Models
{
    public class ClusterResultDisplay : INotifyPropertyChanged
    {
        private bool _isHighlighted;

        public int Index { get; init; }
        public string FileName { get; init; } = string.Empty;
        public int PrimaryCluster { get; init; }
        public string PrimaryClusterDisplay { get; init; } = string.Empty;
        public float Confidence { get; init; }
        public float Uncertainty { get; init; }
        public SolidColorBrush ClusterColor { get; init; } = new(Colors.Gray);
        public SolidColorBrush ConfidenceColor { get; init; } = new(Colors.Gray);
        public SolidColorBrush UncertaintyColor { get; init; } = new(Colors.Gray);
        public float[] ClusterProbabilities { get; init; } = Array.Empty<float>();

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
