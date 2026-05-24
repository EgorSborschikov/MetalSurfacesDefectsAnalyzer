using MetalSurfacesDefectsAnalyzer.App.Dialogs;
using MetalSurfacesDefectsAnalyzer.App.Models;
using MetalSurfacesDefectsAnalyzer.Core.Models;
using MetalSurfacesDefectsAnalyzer.Core.Services;
using MetalSurfacesDefectsAnalyzer.Core.Structs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MetalSurfacesDefectsAnalyzer.App.Pages
{
    /// <summary>
    /// Логика взаимодействия для AnalysisPage.xaml
    /// </summary>
    public partial class AnalysisPage : Page
    {
        private readonly IImagePreprocessor _imageProcessor;
        private readonly IInferenceService _inferenceService;
        private CancellationTokenSource? _cts;
        private bool _isAnalyzing;
        private DateTime _analysisStartTime;

        private static readonly Color[] ClusterColors = new[]
        {
            Color.FromRgb(31, 119, 180), Color.FromRgb(255, 127, 14),
            Color.FromRgb(44, 160, 44), Color.FromRgb(214, 39, 40),
            Color.FromRgb(148, 103, 189), Color.FromRgb(140, 86, 75),
            Color.FromRgb(227, 119, 194), Color.FromRgb(127, 127, 127),
            Color.FromRgb(188, 189, 34), Color.FromRgb(23, 190, 207),
            Color.FromRgb(174, 199, 232), Color.FromRgb(255, 187, 120)
        };

        public AnalysisPage()
        {
            InitializeComponent();

            _imageProcessor = new ImagePreprocessor();
            _inferenceService = new InferenceService();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var loadedImages = AppState.Instance.LoadedImages;

            if (loadedImages == null || loadedImages.Count == 0)
            {
                ShowNoDataState();
                return;
            }

            TxtImageCount.Text = loadedImages.Count.ToString();
            TxtSubtitle.Text = $"Загружено {loadedImages.Count} изображений. Загрузка модели...";
            TxtModelStatus.Text = "Загрузка модели...";
            ModelStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));

            BtnStartAnalysis.IsEnabled = false;
            BtnBack.IsEnabled = false;

            try
            {
                var eta = 0.04;
                if (Window.GetWindow(this) is MainWindow mainWindow)
                    eta = mainWindow.GetCurrentEta();

                TxtModelInfo.Text = $"η={eta:F2}";

                await _inferenceService.LoadModelAsync(eta);

                UpdateModelStatus(true, eta);
                BtnStartAnalysis.IsEnabled = true;
                BtnBack.IsEnabled = true;
                TxtSubtitle.Text = $"Модель загружена. Готово к анализу {loadedImages.Count} изображений.";
            }
            catch (Exception ex)
            {
                UpdateModelStatus(false, 0, ex.Message);
                TxtSubtitle.Text = $"Ошибка загрузки модели: {ex.Message}";

                MessageBox.Show($"Не удалось загрузить модель ONNX.\n\n{ex.Message}",
                              "Ошибка загрузки модели",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private async void BtnStartAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnalyzing) return;

            var loadedImages = AppState.Instance.LoadedImages;
            if (loadedImages == null || loadedImages.Count == 0) return;

            _isAnalyzing = true;
            _analysisStartTime = DateTime.Now;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            ShowProgressState();
            BtnCancelAnalysis.Visibility = Visibility.Visible;
            BtnBack.IsEnabled = false;

            int errors = 0;

            try
            {
                // Этап 1: Предобработка
                UpdateProgress("Шаг 1/2: Предобработка изображений",
                              "Масштабирование и извлечение признаков...", 0, 0, 0);
                TxtStage.Text = "1/2";

                var progress = new Progress<ProcessingProgress>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateProgress("Шаг 1/2: Предобработка изображений",
                                      $"Обработка: {p.CurrentFile}",
                                      p.Percentage, p.Processed, errors);
                        TxtProcessed.Text = p.Processed.ToString();
                    });
                });

                var imagePaths = loadedImages.Select(img => img.FullPath).ToList();
                var processedImages = await _imageProcessor.ProcessBatchAsync(imagePaths, progress, token);

                var validImages = processedImages.Where(img => img.IsProcessed).ToList();
                errors = processedImages.Count - validImages.Count;
                TxtErrors.Text = errors.ToString();

                if (validImages.Count == 0)
                    throw new InvalidOperationException("Нет успешно обработанных изображений для анализа");

                // Этап 2: Кластеризация
                token.ThrowIfCancellationRequested();

                var inferenceProgress = new Progress<InferenceProgress>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateProgress("Шаг 2/2: Кластеризация дефектов",
                                      $"Анализ: {p.CurrentFile}",
                                      p.Processed * 100.0 / p.Total,
                                      p.Processed, errors);
                        TxtStage.Text = "2/2";
                        TxtProcessed.Text = p.Processed.ToString();
                    });
                });

                List<ClusterResult> clusterResults;

                if (_inferenceService.IsModelLoaded)
                    clusterResults = (await _inferenceService.InferBatchAsync(validImages, inferenceProgress, token)).ToList();
                else
                    throw new InvalidOperationException("Модель VaDE не загружена");

                // Формируем результат анализа
                var analysisResult = new AnalysisResult
                {
                    ModelId = $"VaDE_η{_inferenceService.CurrentEta:F2}",
                    EtaParameter = _inferenceService.CurrentEta,
                    AnalysisTime = DateTime.Now,
                    TotalImages = loadedImages.Count,
                    ProcessedImages = clusterResults.Count(r => r.PrimaryCluster >= 0),
                    Results = clusterResults,
                    LatentVectors = clusterResults
                        .Where(r => r.LatentVector != null)
                        .Select(r => r.LatentVector!)
                        .ToArray()
                };

                AppState.Instance.CurrentAnalysisResult = analysisResult;

                // Показываем завершение
                ShowCompletedState(analysisResult);

                // Включаем навигацию в MainWindow
                if (Window.GetWindow(this) is MainWindow mainWindow)
                    mainWindow.EnableResultButtons();
            }
            catch (OperationCanceledException)
            {
                TxtProgressTitle.Text = "Анализ отменён";
                TxtProgressDetail.Text = "Операция была отменена пользователем";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выполнении анализа:\n\n{ex.Message}",
                              "Ошибка анализа",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
                ShowProgressState();
                TxtProgressTitle.Text = "Ошибка анализа";
                TxtProgressDetail.Text = ex.Message;
            }
            finally
            {
                _isAnalyzing = false;
                BtnCancelAnalysis.Visibility = Visibility.Collapsed;
                BtnBack.IsEnabled = true;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void BtnCancelAnalysis_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToPage("LoadImages");
        }

        private void BtnViewResults_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToPage("Results");
        }

        private void BtnVisualization_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToPage("Visualization");
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var exportDialog = new ExportDialog();
            exportDialog.Owner = Window.GetWindow(this);
            exportDialog.ShowDialog();
        }

        

        #region Управление состояниями UI

        private void ShowNoDataState()
        {
            NoDataPanel.Visibility = Visibility.Visible;
            ProgressPanel.Visibility = Visibility.Collapsed;
            CompletedPanel.Visibility = Visibility.Collapsed;
            BtnStartAnalysis.IsEnabled = false;
        }

        private void ShowProgressState()
        {
            NoDataPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            CompletedPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowCompletedState(AnalysisResult result)
        {
            NoDataPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Collapsed;
            CompletedPanel.Visibility = Visibility.Visible;

            var uniqueClusters = result.Results
                .Where(r => r.PrimaryCluster >= 0)
                .Select(r => r.PrimaryCluster)
                .Distinct()
                .Count();

            TxtTotalProcessed.Text = $"{result.TotalImages}";
            TxtSuccessful.Text = $"{result.ProcessedImages}";
            TxtClustersFound.Text = $"{uniqueClusters} из 12";
            TxtAnalysisTime.Text = $"{(DateTime.Now - _analysisStartTime).TotalSeconds:F1} сек";
        }

        private void UpdateProgress(string title, string detail, double percent, int processed, int errors)
        {
            TxtProgressTitle.Text = title;
            TxtProgressDetail.Text = detail;
            ProgressBar.Value = percent;
            TxtProgressPercent.Text = $"{percent:F0}%";
            TxtProcessed.Text = processed.ToString();
            TxtErrors.Text = errors.ToString();
        }

        private void UpdateModelStatus(bool loaded, double eta = 0, string? errorMessage = null)
        {
            if (loaded)
            {
                TxtModelStatus.Text = $"Модель η={eta:F2} загружена";
                ModelStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            }
            else
            {
                TxtModelStatus.Text = "Ошибка загрузки";
                ModelStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                ModelStatusBadge.ToolTip = errorMessage;
            }
        }

        #endregion

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
