using MetalSurfacesDefectsAnalyzer.App.Models;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MetalSurfacesDefectsAnalyzer.App.Dialogs
{
    /// <summary>
    /// Логика взаимодействия для ImagePreviewWindow.xaml
    /// </summary>
    public partial class ImagePreviewWindow : Window
    {
        public ImagePreviewWindow(ImageFileInfo imageInfo)
        {
            InitializeComponent();

            Title = $"Просмотр: {imageInfo.FileName}";
            TxtFileName.Text = imageInfo.FileName;
            TxtResolution.Text = $"{imageInfo.Width}x{imageInfo.Height}";
            TxtSize.Text = imageInfo.FileSizeFormatted;

            // Загружаем полное изображение
            if (System.IO.File.Exists(imageInfo.FullPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new System.Uri(imageInfo.FullPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ImgPreview.Source = bitmap;
                }
                catch
                {
                    MessageBox.Show("Не удалось загрузить изображение.",
                                  "Ошибка",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
