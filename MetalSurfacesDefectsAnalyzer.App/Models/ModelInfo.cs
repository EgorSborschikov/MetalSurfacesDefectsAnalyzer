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
    public class ModelInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        public double Eta { get; init; }
        public double NMI { get; init; }
        public double ARI { get; init; }
        public double Entropy { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public Brush TypeColor { get; init; } = new SolidColorBrush(Colors.Gray);

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
