using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.App.Models
{
    public class ImageResultDisplay : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string FileName { get; init; } = string.Empty;
        public System.Windows.Media.Imaging.BitmapSource? Thumbnail { get; init; }
        public int PrimaryCluster { get; init; }
        public float Confidence { get; init; }
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
