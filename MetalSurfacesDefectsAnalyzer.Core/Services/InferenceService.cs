using MetalSurfacesDefectsAnalyzer.Core.Models;
using MetalSurfacesDefectsAnalyzer.Core.Structs;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MetalSurfacesDefectsAnalyzer.Core.Services
{
    public class InferenceService : IInferenceService
    {
        private InferenceSession? _vadeSession;
        private double _currentEta;
        private bool _disposed;

        private const int FeatureVectorSize = 512;
        private const int LatentVectorSize = 32;
        private const int NumClusters = 12;
        private const float Temperature = 0.3f;

        public bool IsModelLoaded => _vadeSession != null;
        public double CurrentEta => _currentEta;

        public IReadOnlyList<double> AvailableEtas { get; } = new[]
        {
            0.00, 0.04, 0.07
        };

        public Task LoadModelAsync(double eta, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                // Освобождаем предыдущую сессию
                _vadeSession?.Dispose();
                _vadeSession = null;

                // Формируем имя файла модели
                var etaStr = eta.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                              .Replace(".", "_");
                var modelFileName = $"model_eta_{etaStr}.onnx";

                _vadeSession = ModelLoader.LoadModel(modelFileName);
                _currentEta = eta;

                ValidateModelIO();

            }, cancellationToken);
        }

        public async Task<ClusterResult> InferAsync(DefectImage image, CancellationToken cancellationToken = default)
        {
            if (_vadeSession == null)
                throw new InvalidOperationException("Модель не загружена. Вызовите LoadModelAsync()");

            if (!image.IsProcessed || image.FeatureVector == null)
                throw new InvalidOperationException("Изображение не обработано. Выполните предобработку.");

            return await Task.Run(() =>
            {
                // Подготовка входного тензора [1, 512]
                var inputShape = new[] { 1, FeatureVectorSize };
                var inputTensor = new DenseTensor<float>(image.FeatureVector, inputShape);

                var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };

                // Запуск инференса
                using var results = _vadeSession.Run(inputs);

                // Извлечение выходов
                var gammaTensor = results.First(r => r.Name == "gamma").AsTensor<float>();
                var latentTensor = results.First(r => r.Name == "z").AsTensor<float>();

                // Получаем массивы
                var probabilities = gammaTensor.ToArray();  // [12]
                var latentVector = latentTensor.ToArray();   // [32]

                // Определяем основной кластер
                int primaryCluster = 0;
                float maxProb = probabilities[0];
                for (int i = 1; i < probabilities.Length; i++)
                {
                    if (probabilities[i] > maxProb)
                    {
                        maxProb = probabilities[i];
                        primaryCluster = i;
                    }
                }

                // Вычисляем энтропию
                float entropy = 0f;
                foreach (var p in probabilities)
                {
                    if (p > 1e-10f)
                        entropy -= p * MathF.Log(p);
                }

                // Нормализованная неопределенность (0 - уверенно, 1 - полностью неопределенно)
                float maxEntropy = MathF.Log(NumClusters);
                float uncertainty = entropy / maxEntropy;

                return new ClusterResult
                {
                    FileName = image.FileName,
                    PrimaryCluster = primaryCluster,
                    ClusterProbabilities = probabilities,
                    Confidence = maxProb,
                    Uncertainty = uncertainty,
                    LatentVector = latentVector
                };
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ClusterResult>> InferBatchAsync(
            IReadOnlyList<DefectImage> images,
            IProgress<InferenceProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_vadeSession == null)
                throw new InvalidOperationException("Модель не загружена");

            var results = new ClusterResult[images.Count];
            int processed = 0;

            // Последовательная обработка для стабильности
            for (int i = 0; i < images.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    results[i] = await InferAsync(images[i], cancellationToken);
                }
                catch (Exception ex)
                {
                    // В случае ошибки создаем результат с нулевой уверенностью
                    results[i] = new ClusterResult
                    {
                        FileName = images[i].FileName,
                        PrimaryCluster = -1,
                        ClusterProbabilities = new float[NumClusters],
                        Confidence = 0f,
                        Uncertainty = 1f
                    };

                    System.Diagnostics.Debug.WriteLine(
                        $"Ошибка инференса для {images[i].FileName}: {ex.Message}");
                }

                processed++;
                progress?.Report(new InferenceProgress
                {
                    Processed = processed,
                    Total = images.Count,
                    CurrentFile = images[i].FileName
                });
            }

            return results;
        }

        private void ValidateModelIO()
        {
            if (_vadeSession == null) return;

            // Проверяем входы
            if (!_vadeSession.InputMetadata.ContainsKey("input"))
            {
                var availableInputs = string.Join(", ", _vadeSession.InputMetadata.Keys);
                throw new InvalidOperationException(
                    $"Ожидался вход 'input'. Доступные входы: {availableInputs}");
            }

            // Проверяем выходы
            var requiredOutputs = new[] { "gamma", "z", "mu", "logvar" };
            foreach (var output in requiredOutputs)
            {
                if (!_vadeSession.OutputMetadata.ContainsKey(output))
                {
                    var availableOutputs = string.Join(", ", _vadeSession.OutputMetadata.Keys);
                    System.Diagnostics.Debug.WriteLine(
                        $"Предупреждение: Выход '{output}' не найден. Доступные: {availableOutputs}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _vadeSession?.Dispose();
                _disposed = true;
            }
        }
    }
}
