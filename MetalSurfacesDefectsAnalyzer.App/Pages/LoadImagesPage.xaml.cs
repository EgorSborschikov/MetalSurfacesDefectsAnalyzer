using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MetalSurfacesDefectsAnalyzer.App.Dialogs;
using MetalSurfacesDefectsAnalyzer.App.Models;
using Microsoft.Win32;

namespace MetalSurfacesDefectsAnalyzer.App.Pages
{
    /// <summary>
    /// Логика взаимодействия для LoadImagesPage.xaml
    /// </summary>
    public partial class LoadImagesPage : Page, INotifyPropertyChanged
    {
        private ObservableCollection<ImageFileInfo> _imageFiles = new();
        private readonly HashSet<string> _loadedPaths = new();
        private long _totalSize = 0;
        private int _selectedCount = 0;
        private bool _isLoading = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LoadImagesPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholder();
            UpdateButtonStates();
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Обработчики кнопок

        private void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите изображения дефектов металла",
                Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Все файлы (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadImagesAsync(openFileDialog.FileNames);
            }
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку с изображениями",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dialog.ShowDialog() == true)
            {
                var supportedExtensions = new[] { ".png", ".jpg", ".jpeg" };
                try
                {
                    var files = Directory.GetFiles(dialog.FolderName, "*.*", SearchOption.TopDirectoryOnly)
                                       .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                                       .ToArray();

                    if (files.Length == 0)
                    {
                        MessageBox.Show("В выбранной папке нет изображений поддерживаемых форматов (PNG, JPG, JPEG).",
                                      "Нет изображений",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Warning);
                        return;
                    }

                    LoadImagesAsync(files);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при чтении папки: {ex.Message}",
                                  "Ошибка",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_imageFiles.Count > 0)
            {
                var result = MessageBox.Show($"Удалить все загруженные изображения ({_imageFiles.Count} шт.)?",
                                            "Подтверждение очистки",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ClearAllImages();
                }
            }
        }

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = LvImages.SelectedItems.Cast<ImageFileInfo>().ToList();

            if (selectedItems.Count == 0) return;

            var result = MessageBox.Show($"Удалить выбранные изображения ({selectedItems.Count} шт.)?",
                                        "Подтверждение удаления",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in selectedItems)
                {
                    _imageFiles.Remove(item);
                    _loadedPaths.Remove(item.FullPath);
                    _totalSize -= item.FileSize;
                    item.Dispose();
                }

                ReindexImages();
                UpdateUI();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                MessageBox.Show("Дождитесь завершения загрузки изображений.",
                              "Загрузка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            var validImages = _imageFiles.Where(img => img.Status == "Valid").ToList();

            if (validImages.Count == 0)
            {
                MessageBox.Show("Нет корректных изображений для анализа.\nПроверьте загруженные файлы.",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            // Сохраняем список загруженных изображений
            AppState.Instance.LoadedImages = validImages;

            // Получаем главное окно и переходим на страницу анализа
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.EnableAnalysisButtons();
                // Здесь будет навигация на страницу анализа
            }

            MessageBox.Show($"Загружено {validImages.Count} корректных изображений.\nМожно приступать к анализу.",
                          "Готово",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }

        #endregion

        #region Обработчики событий ListView

        private void LvImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCount = LvImages.SelectedItems.Count;
            TxtSelectedCount.Text = _selectedCount.ToString();
            BtnRemoveSelected.IsEnabled = _selectedCount > 0;
        }

        private void LvImages_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (LvImages.SelectedItem is ImageFileInfo selectedImage && selectedImage.Status == "Valid")
            {
                ShowImagePreview(selectedImage);
            }
        }

        #endregion

        #region Загрузка изображений (ИСПРАВЛЕНО)

