using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MetalSurfacesDefectsAnalyzer.Core.Structs;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MetalSurfacesDefectsAnalyzer.Core.Services
{
    public class ImagePreprocessor : IImagePreprocessor, IDisposable
    {
        private const int TargetSize = 224;
        private const int FeatureVectorSize = 512;

        private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
        private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

        private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp"
        };

        private InferenceSession? _resnetSession;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _disposed;

        private async Task<InferenceSession> GetResNetSessionAsync()
        {
            if (_resnetSession != null)
                return _resnetSession;

            await _initLock.WaitAsync();
            try
            {
                if (_resnetSession != null)
                    return _resnetSession;

                // Проверяем наличие папки с моделями
                var modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor] Папка моделей: {modelsDir}");
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor] Существует: {Directory.Exists(modelsDir)}");

                if (Directory.Exists(modelsDir))
                {
                    var files = Directory.GetFiles(modelsDir);
                    System.Diagnostics.Debug.WriteLine($"[ImageProcessor] Файлов в папке: {files.Length}");
                    foreach (var f in files)
                        System.Diagnostics.Debug.WriteLine($"[ImageProcessor]   {Path.GetFileName(f)}");
                }

                _resnetSession = await Task.Run(() =>
                    ModelLoader.LoadModel("resnet.onnx"));

                // Информация о входе/выходе
                System.Diagnostics.Debug.WriteLine("[ImageProcessor] ResNet входы:");
                foreach (var input in _resnetSession.InputMetadata)
                    System.Diagnostics.Debug.WriteLine($"  {input.Key}: {string.Join(", ", input.Value.Dimensions)}");

                System.Diagnostics.Debug.WriteLine("[ImageProcessor] ResNet выходы:");
                foreach (var output in _resnetSession.OutputMetadata)
                    System.Diagnostics.Debug.WriteLine($"  {output.Key}: {string.Join(", ", output.Value.Dimensions)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor] Ошибка загрузки ResNet: {ex}");
                throw;
            }
            finally
            {
                _initLock.Release();
            }

            return _resnetSession;
        }

        public async Task<DefectImage> ProcessAsync(string imagePath)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // Проверка формата
                    var extension = Path.GetExtension(imagePath);
                    if (!SupportedFormats.Contains(extension))
                    {
                        return CreateErrorResult(imagePath, $"Неподдерживаемый формат: {extension}");
                    }

                    // Проверка существования файла
                    if (!File.Exists(imagePath))
                    {
                        return CreateErrorResult(imagePath, "Файл не найден");
                    }

                    // Загрузка изображения
                    using var bitmap = new Bitmap(imagePath);

                    // Проверка размеров
                    if (bitmap.Width < 50 || bitmap.Height < 50)
                    {
                        return CreateErrorResult(imagePath,
                            $"Слишком маленькое разрешение: {bitmap.Width}x{bitmap.Height}");
                    }

                    int originalWidth = bitmap.Width;
                    int originalHeight = bitmap.Height;

                    // Шаг 1: Масштабирование до 224x224
                    using var resized = ResizeToTarget(bitmap);

                    // Шаг 2: Преобразование в тензор [1, 3, 224, 224] с нормализацией ImageNet
                    var inputTensor = BitmapToTensor(resized);

                    // Шаг 3: Извлечение признаков через ResNet
                    var session = await GetResNetSessionAsync();
                    var features = RunResNetInference(session, inputTensor);

                    // Валидация размера вектора признаков
                    if (features.Length != FeatureVectorSize)
                    {
                        return CreateErrorResult(imagePath,
                            $"Неверный размер вектора признаков: {features.Length}, ожидалось {FeatureVectorSize}");
                    }

                    return new DefectImage
                    {
                        FilePath = imagePath,
                        FileName = Path.GetFileName(imagePath),
                        Width = originalWidth,
                        Height = originalHeight,
                        FeatureVector = features,
                        IsProcessed = true
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ImageProcessor] Ошибка обработки {Path.GetFileName(imagePath)}: {ex.Message}");

                    return CreateErrorResult(imagePath, $"Ошибка обработки: {ex.Message}");
                }
            });
        }

        public async Task<IReadOnlyList<DefectImage>> ProcessBatchAsync(
            IEnumerable<string> imagePaths,
            IProgress<ProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var paths = imagePaths.ToList();
            var results = new DefectImage[paths.Count];
            var processed = 0;

            // Предзагружаем ResNet сессию
            await GetResNetSessionAsync();

            // Ограничиваем параллелизм
            using var semaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount - 1));

            var tasks = paths.Select(async (path, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    results[index] = await ProcessAsync(path);

                    Interlocked.Increment(ref processed);
                    progress?.Report(new ProcessingProgress
                    {
                        Processed = processed,
                        Total = paths.Count,
                        CurrentFile = Path.GetFileName(path)
                    });
                }
                catch (OperationCanceledException)
                {
                    results[index] = CreateErrorResult(path, "Операция отменена");
                }
                catch (Exception ex)
                {
                    results[index] = CreateErrorResult(path, $"Ошибка: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return results;
        }

        private Bitmap ResizeToTarget(Bitmap source)
        {
            var destImage = new Bitmap(TargetSize, TargetSize, PixelFormat.Format24bppRgb);
            destImage.SetResolution(96, 96);

            using var graphics = Graphics.FromImage(destImage);

            // Настройки качества
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Вычисляем масштаб с сохранением пропорций (по меньшей стороне)
            float scale = Math.Min(
                (float)TargetSize / source.Width,
                (float)TargetSize / source.Height);

            int scaledWidth = (int)(source.Width * scale);
            int scaledHeight = (int)(source.Height * scale);
            int offsetX = (TargetSize - scaledWidth) / 2;
            int offsetY = (TargetSize - scaledHeight) / 2;

            // Черный фон
            graphics.Clear(Color.Black);

            // Рисуем изображение по центру
            graphics.DrawImage(source,
                new Rectangle(offsetX, offsetY, scaledWidth, scaledHeight),
                0, 0, source.Width, source.Height,
                GraphicsUnit.Pixel);

            return destImage;
        }

        private DenseTensor<float> BitmapToTensor(Bitmap bitmap)
        {
            if (bitmap.Width != TargetSize || bitmap.Height != TargetSize)
                throw new ArgumentException($"Ожидался размер {TargetSize}x{TargetSize}");

            var tensor = new DenseTensor<float>(new[] { 1, 3, TargetSize, TargetSize });

            // Копируем все пиксели в массив для быстрого доступа
            for (int y = 0; y < TargetSize; y++)
            {
                for (int x = 0; x < TargetSize; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);

                    // Нормализация ImageNet: (pixel/255 - mean) / std
                    tensor[0, 0, y, x] = ((pixel.R / 255.0f) - Mean[0]) / Std[0];  // R
                    tensor[0, 1, y, x] = ((pixel.G / 255.0f) - Mean[1]) / Std[1];  // G
                    tensor[0, 2, y, x] = ((pixel.B / 255.0f) - Mean[2]) / Std[2];  // B
                }
            }

            return tensor;
        }

        private float[] RunResNetInference(InferenceSession session, DenseTensor<float> inputTensor)
        {
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };

            using var results = session.Run(inputs);

            // Берём первый выход (как в старом проекте)
            var features = results.First().AsTensor<float>().ToArray();

            return features;
        }

        private DefectImage CreateErrorResult(string imagePath, string errorMessage)
        {
            return new DefectImage
            {
                FilePath = imagePath,
                FileName = Path.GetFileName(imagePath),
                Width = 0,
                Height = 0,
                FeatureVector = null,
                IsProcessed = false,
                ErrorMessage = errorMessage
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _resnetSession?.Dispose();
                _resnetSession = null;
                _initLock.Dispose();
                _disposed = true;
            }
        }
    }
}
