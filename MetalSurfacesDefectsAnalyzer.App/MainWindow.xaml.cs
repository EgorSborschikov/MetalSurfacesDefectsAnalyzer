using MetalSurfacesDefectsAnalyzer.App.Dialogs;
using MetalSurfacesDefectsAnalyzer.App.Models;
using MetalSurfacesDefectsAnalyzer.Core.Services;
using System.Globalization;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MetalSurfacesDefectsAnalyzer.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly System.Timers.Timer _clockTimer;
        private double _currentEta = 0.04;
        private string _currentLanguage = "ru";
        private string _currentTheme = "Light";
        private double _currentDpiScale = 1.0;

        public MainWindow()
        {
            InitializeComponent();

            _clockTimer = new System.Timers.Timer(1000);
            _clockTimer.Elapsed += OnClockTick;
            _clockTimer.AutoReset = true;
            _clockTimer.Start();

            NavigateToPage("LoadImages");
            RefreshScaleDisplay();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar();
            UpdateScaleDisplay();

            PresentationSource.AddSourceChangedHandler(this, OnSourceChanged);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScaleDisplay();
        }

        private void OnSourceChanged(object sender, SourceChangedEventArgs e)
        {
            UpdateScaleDisplay();
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            // При перемещении окна на другой монитор обновляем масштаб
            UpdateScaleDisplay();
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            // Обновляем масштаб при изменении DPI
            UpdateScaleDisplay();
        }

        /// <summary>
        /// Обновление отображения масштаба
        /// </summary>
        private void UpdateScaleDisplay()
        {
            try
            {
                // Получаем DPI для текущего окна
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var matrix = source.CompositionTarget.TransformToDevice;
                    var dpiScaleX = matrix.M11;
                    var dpiScaleY = matrix.M22;

                    // Сохраняем текущий масштаб
                    _currentDpiScale = dpiScaleX;

                    // Обновляем текст в статус-баре
                    StatusScale.Text = $"{(int)(dpiScaleX * 100)}%";

                    // Дополнительно показываем DPI в процентах от стандартного (96 DPI)
                    var dpi = 96.0 * dpiScaleX;
                    StatusScale.ToolTip = $"DPI: {dpi:F0} (стандарт: 96 DPI)";
                }
                else
                {
                    StatusScale.Text = "100%";
                }
            }
            catch (Exception ex)
            {
                StatusScale.Text = "100%";
                System.Diagnostics.Debug.WriteLine($"Ошибка определения масштаба: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик часов
        /// </summary>
        private void OnClockTick(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTime.Text = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy");
            });
        }

        /// <summary>
        /// Обновление статус-бара
        /// </summary>
        private void UpdateStatusBar()
        {
            StatusModel.Text = $"η={_currentEta:F2}";
            StatusLanguage.Text = _currentLanguage switch
            {
                "ru" => "RU (UTF-8)",
                "en" => "EN (UTF-8)",
                _ => _currentLanguage
            };
            StatusTime.Text = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy");
        }

        /// <summary>
        /// Навигация по страницам
        /// </summary>
        public void NavigateToPage(string pageName)
        {
            Page? page = pageName switch
            {
                "LoadImages" => new Pages.LoadImagesPage(),
                "Analysis" => new Pages.AnalysisPage(),
                "Results" => new Pages.ResultsPage(),
                "Visualization" => new Pages.VisualizationPage(),
                _ => null
            };

            if (page != null)
            {
                MainFrame.Navigate(page);
                UpdateNavigationButtons(pageName);
            }
        }

        /// <summary>
        /// Обновление состояния навигационных кнопок
        /// </summary>
        private void UpdateNavigationButtons(string activePage)
        {
            // Сброс всех кнопки
            var navButtons = new[] { NavLoadImages, NavAnalysis, NavResults, NavVisualization, NavExport };
            foreach (var btn in navButtons)
            {
                btn.Style = (Style)FindResource("NavigationButtonStyle");
            }

            // Подсветка активной
            Button? activeButton = activePage switch
            {
                "LoadImages" => NavLoadImages,
                "Analysis" => NavAnalysis,
                "Results" => NavResults,
                "Visualization" => NavVisualization,
                _ => null
            };

            if (activeButton != null)
            {
                activeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90E2"));
            }
        }

        #region Обработчики верхнего тулбара

        /// <summary>
        /// Выбор модели
        /// </summary>
        private void BtnModel_Click(object sender, RoutedEventArgs e)
        {
            var modelWindow = new ModelSelectionWindow(_currentEta);
            modelWindow.Owner = this;

            if (modelWindow.ShowDialog() == true)
            {
                _currentEta = Models.AppConfig.Instance.SelectedEta;
                BtnModel.Content = $"Модель: η={_currentEta:F2}";
                StatusModel.Text = $"η={_currentEta:F2}";

                MessageBox.Show(
                    $"Модель изменена на η={_currentEta:F2}.\n" +
                    "Новая модель будет загружена при следующем запуске анализа.",
                    "Модель обновлена",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ModelSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string etaStr)
            {
                if (double.TryParse(etaStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double eta))
                {
                    _currentEta = eta;
                    BtnModel.Content = $"Модель: η={eta:F2}";
                    StatusModel.Text = $"η={eta:F2}";

                    // Загрузка модели через Core
                    MessageBox.Show($"Выбрана модель с η={eta:F2}",
                                  "Выбор модели",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Очистка кэша
        /// </summary>
        private async void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            var cacheService = new Core.Services.CacheService();
            var cacheInfo = cacheService.GetCacheInfo();

            string message;

            if (cacheInfo.CacheSizeBytes == 0 && AppState.Instance.LoadedImages.Count == 0)
            {
                message = "Кэш пуст. Нечего очищать.";
            }
            else
            {
                message = "Очистить кэш приложения?\n\n";

                if (AppState.Instance.LoadedImages.Count > 0)
                    message += $"• Загруженных изображений: {AppState.Instance.LoadedImages.Count}\n";

                if (cacheInfo.CacheSizeBytes > 0)
                    message += $"• Временные файлы моделей: {cacheInfo.CacheSizeFormatted} ({cacheInfo.FileCount} файлов)\n";

                message += $"\nПуть кэша: {cacheInfo.CachePath}\n\n";
                message += "После очистки потребуется заново загрузить изображения и модель.";
            }

            var result = MessageBox.Show(
                message,
                "Очистка кэша",
                MessageBoxButton.YesNo,
                cacheInfo.CacheSizeBytes > 0 ? MessageBoxImage.Question : MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes &&
                (cacheInfo.CacheSizeBytes > 0 || AppState.Instance.LoadedImages.Count > 0))
            {
                try
                {
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                    // Очищаем всё
                    await AppState.Instance.ClearAllAsync();

                    // Сбрасываем навигацию
                    NavAnalysis.IsEnabled = false;
                    NavResults.IsEnabled = false;
                    NavVisualization.IsEnabled = false;
                    NavExport.IsEnabled = false;

                    // Переходим на страницу загрузки
                    NavigateToPage("LoadImages");

                    Mouse.OverrideCursor = null;

                    MessageBox.Show(
                        "Кэш успешно очищен.\nПамять освобождена, временные файлы удалены.",
                        "Кэш очищен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Mouse.OverrideCursor = null;
                    MessageBox.Show(
                        $"Ошибка при очистке кэша:\n{ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void UpdateCacheStatus()
        {
            try
            {
                var cacheService = new Core.Services.CacheService();
                var cacheInfo = cacheService.GetCacheInfo();
                StatusCache.Text = cacheInfo.CacheSizeFormatted;
            }
            catch
            {
                StatusCache.Text = "N/A";
            }
        }

        /// <summary>
        /// О программе
        /// </summary>
        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new HelpWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        #endregion

        #region Обработчики навигационных кнопок

        private void NavLoadImages_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage("LoadImages");
        }

        private void NavAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (NavAnalysis.IsEnabled)
            {
                NavigateToPage("Analysis");
            }
        }

        private void NavResults_Click(object sender, RoutedEventArgs e)
        {
            if (NavResults.IsEnabled)
            {
                NavigateToPage("Results");
            }
        }

        private void NavVisualization_Click(object sender, RoutedEventArgs e)
        {
            if (NavVisualization.IsEnabled)
            {
                NavigateToPage("Visualization");
            }
        }

        private void NavExport_Click(object sender, RoutedEventArgs e)
        {
            if (NavExport.IsEnabled)
            {
                var exportDialog = new ExportDialog();
                exportDialog.Owner = this;
                exportDialog.ShowDialog();
            }
        }

        #endregion

        #region Публичные методы для управления навигацией

        /// <summary>
        /// Включить кнопки после загрузки изображений
        /// </summary>
        public void EnableAnalysisButtons()
        {
            NavAnalysis.IsEnabled = true;
        }

        /// <summary>
        /// Включить кнопки после анализа
        /// </summary>
        public void EnableResultButtons()
        {
            NavResults.IsEnabled = true;
            NavVisualization.IsEnabled = true;
            NavExport.IsEnabled = true;
        }

        /// <summary>
        /// Получить текущий параметр eta
        /// </summary>
        public double GetCurrentEta() => _currentEta;

        /// <summary>
        /// Получить текущий язык
        /// </summary>
        public string GetCurrentLanguage() => _currentLanguage;

        #endregion

        public void RefreshScaleDisplay()
        {
            UpdateScaleDisplay();
        }

        protected override void OnClosed(EventArgs e)
        {
            PresentationSource.RemoveSourceChangedHandler(this, OnSourceChanged);

            _clockTimer.Stop();
            _clockTimer.Dispose();
            base.OnClosed(e);
        }
    }
}