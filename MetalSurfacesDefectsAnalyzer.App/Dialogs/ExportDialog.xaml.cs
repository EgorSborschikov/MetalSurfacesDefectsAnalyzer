using MetalSurfacesDefectsAnalyzer.App.Models;
using MetalSurfacesDefectsAnalyzer.Utilts.Export;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MetalSurfacesDefectsAnalyzer.App.Dialogs
{
    /// <summary>
    /// Логика взаимодействия для ExportDialog.xaml
    /// </summary>
    public partial class ExportDialog : Window
    {
        private readonly IExportService _exportService;

        public ExportDialog()
        {
            InitializeComponent();
            _exportService = new ExportService();

            RbPdf.Checked += (s, e) => CbIncludeVisualizations.IsEnabled = true;
            RbPdf.Unchecked += (s, e) => CbIncludeVisualizations.IsEnabled = false;
            RbJson.Checked += (s, e) => CbIncludeVisualizations.IsEnabled = false;
            RbCsv.Checked += (s, e) => CbIncludeVisualizations.IsEnabled = false;
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var result = AppState.Instance.CurrentAnalysisResult;
            if (result == null)
            {
                MessageBox.Show("Нет результатов для экспорта.", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string format = RbJson.IsChecked == true ? "JSON" :
                           RbCsv.IsChecked == true ? "CSV" : "PDF";

            string filter = format switch
            {
                "JSON" => "JSON files (*.json)|*.json",
                "CSV" => "CSV files (*.csv)|*.csv",
                "PDF" => "PDF files (*.pdf)|*.pdf",
                _ => "All files (*.*)|*.*"
            };

            var dialog = new SaveFileDialog
            {
                Title = $"Экспорт результатов в {format}",
                Filter = filter,
                FileName = $"analysis_results_{DateTime.Now:yyyyMMdd_HHmmss}.{format.ToLower()}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                    switch (format)
                    {
                        case "JSON":
                            await _exportService.ExportToJsonAsync(result, dialog.FileName);
                            break;
                        case "CSV":
                            await _exportService.ExportToCsvAsync(result, dialog.FileName);
                            break;
                        case "PDF":
                            byte[]? tsneImage = null;
                            byte[]? umapImage = null;

                            if (CbIncludeVisualizations.IsChecked == true)
                            {
                                // Здесь нужно получить изображения из VisualizationPage
                                // Пока экспортируем без них
                            }

                            await _exportService.ExportToPdfAsync(result, dialog.FileName, tsneImage, umapImage);
                            break;
                    }

                    Mouse.OverrideCursor = null;

                    MessageBox.Show($"Результаты успешно экспортированы в {format}:\n{dialog.FileName}",
                                  "Экспорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    Mouse.OverrideCursor = null;
                    MessageBox.Show($"Ошибка экспорта:\n{ex.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
