using MetalSurfacesDefectsAnalyzer.App.Dialogs;
using MetalSurfacesDefectsAnalyzer.App.Models;
using MetalSurfacesDefectsAnalyzer.Utilts.Models;
using MetalSurfacesDefectsAnalyzer.Utilts.Visualization;
using ScottPlot;
using ScottPlot.WPF;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MetalSurfacesDefectsAnalyzer.App.Pages
{
    /// <summary>
    /// Логика взаимодействия для VisualizationPage.xaml
    /// </summary>
    public partial class VisualizationPage : Page
    {
        private readonly IDimensionalityReducer _reducer;
        private DimensionalityReductionResult? _currentResult;
        private CancellationTokenSource? _cts;

        private static readonly System.Windows.Media.Color[] ClusterColors = new[]
        {
            System.Windows.Media.Color.FromRgb(31, 119, 180), System.Windows.Media.Color.FromRgb(255, 127, 14),
            System.Windows.Media.Color.FromRgb(44, 160, 44), System.Windows.Media.Color.FromRgb(214, 39, 40),
            System.Windows.Media.Color.FromRgb(148, 103, 189), System.Windows.Media.Color.FromRgb(140, 86, 75),
            System.Windows.Media.Color.FromRgb(227, 119, 194), System.Windows.Media.Color.FromRgb(127, 127, 127),
            System.Windows.Media.Color.FromRgb(188, 189, 34), System.Windows.Media.Color.FromRgb(23, 190, 207),
            System.Windows.Media.Color.FromRgb(174, 199, 232), System.Windows.Media.Color.FromRgb(255, 187, 120)
        };

        public VisualizationPage()
        {
            InitializeComponent();
            _reducer = new DimensionalityReducer();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var result = AppState.Instance.CurrentAnalysisResult;
            if (result == null || result.LatentVectors == null || result.LatentVectors.Length == 0)
            {
                TxtSubtitle.Text = "Нет данных для визуализации. Выполните анализ изображений.";
                BtnTSNE.IsEnabled = false;
                BtnUMAP.IsEnabled = false;
                BtnSave.IsEnabled = false;
            }
            else
            {
                TxtSubtitle.Text = $"Доступно {result.LatentVectors.Length} точек для проекции";
            }
        }

        private async void BtnTSNE_Click(object sender, RoutedEventArgs e)
        {
            await RunVisualization("tsne");
        }

        private async void BtnUMAP_Click(object sender, RoutedEventArgs e)
        {
            await RunVisualization("umap");
        }

        private async Task RunVisualization(string method)
        {
            var result = AppState.Instance.CurrentAnalysisResult;
            if (result == null || result.LatentVectors == null || result.LatentVectors.Length == 0)
            {
                MessageBox.Show("Нет данных для визуализации.", "Информация",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            ShowLoading(method);
            InfoBar.Visibility = Visibility.Collapsed;

            try
            {
                var progress = new Progress<int>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtLoading.Text = $"{method.ToUpper()}: вычисление... {p}%";
                    });
                });

                _currentResult = await _reducer.ReduceAsync(result, method, progress, _cts.Token);

                PlotResult(_currentResult);
                ShowInfo(_currentResult);
            }
            catch (OperationCanceledException)
            {
                TxtLoading.Text = "Операция отменена";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка визуализации:\n{ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void PlotResult(DimensionalityReductionResult result)
        {
            var plot = PlotControl.Plot;
            plot.Clear();

            // Настройка
            string methodName = result.Method == "TSNE" ? "t-SNE" : "UMAP";
            plot.Title($"Проекция латентного пространства ({methodName})");
            plot.XLabel($"{methodName} компонента 1");
            plot.YLabel($"{methodName} компонента 2");

            // Группируем точки по кластерам
            var clusterGroups = new Dictionary<int, (List<double> xs, List<double> ys)>();

            for (int i = 0; i < result.Points2D.Length; i++)
            {
                int cluster = result.ClusterAssignments[i];
                if (cluster < 0) continue;

                if (!clusterGroups.ContainsKey(cluster))
                    clusterGroups[cluster] = (new List<double>(), new List<double>());

                clusterGroups[cluster].xs.Add(result.Points2D[i][0]);
                clusterGroups[cluster].ys.Add(result.Points2D[i][1]);
            }

            // Отображаем каждый кластер своим цветом
            var legendItems = new List<(string label, ScottPlot.Color color)>();

            foreach (var group in clusterGroups.OrderBy(g => g.Key))
            {
                int clusterId = group.Key;
                var color = ClusterColors[clusterId % ClusterColors.Length];
                var scottColor = new ScottPlot.Color(color.R, color.G, color.B);

                var scatter = plot.Add.Scatter(
                    group.Value.xs.ToArray(),
                    group.Value.ys.ToArray());

                scatter.Color = scottColor;
                scatter.MarkerSize = 8;
                scatter.LineWidth = 0;
                scatter.LegendText = $"Кластер {clusterId}";
                scatter.MarkerShape = MarkerShape.FilledCircle;
            }

            // Добавляем центроиды кластеров
            if (result.ClusterCentroids2D != null && result.ClusterCentroids2D.Count > 0)
            {
                foreach (var centroid in result.ClusterCentroids2D)
                {
                    int clusterId = centroid.Key;
                    var color = ClusterColors[clusterId % ClusterColors.Length];
                    var scottColor = new ScottPlot.Color(color.R, color.G, color.B);

                    var marker = plot.Add.Marker(centroid.Value[0], centroid.Value[1]);
                    marker.Color = scottColor;
                    marker.MarkerSize = 14;
                    marker.MarkerShape = MarkerShape.FilledDiamond;
                    marker.LineWidth = 2;
                    marker.MarkerLineColor = ScottPlot.Color.FromHex("#000000");
                }
            }

            // Настройка легенды
            plot.Legend.IsVisible = true;
            plot.Legend.FontSize = 11;
            plot.Legend.Location = Alignment.UpperRight;

            // Сетка
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E0E0E0");

            PlotControl.Refresh();
            EmptyPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void ShowInfo(DimensionalityReductionResult result)
        {
            string methodName = result.Method == "TSNE" ? "t-SNE" : "UMAP";
            TxtInfoMethod.Text = $"Метод: {methodName}";
            TxtInfoTime.Text = $"Время вычисления: {result.ComputationTime.TotalSeconds:F1} сек";
            TxtInfoPoints.Text = $"Точек: {result.Points2D.Length}";
            InfoBar.Visibility = Visibility.Visible;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null)
            {
                MessageBox.Show("Сначала выполните визуализацию.",
                              "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить визуализацию",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                FileName = $"visualization_{_currentResult.Method}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    PlotControl.Plot.SavePng(dialog.FileName, 1200, 800);
                    MessageBox.Show($"Визуализация сохранена:\n{dialog.FileName}",
                                  "Сохранено", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения:\n{ex.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShowLoading(string method)
        {
            EmptyPlaceholder.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            TxtLoading.Text = $"{method.ToUpper()}: подготовка данных...";
            BtnTSNE.IsEnabled = false;
            BtnUMAP.IsEnabled = false;
            BtnSave.IsEnabled = false;
        }

        private void HideLoading()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            BtnTSNE.IsEnabled = true;
            BtnUMAP.IsEnabled = true;
            BtnSave.IsEnabled = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
