using MetalSurfacesDefectsAnalyzer.Core.Structs;
using System.Collections.Generic;

namespace MetalSurfacesDefectsAnalyzer.App.Models
{
    public class AppState
    {
        private static AppState? _instance;
        private static readonly object _lock = new();

        public static AppState Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new AppState();
                    }
                }
                return _instance;
            }
        }

        private AppState() { }

        /// <summary>
        /// Загруженные изображения для анализа
        /// </summary>
        public List<ImageFileInfo> LoadedImages { get; set; } = new();

        /// <summary>
        /// Результаты текущего анализа
        /// </summary>
        public AnalysisResult? CurrentAnalysisResult { get; set; }

        /// <summary>
        /// Имеются ли загруженные изображения
        /// </summary>
        public bool HasImages => LoadedImages?.Count > 0;

        /// <summary>
        /// Выполнен ли анализ
        /// </summary>
        public bool HasAnalysisResult => CurrentAnalysisResult != null;

        /// <summary>
        /// Получить результат для конкретного изображения по имени файла
        /// </summary>
        public ClusterResult? GetResultForImage(string fileName)
        {
            return CurrentAnalysisResult?.Results
                .FirstOrDefault(r => r.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Полностью очистить кэш и состояние приложения
        /// </summary>
        public async Task ClearAllAsync()
        {
            ClearState();

            // Очищаем кэш моделей
            var cacheService = new Core.Services.CacheService();
            await cacheService.ClearCacheAsync();

            // Принудительная сборка мусора
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Очистка всего состояния
        /// </summary>
        public void ClearState()
        {
            if (LoadedImages != null)
            {
                foreach (var image in LoadedImages)
                {
                    image?.Dispose();
                }
                LoadedImages.Clear();
            }

            CurrentAnalysisResult = null;
        }
    }
}
