using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Core.Services
{
    internal static class ModelLoader
    {
        private static string ModelsDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "MetalSurfacesDefectsAnalyzer.Core", "Resources");

        public static InferenceSession LoadModel(string modelName)
        {
            var modelPath = Path.Combine(ModelsDirectory, modelName);

            System.Diagnostics.Debug.WriteLine($"[ModelLoader] Загрузка модели: {modelPath}");
            System.Diagnostics.Debug.WriteLine($"[ModelLoader] Файл существует: {File.Exists(modelPath)}");

            if (!File.Exists(modelPath))
            {
                // Проверяем, какие модели доступны
                var availableModels = Directory.Exists(ModelsDirectory)
                    ? Directory.GetFiles(ModelsDirectory, "*.onnx")
                        .Select(f => Path.GetFileName(f))
                        .ToList()
                    : new List<string>();

                throw new FileNotFoundException(
                    $"Модель '{modelName}' не найдена.\n" +
                    $"Путь: {modelPath}\n" +
                    $"Папка моделей: {ModelsDirectory}\n" +
                    $"Доступные модели: {(availableModels.Any() ? string.Join(", ", availableModels) : "папка пуста или не существует")}");
            }

            System.Diagnostics.Debug.WriteLine($"[ModelLoader] Загрузка модели: {modelPath}");

            try
            {
                var session = new InferenceSession(modelPath);
                System.Diagnostics.Debug.WriteLine($"[ModelLoader] Модель загружена успешно");
                return session;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelLoader] Ошибка загрузки: {ex.Message}");
                throw;
            }
        }
    }
}