        /// <summary>
        /// Асинхронная загрузка изображений с правильной работой с UI потоком
        /// </summary>
        private async void LoadImagesAsync(string[] filePaths)
        {
            if (_isLoading) return;
            _isLoading = true;

            // Показываем индикатор загрузки
            var progressWindow = new LoadingWindow("Загрузка изображений...");
            progressWindow.Owner = Window.GetWindow(this);
            progressWindow.Show();

            int loadedCount = 0;
            int errorCount = 0;
            int duplicateCount = 0;

            try
            {
                await Task.Run(() =>
                {
                    foreach (var filePath in filePaths)
                    {
                        // Проверяем, не загружен ли уже этот файл
                        lock (_loadedPaths)
                        {
                            if (_loadedPaths.Contains(filePath))
                            {
                                Interlocked.Increment(ref duplicateCount);
                                continue;
                            }
                        }

                        try
                        {
                            var imageInfo = ProcessImageFile(filePath);

                            if (imageInfo != null)
                            {
                                // Создаем миниатюру В ОСНОВНОМ ПОТОКЕ
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        var thumbnail = CreateThumbnailInUIThread(filePath, 80, 60);
                                        imageInfo.Thumbnail = thumbnail;

                                        lock (_loadedPaths)
                                        {
                                            if (_loadedPaths.Add(filePath))
                                            {
                                                _imageFiles.Add(imageInfo);
                                                _totalSize += imageInfo.FileSize;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Если не удалось создать миниатюру, добавляем без неё
                                        imageInfo.Status = "Error";
                                        imageInfo.StatusText = "Ошибка миниатюры";

                                        lock (_loadedPaths)
                                        {
                                            if (_loadedPaths.Add(filePath))
                                            {
                                                _imageFiles.Add(imageInfo);
                                            }
                                        }

                                        Interlocked.Increment(ref errorCount);
                                    }
                                });

                                Interlocked.Increment(ref loadedCount);
                            }
                            else
                            {
                                Interlocked.Increment(ref errorCount);
                            }
                        }
                        catch
                        {
                            Interlocked.Increment(ref errorCount);
                        }
                    }
                });

                // Небольшая задержка для завершения UI операций
                await Task.Delay(100);
            }
            finally
            {
                progressWindow.Close();
                _isLoading = false;

                ReindexImages();
                UpdateUI();

                // Показываем результат загрузки
                string message = $"Загружено: {loadedCount} изображений";
                if (errorCount > 0) message += $"\nОшибок: {errorCount}";
                if (duplicateCount > 0) message += $"\nДубликатов пропущено: {duplicateCount}";

                if (errorCount > 0 || duplicateCount > 0)
                {
                    MessageBox.Show(message, "Результат загрузки",
                                  MessageBoxButton.OK,
                                  errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Обработка файла изображения (без создания UI объектов)
        /// </summary>
        private ImageFileInfo? ProcessImageFile(string filePath)
        {
            // Проверяем расширение
            string extension = Path.GetExtension(filePath).ToLower();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                return CreateInvalidImageInfo(filePath, "Неверный формат");
            }

            // Проверяем существование файла
            if (!File.Exists(filePath))
            {
                return CreateInvalidImageInfo(filePath, "Файл не найден");
            }

            // Проверяем размер файла (макс 50 MB)
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 50 * 1024 * 1024)
            {
                return CreateInvalidImageInfo(filePath, "Слишком большой файл");
            }
            if (fileInfo.Length == 0)
            {
                return CreateInvalidImageInfo(filePath, "Пустой файл");
            }

            // Проверяем, что это действительно изображение и получаем его параметры
            try
            {
                using var bitmap = new Bitmap(filePath);

                // Проверяем минимальное разрешение
                if (bitmap.Width < 50 || bitmap.Height < 50)
                {
                    return CreateInvalidImageInfo(filePath, "Слишком маленькое разрешение");
                }

                // Создаем информационный объект (без миниатюры - она создается в UI потоке)
                return new ImageFileInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FullPath = filePath,
                    FileSize = fileInfo.Length,
                    FileSizeFormatted = FormatFileSize(fileInfo.Length),
                    Resolution = $"{bitmap.Width}x{bitmap.Height}",
                    Status = "Valid",
                    StatusText = "✓ Готов",
                    Width = bitmap.Width,
                    Height = bitmap.Height
                };
            }
            catch (Exception)
            {
                return CreateInvalidImageInfo(filePath, "Ошибка чтения изображения");
            }
        }

        /// <summary>
        /// Создание информационного объекта для невалидного изображения
        /// </summary>
        private ImageFileInfo CreateInvalidImageInfo(string filePath, string statusText)
        {
            return new ImageFileInfo
            {
                FileName = Path.GetFileName(filePath),
                FullPath = filePath,
                FileSize = 0,
                FileSizeFormatted = "N/A",
                Resolution = "N/A",
                Status = "Error",
                StatusText = statusText
            };
        }

        /// <summary>
        /// Создание миниатюры в UI потоке
        /// </summary>
        private BitmapSource? CreateThumbnailInUIThread(string filePath, int width, int height)
        {
            try
            {
                // Используем BitmapImage для загрузки в UI потоке
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(filePath);
                bitmapImage.DecodePixelWidth = width;
                bitmapImage.DecodePixelHeight = height;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmapImage.EndInit();

                // Замораживаем для использования в других потоках
                bitmapImage.Freeze();

                return bitmapImage;
            }
            catch
            {
                // Если не удалось, пробуем через System.Drawing
                try
                {
                    using var bitmap = new Bitmap(filePath);
                    using var thumb = bitmap.GetThumbnailImage(width, height, () => false, IntPtr.Zero);

                    using var memory = new MemoryStream();
                    thumb.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
                catch
                {
                    return null;
                }
            }
        }

        #endregion

        #region Управление UI

        private void UpdateUI()
        {
            // Обновляем ListView в UI потоке
            Dispatcher.Invoke(() =>
            {
                LvImages.ItemsSource = null;
                LvImages.ItemsSource = _imageFiles;

                TxtImageCount.Text = _imageFiles.Count(img => img.Status == "Valid").ToString();
                TxtTotalSize.Text = FormatFileSize(_totalSize);
                TxtSelectedCount.Text = "0";
            });

            UpdateButtonStates();
            UpdatePlaceholder();
        }

        private void UpdateButtonStates()
        {
            Dispatcher.Invoke(() =>
            {
                bool hasImages = _imageFiles.Count > 0;
                bool hasValidImages = _imageFiles.Any(img => img.Status == "Valid");

                BtnClear.IsEnabled = hasImages && !_isLoading;
                BtnRemoveSelected.IsEnabled = _selectedCount > 0;
                BtnNext.IsEnabled = hasValidImages && !_isLoading;
            });
        }

        private void UpdatePlaceholder()
        {
            Dispatcher.Invoke(() =>
            {
                EmptyPlaceholder.Visibility = _imageFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void ReindexImages()
        {
            Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < _imageFiles.Count; i++)
                {
                    _imageFiles[i].Index = i + 1;
                }
            });
        }

        private void ClearAllImages()
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var image in _imageFiles)
                {
                    image.Dispose();
                }

                _imageFiles.Clear();
                _loadedPaths.Clear();
                _totalSize = 0;
                _selectedCount = 0;

                UpdateUI();
            });
        }

        #endregion

        #region Вспомогательные методы

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:F1} {suffixes[suffixIndex]}";
        }

        private void ShowImagePreview(ImageFileInfo imageInfo)
        {
            var previewWindow = new ImagePreviewWindow(imageInfo);
            previewWindow.Owner = Window.GetWindow(this);
            previewWindow.ShowDialog();
        }

        #endregion
    }
}
