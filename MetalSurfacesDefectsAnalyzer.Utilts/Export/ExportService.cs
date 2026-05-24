using MetalSurfacesDefectsAnalyzer.Core.Structs;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Utilts.Export
{
    public class ExportService : IExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // JSON
        public async Task ExportToJsonAsync(AnalysisResult result, string filePath)
        {
            var exportData = new
            {
                model = result.ModelId,
                etaParameter = result.EtaParameter,
                analysisTime = result.AnalysisTime.ToString("yyyy-MM-dd HH:mm:ss"),
                totalImages = result.TotalImages,
                processedImages = result.ProcessedImages,
                numberOfClusters = 12,
                results = result.Results.Select(r => new
                {
                    fileName = r.FileName,
                    primaryCluster = r.PrimaryCluster,
                    confidence = Math.Round(r.Confidence, 4),
                    uncertainty = Math.Round(r.Uncertainty, 4),
                    entropy = Math.Round(r.Uncertainty * Math.Log(12), 4),
                    clusterProbabilities = r.ClusterProbabilities
                        .Select((p, i) => new { cluster = i, probability = Math.Round(p, 4) })
                        .OrderByDescending(x => x.probability)
                        .ToList(),
                    latentVector = r.LatentVector?.Take(8).Select(v => Math.Round(v, 4)).ToList()
                }).ToList()
            };

            var json = JsonSerializer.Serialize(exportData, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        }

        // CSV
        public async Task ExportToCsvAsync(AnalysisResult result, string filePath)
        {
            var sb = new StringBuilder();

            // Заголовок
            sb.AppendLine("# Результаты кластеризации дефектов металлических поверхностей");
            sb.AppendLine($"# Модель: {result.ModelId}");
            sb.AppendLine($"# Параметр η: {result.EtaParameter}");
            sb.AppendLine($"# Время анализа: {result.AnalysisTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"# Всего изображений: {result.TotalImages}");
            sb.AppendLine($"# Обработано: {result.ProcessedImages}");
            sb.AppendLine();

            // Основная таблица
            sb.Append("FileName,PrimaryCluster,Confidence,Uncertainty,Entropy");
            for (int i = 0; i < 12; i++)
                sb.Append($",Prob_Cluster_{i}");
            sb.AppendLine(",Top3_Clusters");

            foreach (var r in result.Results)
            {
                sb.Append($"\"{r.FileName}\"");
                sb.Append($",{r.PrimaryCluster}");
                sb.Append($",{r.Confidence:F4}");
                sb.Append($",{r.Uncertainty:F4}");
                sb.Append($",{r.Uncertainty * Math.Log(12):F4}");

                // Вероятности для всех кластеров
                for (int i = 0; i < 12; i++)
                {
                    float prob = i < r.ClusterProbabilities.Length ? r.ClusterProbabilities[i] : 0f;
                    sb.Append($",{prob:F4}");
                }

                // Топ-3 кластера
                var top3 = r.ClusterProbabilities
                    .Select((p, i) => new { Cluster = i, Probability = p })
                    .OrderByDescending(x => x.Probability)
                    .Take(3)
                    .Select(x => $"K{x.Cluster}({x.Probability:P1})");

                sb.AppendLine($",\"{string.Join("; ", top3)}\"");
            }

            sb.AppendLine();

            // Сводка по кластерам
            sb.AppendLine("# Сводка по кластерам");
            sb.AppendLine("Cluster,ImageCount,AvgConfidence,AvgEntropy,HighConfidenceRatio");

            for (int c = 0; c < 12; c++)
            {
                var clusterResults = result.Results
                    .Where(r => r.ClusterProbabilities.Length > c && r.ClusterProbabilities[c] > 0.01f)
                    .ToList();

                if (clusterResults.Count == 0)
                {
                    sb.AppendLine($"{c},0,0,0,0");
                    continue;
                }

                float totalWeight = clusterResults.Sum(r => r.ClusterProbabilities[c]);
                float avgConf = clusterResults.Sum(r => r.Confidence * r.ClusterProbabilities[c]) / totalWeight;
                float avgEnt = clusterResults.Sum(r => r.Uncertainty * r.ClusterProbabilities[c]) / totalWeight;
                float highConf = clusterResults
                    .Where(r => r.Confidence > 0.8f && r.ClusterProbabilities[c] > 0.5f)
                    .Sum(r => r.ClusterProbabilities[c]) / totalWeight;

                sb.AppendLine($"{c},{clusterResults.Count},{avgConf:F4},{avgEnt:F4},{highConf:F4}");
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        }

        // PDF
        public async Task ExportToPdfAsync(AnalysisResult result, string filePath, byte[]? tSneImage = null, byte[]? umapImage = null)
        {
            var html = GenerateHtmlReport(result, tSneImage != null, umapImage != null);
            await GeneratePdfFromHtml(html, filePath, tSneImage, umapImage, result);
        }

        // Промежуточное состояние - HTML
        private string GenerateHtmlReport(AnalysisResult result, bool hasTSne, bool hasUMap)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'>");
            sb.AppendLine("<title>Результаты анализа дефектов</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("h1 { color: #2C3E50; }");
            sb.AppendLine("h2 { color: #34495E; margin-top: 30px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 15px 0; }");
            sb.AppendLine("th, td { border: 1px solid #BDC3C7; padding: 8px 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #ECF0F1; font-weight: bold; }");
            sb.AppendLine(".cluster-bar { display: inline-block; height: 18px; background: #3498DB; margin: 2px; }");
            sb.AppendLine(".high-conf { color: #27AE60; font-weight: bold; }");
            sb.AppendLine(".low-conf { color: #E74C3C; }");
            sb.AppendLine(".summary-box { background: #F8F9FA; padding: 15px; border-radius: 5px; margin: 15px 0; }");
            sb.AppendLine("img { max-width: 100%; height: auto; margin: 10px 0; }");
            sb.AppendLine("</style></head><body>");

            // Заголовок
            sb.AppendLine($"<h1>Результаты кластеризации дефектов</h1>");
            sb.AppendLine($"<div class='summary-box'>");
            sb.AppendLine($"<strong>Модель:</strong> {result.ModelId}<br>");
            sb.AppendLine($"<strong>Параметр энтропийной регуляризации η:</strong> {result.EtaParameter}<br>");
            sb.AppendLine($"<strong>Время анализа:</strong> {result.AnalysisTime:yyyy-MM-dd HH:mm:ss}<br>");
            sb.AppendLine($"<strong>Всего изображений:</strong> {result.TotalImages}<br>");
            sb.AppendLine($"<strong>Успешно обработано:</strong> {result.ProcessedImages}<br>");
            sb.AppendLine($"<strong>Количество кластеров:</strong> 12<br>");
            sb.AppendLine($"</div>");

            // Таблица результатов
            sb.AppendLine("<h2>Результаты кластеризации</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Файл</th><th>Кластер</th><th>Уверенность</th><th>Энтропия H(γ)</th><th>Неопределённость</th><th>Топ-3 кластера</th></tr>");

            foreach (var r in result.Results)
            {
                string confClass = r.Confidence > 0.7 ? "high-conf" : (r.Confidence < 0.3 ? "low-conf" : "");

                var top3 = r.ClusterProbabilities
                    .Select((p, i) => new { Cluster = i, Probability = p })
                    .OrderByDescending(x => x.Probability)
                    .Take(3)
                    .Select(x => $"K{x.Cluster}: {x.Probability:P1}");

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{r.FileName}</td>");
                sb.AppendLine($"<td>{r.PrimaryCluster}</td>");
                sb.AppendLine($"<td class='{confClass}'>{r.Confidence:P1}</td>");
                sb.AppendLine($"<td>{r.Uncertainty * Math.Log(12):F3}</td>");
                sb.AppendLine($"<td>{r.Uncertainty:F3}</td>");
                sb.AppendLine($"<td>{string.Join("; ", top3)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");

            // Детальные распределения вероятностей
            sb.AppendLine("<h2>Распределение вероятностей по кластерам</h2>");

            foreach (var r in result.Results.Take(20)) // Ограничим для PDF
            {
                sb.AppendLine($"<h3>{r.FileName}</h3>");
                sb.AppendLine("<div style='margin: 10px 0;'>");

                var sorted = r.ClusterProbabilities
                    .Select((p, i) => new { Cluster = i, Probability = p })
                    .OrderByDescending(x => x.Probability);

                foreach (var item in sorted)
                {
                    int width = (int)(item.Probability * 200);
                    sb.AppendLine($"<span>K{item.Cluster}: </span>");
                    sb.AppendLine($"<span class='cluster-bar' style='width:{width}px'></span>");
                    sb.AppendLine($"<span>{item.Probability:P1}</span><br>");
                }

                sb.AppendLine("</div>");
            }

            // Визуализации
            if (hasTSne || hasUMap)
            {
                sb.AppendLine("<h2>Визуализация латентного пространства</h2>");

                if (hasTSne)
                {
                    sb.AppendLine("<h3>t-SNE проекция</h3>");
                    sb.AppendLine("<img src='data:image/png;base64,{{TSNE_IMAGE}}' alt='t-SNE'>");
                }

                if (hasUMap)
                {
                    sb.AppendLine("<h3>UMAP проекция</h3>");
                    sb.AppendLine("<img src='data:image/png;base64,{{UMAP_IMAGE}}' alt='UMAP'>");
                }
            }

            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        // Состояние HTML -> PDF
        private async Task GeneratePdfFromHtml(string html, string pdfPath, byte[]? tSneImage, byte[]? umapImage, AnalysisResult result)
        {
            await Task.Run(() =>
            {
                try
                {
                    var document = new PdfDocument();
                    document.Info.Title = "Результаты анализа дефектов";
                    document.Info.Author = "Metal Surfaces Defects Analyzer";

                    // Страница 1: Сводка и таблица результатов
                    var page = document.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    var gfx = XGraphics.FromPdfPage(page);

                    var fontTitle = new XFont("Arial", 18, XFontStyleEx.Bold);
                    var fontHeader = new XFont("Arial", 14, XFontStyleEx.Bold);
                    var fontNormal = new XFont("Arial", 10, XFontStyleEx.Regular);
                    var fontSmall = new XFont("Arial", 8, XFontStyleEx.Regular);
                    var fontBold = new XFont("Arial", 10, XFontStyleEx.Bold);

                    double y = 40;
                    double x = 40;
                    double pageWidth = page.Width - 80;

                    // Заголовок
                    gfx.DrawString("Результаты кластеризации дефектов", fontTitle, XBrushes.Black,
                        new XRect(x, y, pageWidth, 30), XStringFormats.TopLeft);
                    y += 35;

                    // Информация о модели
                    gfx.DrawString($"Модель: {result.ModelId}  |  η = {result.EtaParameter}  |  {result.AnalysisTime:yyyy-MM-dd HH:mm:ss}",
                        fontSmall, XBrushes.Gray, new XRect(x, y, pageWidth, 20), XStringFormats.TopLeft);
                    y += 20;

                    gfx.DrawString($"Всего изображений: {result.TotalImages}  |  Обработано: {result.ProcessedImages}  |  Кластеров: 12",
                        fontSmall, XBrushes.Gray, new XRect(x, y, pageWidth, 20), XStringFormats.TopLeft);
                    y += 30;

                    // Таблица результатов
                    DrawResultsTable(gfx, ref y, x, pageWidth, result, fontBold, fontNormal, fontSmall);

                    // Страницы для детальных распределений
                    foreach (var clusterResult in result.Results)
                    {
                        if (y > page.Height - 200)
                        {
                            page = document.AddPage();
                            page.Size = PdfSharp.PageSize.A4;
                            gfx = XGraphics.FromPdfPage(page);
                            y = 40;
                        }

                        DrawClusterDistribution(gfx, ref y, x, pageWidth, clusterResult, fontHeader, fontNormal, fontSmall, document);
                    }

                    // Визуализации
                    if (tSneImage != null || umapImage != null)
                    {
                        page = document.AddPage();
                        page.Size = PdfSharp.PageSize.A4;
                        gfx = XGraphics.FromPdfPage(page);
                        y = 40;

                        gfx.DrawString("Визуализация латентного пространства", fontHeader, XBrushes.Black,
                            new XRect(x, y, pageWidth, 25), XStringFormats.TopLeft);
                        y += 35;

                        if (tSneImage != null)
                        {
                            gfx.DrawString("t-SNE проекция", fontBold, XBrushes.Black,
                                new XRect(x, y, pageWidth, 20), XStringFormats.TopLeft);
                            y += 25;

                            using var ms = new MemoryStream(tSneImage);
                            var image = XImage.FromStream(ms);

                            double imgWidth = pageWidth;
                            double imgHeight = image.PixelHeight * (imgWidth / image.PixelWidth);

                            gfx.DrawImage(image, x, y, imgWidth, imgHeight);
                            y += imgHeight + 20;
                        }

                        if (umapImage != null)
                        {
                            if (y > page.Height - 200)
                            {
                                page = document.AddPage();
                                page.Size = PdfSharp.PageSize.A4;
                                gfx = XGraphics.FromPdfPage(page);
                                y = 40;
                            }

                            gfx.DrawString("UMAP проекция", fontBold, XBrushes.Black,
                                new XRect(x, y, pageWidth, 20), XStringFormats.TopLeft);
                            y += 25;

                            using var ms = new MemoryStream(umapImage);
                            var image = XImage.FromStream(ms);

                            double imgWidth = pageWidth;
                            double imgHeight = image.PixelHeight * (imgWidth / image.PixelWidth);

                            gfx.DrawImage(image, x, y, imgWidth, imgHeight);
                        }
                    }

                    document.Save(pdfPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка PDF: {ex.Message}");
                    // Сохраняем HTML как резервный вариант
                    var htmlPath = Path.ChangeExtension(pdfPath, ".html");
                    File.WriteAllText(htmlPath, html, Encoding.UTF8);
                }
            });
        }

        private void DrawResultsTable(XGraphics gfx, ref double y, double x, double pageWidth,
            AnalysisResult result, XFont fontBold, XFont fontNormal, XFont fontSmall)
        {
            // Заголовок таблицы
            gfx.DrawString("Результаты кластеризации", fontBold, XBrushes.Black,
                new XRect(x, y, pageWidth, 20), XStringFormats.TopLeft);
            y += 25;

            // Шапка таблицы
            double[] colWidths = { 120, 40, 70, 70, 70 };
            string[] headers = { "Файл", "Кластер", "Уверенность", "Энтропия", "Неопред." };

            double tableX = x;
            double rowHeight = 20;

            // Фон шапки
            gfx.DrawRectangle(XBrushes.LightGray, tableX, y, colWidths.Sum(), rowHeight);

            double colX = tableX;
            for (int i = 0; i < headers.Length; i++)
            {
                gfx.DrawString(headers[i], fontBold, XBrushes.Black,
                    new XRect(colX, y, colWidths[i], rowHeight), XStringFormats.CenterLeft);
                colX += colWidths[i];
            }
            y += rowHeight;

            // Данные
            foreach (var r in result.Results.Take(50)) // Ограничим для читаемости
            {
                if (y > 750) break; // Не выходим за пределы страницы

                colX = tableX;

                gfx.DrawString(TruncateText(r.FileName, 20), fontSmall, XBrushes.Black,
                    new XRect(colX, y, colWidths[0], rowHeight), XStringFormats.CenterLeft);
                colX += colWidths[0];

                gfx.DrawString(r.PrimaryCluster.ToString(), fontSmall, XBrushes.Black,
                    new XRect(colX, y, colWidths[1], rowHeight), XStringFormats.Center);
                colX += colWidths[1];

                gfx.DrawString($"{r.Confidence:P1}", fontSmall, XBrushes.Black,
                    new XRect(colX, y, colWidths[2], rowHeight), XStringFormats.Center);
                colX += colWidths[2];

                gfx.DrawString($"{r.Uncertainty * Math.Log(12):F3}", fontSmall, XBrushes.Black,
                    new XRect(colX, y, colWidths[3], rowHeight), XStringFormats.Center);
                colX += colWidths[3];

                gfx.DrawString($"{r.Uncertainty:F3}", fontSmall, XBrushes.Black,
                    new XRect(colX, y, colWidths[4], rowHeight), XStringFormats.Center);

                y += rowHeight;

                // Линия между строками
                gfx.DrawLine(XPens.LightGray, tableX, y, tableX + colWidths.Sum(), y);
            }

            y += 10;
        }

        private void DrawClusterDistribution(XGraphics gfx, ref double y, double x, double pageWidth,
            ClusterResult result, XFont fontHeader, XFont fontNormal, XFont fontSmall, PdfDocument document)
        {
            gfx.DrawString($"Распределение: {result.FileName}", fontHeader, XBrushes.Black,
                new XRect(x, y, pageWidth, 22), XStringFormats.TopLeft);
            y += 28;

            gfx.DrawString($"Кластер {result.PrimaryCluster}  |  Уверенность: {result.Confidence:P1}  |  H(γ): {result.Uncertainty * Math.Log(12):F3}",
                fontSmall, XBrushes.Gray, new XRect(x, y, pageWidth, 18), XStringFormats.TopLeft);
            y += 25;

            var sorted = result.ClusterProbabilities
                .Select((p, i) => new { Cluster = i, Probability = p })
                .OrderByDescending(item => item.Probability);

            double barMaxWidth = pageWidth - 100;
            double barHeight = 15;
            double barSpacing = 5;

            foreach (var item in sorted)
            {
                // Проверяем, нужна ли новая страница
                if (y > 750)
                {
                    var newPage = document.AddPage();
                    newPage.Size = PdfSharp.PageSize.A4;
                    gfx = XGraphics.FromPdfPage(newPage);
                    y = 40;
                }

                // Метка кластера
                gfx.DrawString($"K{item.Cluster}", fontSmall, XBrushes.Black,
                    new XRect(x, y, 40, barHeight), XStringFormats.CenterLeft);

                // Фон бара
                gfx.DrawRectangle(XBrushes.LightGray, x + 45, y, barMaxWidth, barHeight);

                // Заполнение бара
                double barWidth = item.Probability * barMaxWidth;
                if (barWidth > 0)
                {
                    var brush = GetClusterBrush(item.Cluster);
                    gfx.DrawRectangle(brush, x + 45, y, barWidth, barHeight);
                }

                // Процент
                gfx.DrawString($"{item.Probability:P1}", fontSmall, XBrushes.Black,
                    new XRect(x + 50 + barMaxWidth, y, 45, barHeight), XStringFormats.CenterLeft);

                y += barHeight + barSpacing;
            }

            y += 15;
        }

        private XBrush GetClusterBrush(int clusterId)
        {
            int colorIndex = clusterId % 12;

            switch (colorIndex)
            {
                case 0: return XBrushes.Blue;
                case 1: return XBrushes.Orange;
                case 2: return XBrushes.Green;
                case 3: return XBrushes.Red;
                case 4: return XBrushes.Purple;
                case 5: return XBrushes.Brown;
                case 6: return XBrushes.Pink;
                case 7: return XBrushes.Gray;
                case 8: return XBrushes.Gold;
                case 9: return XBrushes.Cyan;
                case 10: return XBrushes.LightBlue;
                case 11: return XBrushes.LightCoral;
                default: return XBrushes.Black;
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            return text.Length > maxLength ? text[..(maxLength - 3)] + "..." : text;
        }
    }
}
