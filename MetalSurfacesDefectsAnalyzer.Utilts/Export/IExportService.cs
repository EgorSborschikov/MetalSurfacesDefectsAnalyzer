using MetalSurfacesDefectsAnalyzer.Core.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.Utilts.Export
{
    public interface IExportService
    {
        // JSON
        Task ExportToJsonAsync(AnalysisResult result, string filePath);

        // CSV
        Task ExportToCsvAsync(AnalysisResult result, string filePath);

        // PDF
        Task ExportToPdfAsync(AnalysisResult result, string filePath, byte[]? tSneImage = null, byte[]? umapImage = null);
    }
}
