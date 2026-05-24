using MetalSurfacesDefectsAnalyzer.App.Models;
using MetalSurfacesDefectsAnalyzer.Core.Structs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MetalSurfacesDefectsAnalyzer.App.Pages
{
    /// <summary>
    /// Логика взаимодействия для ResultsPage.xaml
    /// </summary>
    public partial class ResultsPage : Page
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

        public ResultsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var analysisResult = AppState.Instance.CurrentAnalysisResult;
            var loadedImages = AppState.Instance.LoadedImages;

            if (analysisResult == null || loadedImages == null || loadedImages.Count == 0)
            {
                TxtSelectedFile.Text = "Нет данных для отображения. Выполните анализ.";
                return;
            }

            var displayItems = new ObservableCollection<ImageResultDisplay>();

            foreach (var img in loadedImages)
            {
                var result = analysisResult.Results
                    .FirstOrDefault(r => r.FileName.Equals(img.FileName, StringComparison.OrdinalIgnoreCase));

                displayItems.Add(new ImageResultDisplay
                {
                    FileName = img.FileName,
                    Thumbnail = img.Thumbnail,
                    PrimaryCluster = result?.PrimaryCluster ?? -1,
                    Confidence = result?.Confidence ?? 0f,
                    IsSelected = false
                });
            }

            LbImages.ItemsSource = displayItems;
        }

        private void LbImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbImages.SelectedItem is not ImageResultDisplay selected)
                return;

            // Сбрасываем выделение у остальных
            if (LbImages.ItemsSource is ObservableCollection<ImageResultDisplay> items)
            {
                foreach (var item in items)
                    item.IsSelected = false;
                selected.IsSelected = true;
            }

            var result = AppState.Instance.GetResultForImage(selected.FileName);
            if (result == null)
            {
                TxtSelectedFile.Text = $"Нет результатов для {selected.FileName}";
                return;
            }

            ShowDetailedResult(result);
        }

        private void ShowDetailedResult(ClusterResult result)
        {
            TxtSelectedFile.Text = result.FileName;
            SummaryPanel.Visibility = Visibility.Visible;
            IcClusterBars.Visibility = Visibility.Visible;
            LatentPanel.Visibility = Visibility.Visible;

            TxtMainCluster.Text = $"Кластер {result.PrimaryCluster}";
            TxtConfidence.Text = $"{result.Confidence:P1}";
            TxtEntropy.Text = $"{result.Uncertainty * MathF.Log(12):F3}";  // H(γ)
            TxtUncertainty.Text = $"{result.Uncertainty:F3}";

            // Заполняем распределение вероятностей
            var bars = new ObservableCollection<ClusterProbabilityBar>();
            for (int i = 0; i < result.ClusterProbabilities.Length; i++)
            {
                bars.Add(new ClusterProbabilityBar
                {
                    ClusterId = i,
                    Probability = result.ClusterProbabilities[i],
                    BarColor = new SolidColorBrush(ClusterColors[i % ClusterColors.Length])
                });
            }
            IcClusterBars.ItemsSource = bars;

            // Латентный вектор
            if (result.LatentVector != null)
            {
                var first12 = string.Join(", ", result.LatentVector.Take(12).Select(v => v.ToString("F4")));
                TxtLatentVector.Text = first12 + (result.LatentVector.Length > 8 ? ", ..." : "");
                LatentPanel.Visibility = Visibility.Visible;
            }
            else
            {
                LatentPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
