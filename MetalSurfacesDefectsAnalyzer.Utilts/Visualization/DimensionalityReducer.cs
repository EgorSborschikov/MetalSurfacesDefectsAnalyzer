using MetalSurfacesDefectsAnalyzer.Core.Structs;
using MetalSurfacesDefectsAnalyzer.Utilts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Utilts.Visualization
{
    public class DimensionalityReducer : IDimensionalityReducer
    {
        public async Task<DimensionalityReductionResult> ReduceAsync(
            AnalysisResult analysisResult,
            string method = "tsne",
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var startTime = DateTime.Now;

                var latentVectors = analysisResult.LatentVectors;
                if (latentVectors == null || latentVectors.Length == 0)
                    throw new InvalidOperationException("Нет латентных векторов для визуализации");

                var results = analysisResult.Results;

                int n = latentVectors.Length;
                var vectors = latentVectors.Select(v => v.Select(x => (double)x).ToArray()).ToArray();
                var assignments = results.Select(r => r.PrimaryCluster).ToArray();
                var fileNames = results.Select(r => r.FileName).ToArray();
                var confidences = results.Select(r => r.Confidence).ToArray();

                // ДИАГНОСТИКА
                System.Diagnostics.Debug.WriteLine($"\n=== ВИЗУАЛИЗАЦИЯ: ДИАГНОСТИКА ===");
                System.Diagnostics.Debug.WriteLine($"Количество точек: {n}");
                System.Diagnostics.Debug.WriteLine($"Размерность латентного пространства: {vectors[0].Length}");
                System.Diagnostics.Debug.WriteLine($"Метод: {method}");

                // Статистика латентных векторов
                for (int d = 0; d < Math.Min(5, vectors[0].Length); d++)
                {
                    var dimValues = vectors.Select(v => v[d]).ToArray();
                    System.Diagnostics.Debug.WriteLine($"  Измерение {d}: min={dimValues.Min():F4}, max={dimValues.Max():F4}, " +
                        $"mean={dimValues.Average():F4}, std={Math.Sqrt(dimValues.Average(x => (x - dimValues.Average()) * (x - dimValues.Average()))):F4}");
                }

                // Расстояния между векторами
                var distances = new List<double>();
                for (int i = 0; i < Math.Min(n, 10); i++)
                {
                    for (int j = i + 1; j < Math.Min(n, 10); j++)
                    {
                        double dist = EuclideanDistance(vectors[i], vectors[j]);
                        distances.Add(dist);
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Среднее расстояние между первыми 10 точками: {distances.Average():F4}");
                System.Diagnostics.Debug.WriteLine($"Мин расстояние: {distances.Min():F4}");
                System.Diagnostics.Debug.WriteLine($"Макс расстояние: {distances.Max():F4}");

                // Уникальные кластеры
                var uniqueClusters = assignments.Where(a => a >= 0).Distinct().OrderBy(c => c).ToArray();
                System.Diagnostics.Debug.WriteLine($"Уникальных кластеров: {uniqueClusters.Length}");
                System.Diagnostics.Debug.WriteLine($"Кластеры: {string.Join(", ", uniqueClusters)}");

                progress?.Report(10);

                // Если все точки в одном кластере или расстояния слишком малы - масштабируем данные
                if (uniqueClusters.Length <= 1 || distances.Average() < 0.1)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Данные требуют масштабирования перед визуализацией");
                    vectors = NormalizeAndScale(vectors);
                }

                double[][] points2D;

                if (method.ToLower() == "umap")
                {
                    points2D = ReduceWithUMAP(vectors, assignments, progress, cancellationToken);
                }
                else
                {
                    points2D = ReduceWithTSNE(vectors, progress, cancellationToken);
                }

                progress?.Report(90);

                // Вычисляем центроиды кластеров в 2D пространстве
                var centroids = CalculateClusterCentroids(points2D, assignments);

                var elapsed = DateTime.Now - startTime;
                progress?.Report(100);

                // ДИАГНОСТИКА РЕЗУЛЬТАТА
                System.Diagnostics.Debug.WriteLine($"\n=== РЕЗУЛЬТАТ ПРОЕКЦИИ ===");
                System.Diagnostics.Debug.WriteLine($"Время вычисления: {elapsed.TotalSeconds:F2} сек");
                System.Diagnostics.Debug.WriteLine($"2D точки - X: min={points2D.Min(p => p[0]):F3}, max={points2D.Max(p => p[0]):F3}");
                System.Diagnostics.Debug.WriteLine($"2D точки - Y: min={points2D.Min(p => p[1]):F3}, max={points2D.Max(p => p[1]):F3}");

                return new DimensionalityReductionResult
                {
                    Method = method.ToUpper(),
                    Points2D = points2D,
                    ClusterAssignments = assignments,
                    FileNames = fileNames,
                    Confidences = confidences,
                    ClusterCentroids2D = centroids,
                    ComputationTime = elapsed
                };
            }, cancellationToken);
        }

        /// <summary>
        /// Нормализация и масштабирование данных
        /// </summary>
        private double[][] NormalizeAndScale(double[][] vectors)
        {
            int n = vectors.Length;
            int dim = vectors[0].Length;

            // Z-score нормализация по каждому измерению
            var normalized = new double[n][];
            for (int i = 0; i < n; i++)
            {
                normalized[i] = new double[dim];
            }

            for (int d = 0; d < dim; d++)
            {
                var values = vectors.Select(v => v[d]).ToArray();
                double mean = values.Average();
                double std = Math.Sqrt(values.Average(x => (x - mean) * (x - mean)));

                if (std < 1e-6) std = 1.0;

                for (int i = 0; i < n; i++)
                {
                    normalized[i][d] = (vectors[i][d] - mean) / std;
                }
            }

            // Дополнительное масштабирование для лучшей визуализации
            double globalScale = 5.0;
            for (int i = 0; i < n; i++)
            {
                for (int d = 0; d < dim; d++)
                {
                    normalized[i][d] *= globalScale;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Данные нормализованы и масштабированы (scale={globalScale})");
            return normalized;
        }

        /// <summary>
        /// Улучшенный t-SNE
        /// </summary>
        private double[][] ReduceWithTSNE(double[][] vectors, IProgress<int>? progress, CancellationToken ct)
        {
            int n = vectors.Length;
            int dim = vectors[0].Length;

            // Адаптивная перплексия
            double perplexity = Math.Min(Math.Max(5, n / 4.0), 50);
            int iterations = Math.Max(300, n * 2);
            double learningRate = Math.Max(50, n / 2.0);

            System.Diagnostics.Debug.WriteLine($"t-SNE параметры: perplexity={perplexity:F1}, iterations={iterations}, lr={learningRate:F1}");

            // Вычисление дистанционной матрицы с нормализацией
            var distances = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double dist = EuclideanDistance(vectors[i], vectors[j]);
                    distances[i, j] = dist;
                    distances[j, i] = dist;
                }
            }

            // Нормализация расстояний
            double maxDist = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (distances[i, j] > maxDist)
                        maxDist = distances[i, j];

            if (maxDist > 0)
            {
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        distances[i, j] /= maxDist;
            }

            progress?.Report(30);
            ct.ThrowIfCancellationRequested();

            // P-матрица с улучшенной симметризацией
            var P = ComputeSymmetricP(distances, perplexity);

            progress?.Report(50);
            ct.ThrowIfCancellationRequested();

            // Инициализация с большим разбросом
            var random = new Random(42);
            var Y = new double[n, 2];
            double initScale = 1.0;

            for (int i = 0; i < n; i++)
            {
                Y[i, 0] = (random.NextDouble() - 0.5) * initScale * n / 10.0;
                Y[i, 1] = (random.NextDouble() - 0.5) * initScale * n / 10.0;
            }

            var prevY = (double[,])Y.Clone();
            var gains = new double[n, 2];
            for (int i = 0; i < n; i++)
                for (int d = 0; d < 2; d++)
                    gains[i, d] = 1.0;

            double momentum = 0.5;
            double finalMomentum = 0.8;
            int momentumSwitchIter = 100;

            for (int iter = 0; iter < iterations; iter++)
            {
                ct.ThrowIfCancellationRequested();

                double currentMomentum = iter < momentumSwitchIter ? momentum : finalMomentum;

                // Q-матрица
                var Q = new double[n, n];
                double sumQ = 0;

                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        double dist = 0;
                        for (int d = 0; d < 2; d++)
                        {
                            double diff = Y[i, d] - Y[j, d];
                            dist += diff * diff;
                        }
                        Q[i, j] = 1.0 / (1.0 + dist);
                        sumQ += Q[i, j];
                    }
                }

                // Градиенты
                var dY = new double[n, 2];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;

                        double pq = P[i, j] - Q[i, j] / Math.Max(sumQ, 1e-12);
                        double dist = 0;
                        for (int d = 0; d < 2; d++)
                        {
                            double diff = Y[i, d] - Y[j, d];
                            dist += diff * diff;
                        }
                        double weight = pq * Q[i, j];

                        for (int d = 0; d < 2; d++)
                        {
                            dY[i, d] += 4.0 * weight * (Y[i, d] - Y[j, d]);
                        }
                    }
                }

                // Обновление позиций
                for (int i = 0; i < n; i++)
                {
                    for (int d = 0; d < 2; d++)
                    {
                        double gradient = dY[i, d];

                        if (Math.Sign(gradient) == Math.Sign(prevY[i, d] - Y[i, d]))
                            gains[i, d] = Math.Min(gains[i, d] * 1.2, 50.0);
                        else
                            gains[i, d] = Math.Max(gains[i, d] * 0.8, 0.01);

                        double update = currentMomentum * (Y[i, d] - prevY[i, d]);
                        update -= learningRate * gains[i, d] * gradient;

                        prevY[i, d] = Y[i, d];
                        Y[i, d] += update;
                    }
                }

                // Центрирование каждые 50 итераций
                if (iter % 50 == 0)
                {
                    double meanX = 0, meanY = 0;
                    for (int i = 0; i < n; i++)
                    {
                        meanX += Y[i, 0];
                        meanY += Y[i, 1];
                    }
                    meanX /= n;
                    meanY /= n;
                    for (int i = 0; i < n; i++)
                    {
                        Y[i, 0] -= meanX;
                        Y[i, 1] -= meanY;
                    }
                }

                progress?.Report(50 + (iter * 40 / iterations));
            }

            // Финальная нормализация
            double scaleX = 0, scaleY = 0;
            for (int i = 0; i < n; i++)
            {
                scaleX = Math.Max(scaleX, Math.Abs(Y[i, 0]));
                scaleY = Math.Max(scaleY, Math.Abs(Y[i, 1]));
            }

            double finalScale = Math.Max(scaleX, scaleY);
            if (finalScale > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    Y[i, 0] = Y[i, 0] / finalScale * 10.0;
                    Y[i, 1] = Y[i, 1] / finalScale * 10.0;
                }
            }

            var points = new double[n][];
            for (int i = 0; i < n; i++)
                points[i] = new[] { Y[i, 0], Y[i, 1] };

            return points;
        }

        private double[][] ReduceWithUMAP(double[][] vectors, int[] assignments, IProgress<int>? progress, CancellationToken ct)
        {
            int n = vectors.Length;
            int dim = vectors[0].Length;

            int nNeighbors = Math.Min(Math.Max(3, n / 3), 15);
            double minDist = 0.1;

            System.Diagnostics.Debug.WriteLine($"UMAP параметры: neighbors={nNeighbors}, minDist={minDist}");

            progress?.Report(20);

            // Вычисление k-ближайших соседей с нормализацией
            var knnIndices = new List<int[]>();
            var knnDistances = new List<double[]>();

            for (int i = 0; i < n; i++)
            {
                ct.ThrowIfCancellationRequested();

                var dists = new List<(double dist, int index)>();
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    double dist = EuclideanDistance(vectors[i], vectors[j]);
                    dists.Add((dist, j));
                }

                var nearest = dists.OrderBy(x => x.dist)
                                  .Take(nNeighbors)
                                  .ToList();

                // Нормализация расстояний
                double sigma = nearest.Average(x => x.dist);
                if (sigma < 1e-6) sigma = 1.0;

                var normDists = nearest.Select(x => x.dist / sigma).ToArray();

                knnIndices.Add(nearest.Select(x => x.index).ToArray());
                knnDistances.Add(normDists);
            }

            progress?.Report(50);
            ct.ThrowIfCancellationRequested();

            // Инициализация с учетом кластеров
            var random = new Random(42);
            var embedding = new double[n, 2];

            // Группируем начальные позиции по кластерам
            var clusterCounts = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                int c = assignments[i];
                if (!clusterCounts.ContainsKey(c))
                    clusterCounts[c] = 0;
                clusterCounts[c]++;
            }

            var clusterOffsets = new Dictionary<int, (double x, double y)>();
            foreach (var kvp in clusterCounts)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = 5.0 * (1 + clusterOffsets.Count);
                clusterOffsets[kvp.Key] = (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            }

            for (int i = 0; i < n; i++)
            {
                int c = assignments[i] >= 0 ? assignments[i] : 0;

                double offsetX = 0;
                double offsetY = 0;

                if (clusterOffsets.ContainsKey(c))
                {
                    offsetX = clusterOffsets[c].Item1;
                    offsetY = clusterOffsets[c].Item2;
                }

                embedding[i, 0] = offsetX + (random.NextDouble() - 0.5) * 2.0;
                embedding[i, 1] = offsetY + (random.NextDouble() - 0.5) * 2.0;
            }

            int epochs = 200;
            double alpha = 1.0;

            for (int epoch = 0; epoch < epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();

                for (int i = 0; i < n; i++)
                {
                    // Притяжение к соседям
                    for (int j = 0; j < Math.Min(knnIndices[i].Length, 5); j++)
                    {
                        int neighbor = knnIndices[i][j];

                        double dx = embedding[i, 0] - embedding[neighbor, 0];
                        double dy = embedding[i, 1] - embedding[neighbor, 1];
                        double dist = Math.Sqrt(dx * dx + dy * dy) + 1e-8;

                        // Притяжение (сильнее для близких точек)
                        double attraction = -2.0 * alpha * dx / dist * Math.Exp(-dist * dist);

                        embedding[i, 0] += attraction;
                        embedding[i, 1] += attraction;
                    }

                    // Отталкивание от случайных точек
                    for (int r = 0; r < 5; r++)
                    {
                        int randomPoint = random.Next(n);
                        if (randomPoint == i) continue;

                        double dx = embedding[i, 0] - embedding[randomPoint, 0];
                        double dy = embedding[i, 1] - embedding[randomPoint, 1];
                        double dist = Math.Sqrt(dx * dx + dy * dy) + 1e-4;

                        // Отталкивание
                        double repulsion = alpha * minDist / (dist * dist);

                        embedding[i, 0] += repulsion * dx;
                        embedding[i, 1] += repulsion * dy;
                    }
                }

                alpha *= 0.99;
                progress?.Report(50 + (epoch * 40 / epochs));
            }

            var points = new double[n][];
            for (int i = 0; i < n; i++)
                points[i] = new[] { embedding[i, 0], embedding[i, 1] };

            return points;
        }

        private double EuclideanDistance(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double diff = a[i] - b[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }

        private double[,] ComputeSymmetricP(double[,] distances, double perplexity)
        {
            int n = distances.GetLength(0);
            var P = new double[n, n];

            // Вычисляем условные вероятности с адаптивной сигмой
            for (int i = 0; i < n; i++)
            {
                // Находим сигму для целевой перплексии
                double sigma = 0;
                double targetEntropy = Math.Log(perplexity);

                // Бисекция для поиска sigma
                double sigmaMin = 0.01;
                double sigmaMax = 10.0;

                for (int iter = 0; iter < 50; iter++)
                {
                    sigma = (sigmaMin + sigmaMax) / 2.0;
                    double sum = 0;
                    double entropy = 0;

                    for (int j = 0; j < n; j++)
                    {
                        if (i != j)
                        {
                            double val = Math.Exp(-distances[i, j] * distances[i, j] / (2.0 * sigma * sigma));
                            sum += val;
                            if (val > 1e-10)
                                entropy += val * Math.Log(val);
                        }
                    }

                    entropy = Math.Log(sum) - entropy / sum;

                    if (entropy > targetEntropy)
                        sigmaMin = sigma;
                    else
                        sigmaMax = sigma;
                }

                // Вычисляем P_i|j
                double rowSum = 0;
                for (int j = 0; j < n; j++)
                {
                    if (i != j)
                    {
                        P[i, j] = Math.Exp(-distances[i, j] * distances[i, j] / (2.0 * sigma * sigma));
                        rowSum += P[i, j];
                    }
                }

                for (int j = 0; j < n; j++)
                    if (i != j)
                        P[i, j] /= Math.Max(rowSum, 1e-12);
            }

            // Симметризация
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    P[i, j] = (P[i, j] + P[j, i]) / (2.0 * n);
                    P[j, i] = P[i, j];
                }

            // Усиление структуры
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (i != j && P[i, j] < 1e-8)
                        P[i, j] = 1e-10;

            return P;
        }

        private Dictionary<int, double[]> CalculateClusterCentroids(
            double[][] points2D, int[] assignments)
        {
            var centroids = new Dictionary<int, List<double[]>>();

            for (int i = 0; i < assignments.Length; i++)
            {
                int cluster = assignments[i];
                if (cluster < 0) continue;

                if (!centroids.ContainsKey(cluster))
                    centroids[cluster] = new List<double[]>();

                centroids[cluster].Add(points2D[i]);
            }

            var result = new Dictionary<int, double[]>();
            foreach (var kvp in centroids)
            {
                var points = kvp.Value;
                int count = points.Count;
                double meanX = points.Average(p => p[0]);
                double meanY = points.Average(p => p[1]);
                result[kvp.Key] = new[] { meanX, meanY };
            }

            return result;
        }
    }
}
