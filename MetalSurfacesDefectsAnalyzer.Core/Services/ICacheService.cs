using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Core.Services
{
    public interface ICacheService
    {
        long GetCacheSize();

        string GetCachePath();

        Task ClearCacheAsync();

        CacheInfo GetCacheInfo();
    }

    public class CacheInfo
    {
        public string CachePath { get; init; } = string.Empty;
        public long CacheSizeBytes { get; init; }
        public int FileCount { get; init; }
        public DateTime LastCleanup { get; init; }
        public string CacheSizeFormatted => FormatSize(CacheSizeBytes);

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
