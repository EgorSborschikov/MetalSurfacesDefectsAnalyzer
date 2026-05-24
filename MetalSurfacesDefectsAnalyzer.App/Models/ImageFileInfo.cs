using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace MetalSurfacesDefectsAnalyzer.App.Models
{
    public class ImageFileInfo : INotifyPropertyChanged, IDisposable
    {
        private int _index;
        private string _fileName = string.Empty;
        private string _fullPath = string.Empty;
        private long _fileSize;
        private string _fileSizeFormatted = string.Empty;
        private string _resolution = string.Empty;
        private string _status = string.Empty;
        private string _statusText = string.Empty;
        private BitmapSource? _thumbnail;
        private int _width;
        private int _height;
        private bool _disposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        public string FullPath
        {
            get => _fullPath;
            set { _fullPath = value; OnPropertyChanged(); }
        }

        public long FileSize
        {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(); }
        }

        public string FileSizeFormatted
        {
            get => _fileSizeFormatted;
            set { _fileSizeFormatted = value; OnPropertyChanged(); }
        }

        public string Resolution
        {
            get => _resolution;
            set { _resolution = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(); }
        }

        public int Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public int Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _thumbnail = null;
                _disposed = true;
            }
        }
    }
}
