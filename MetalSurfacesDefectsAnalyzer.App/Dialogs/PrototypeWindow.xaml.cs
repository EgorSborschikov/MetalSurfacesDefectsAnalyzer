using MetalSurfacesDefectsAnalyzer.Core.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MetalSurfacesDefectsAnalyzer.App.Dialogs
{
    /// <summary>
    /// Логика взаимодействия для PrototypeWindow.xaml
    /// </summary>
    public partial class PrototypeWindow : Window
    {
        private static readonly Color[] ClusterColors = new[]
    {
        Color.FromRgb(31, 119, 180), Color.FromRgb(255, 127, 14),
        Color.FromRgb(44, 160, 44), Color.FromRgb(214, 39, 40),
        Color.FromRgb(148, 103, 189), Color.FromRgb(140, 86, 75),
        Color.FromRgb(227, 119, 194), Color.FromRgb(127, 127, 127),
        Color.FromRgb(188, 189, 34), Color.FromRgb(23, 190, 207),
        Color.FromRgb(174, 199, 232), Color.FromRgb(255, 187, 120)
    };

        public PrototypeWindow(IEnumerable<ClusterPrototype> prototypes)
        {
            InitializeComponent();
            DisplayPrototypes(prototypes.ToList());
        }

        private void DisplayPrototypes(List<ClusterPrototype> prototypes)
        {
            foreach (var proto in prototypes.OrderBy(p => p.ClusterId))
            {
                var card = new Border
                {
                    Width = 250,
                    Height = 280,
                    Margin = new Thickness(8),
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                    CornerRadius = new CornerRadius(8),
                    BorderBrush = new SolidColorBrush(ClusterColors[proto.ClusterId % ClusterColors.Length]),
                    BorderThickness = new Thickness(2)
                };

                var stack = new StackPanel { Margin = new Thickness(10) };

                // Заголовок кластера
                var header = new TextBlock
                {
                    Text = $"Кластер {proto.ClusterId}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(ClusterColors[proto.ClusterId % ClusterColors.Length]),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 10)
                };
                stack.Children.Add(header);

                // Изображение прототипа или заглушка
                if (proto.PrototypeImageData != null && proto.PrototypeImageData.Length > 0)
                {
                    try
                    {
                        var image = new System.Windows.Controls.Image
                        {
                            Width = 200,
                            Height = 150,
                            Stretch = Stretch.Uniform,
                            Source = LoadImage(proto.PrototypeImageData)
                        };
                        stack.Children.Add(image);
                    }
                    catch
                    {
                        stack.Children.Add(CreatePlaceholderImage(proto.ClusterId));
                    }
                }
                else
                {
                    stack.Children.Add(CreatePlaceholderImage(proto.ClusterId));
                }

                // Характеристики
                var stats = new TextBlock
                {
                    Text = $"Изображений: {proto.ImageCount}\n" +
                           $"Ср. уверенность: {proto.Characteristics.AverageConfidence:P1}\n" +
                           $"Ср. энтропия: {proto.Characteristics.AverageEntropy:F3}\n" +
                           $"Компактность: {proto.Characteristics.Compactness:F4}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    Margin = new Thickness(0, 10, 0, 0)
                };
                stack.Children.Add(stats);

                card.Child = stack;
                PrototypesPanel.Children.Add(card);
            }
        }

        private System.Windows.Controls.Image CreatePlaceholderImage(int clusterId)
        {
            // Создаем простую картинку с номером кластера
            const int size = 150;
            using var bitmap = new System.Drawing.Bitmap(size, size);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);

            graphics.Clear(System.Drawing.Color.FromArgb(240, 240, 240));

            using var font = new System.Drawing.Font("Arial", 36, System.Drawing.FontStyle.Bold);
            using var brush = new System.Drawing.SolidBrush(
                System.Drawing.Color.FromArgb(
                    ClusterColors[clusterId % ClusterColors.Length].R,
                    ClusterColors[clusterId % ClusterColors.Length].G,
                    ClusterColors[clusterId % ClusterColors.Length].B));

            var text = $"K{clusterId}";
            var textSize = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, brush, (size - textSize.Width) / 2, (size - textSize.Height) / 2);

            using var ms = new System.IO.MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = ms;
            bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();

            var image = new System.Windows.Controls.Image
            {
                Width = size,
                Height = size,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Source = bitmapImage
            };

            return image;
        }

        private ImageSource LoadImage(byte[] imageData)
        {
            using var ms = new System.IO.MemoryStream(imageData);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
    }
}
