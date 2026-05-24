using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Core.Services
{
    public class CacheService : ICacheService
    {
        private readonly string _cacheDirectory;

        public CacheService()
        {
            _cacheDirectory = Path.Combine(Path.GetTempPath(), "MetalDefectsAnalyzer", "Models");
        }

        /// <summary>
        /// Получить размер кэша
        /// </summary>
        public long GetCacheSize()
        {
            if (!Directory.Exists(_cacheDirectory))
                return 0;

            try
            {
                return Directory.GetFiles(_cacheDirectory, "*.*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Получить путь к кэшу
        /// </summary>
        public string GetCachePath()
        {
            return _cacheDirectory;
        }

        /// <summary>
        /// Очистить кэш
        /// </summary>
        public async Task ClearCacheAsync()
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(_cacheDirectory))
                    return;

                try
                {
                    // Удаляем все файлы моделей
                    var files = Directory.GetFiles(_cacheDirectory, "*.onnx*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Не удалось удалить файл {file}: {ex.Message}");
                        }
                    }

                    // Удаляем пустые папки
                    var directories = Directory.GetDirectories(_cacheDirectory, "*", SearchOption.AllDirectories)
                        .OrderByDescending(d => d.Length); // Сначала глубокие

                    foreach (var dir in directories)
                    {
                        try
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                                Directory.Delete(dir);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Не удалось удалить папку {dir}: {ex.Message}");
                        }
                    }

                    // Пытаемся удалить корневую папку кэша если она пуста
                    try
                    {
                        if (Directory.Exists(_cacheDirectory) &&
                            !Directory.EnumerateFileSystemEntries(_cacheDirectory).Any())
                        {
                            Directory.Delete(_cacheDirectory);
                        }
                    }
                    catch { /* игнорируем */ }

                    System.Diagnostics.Debug.WriteLine("Кэш успешно очищен");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка очистки кэша: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Получить информацию о кэше
        /// </summary>
        public CacheInfo GetCacheInfo()
        {
            int fileCount = 0;

            if (Directory.Exists(_cacheDirectory))
            {
                try
                {
                    fileCount = Directory.GetFiles(_cacheDirectory, "*.*", SearchOption.AllDirectories).Length;
                }
                catch { }
            }

            return new CacheInfo
            {
                CachePath = _cacheDirectory,
                CacheSizeBytes = GetCacheSize(),
                FileCount = fileCount,
                LastCleanup = DateTime.Now
            };
        }
    }
}
