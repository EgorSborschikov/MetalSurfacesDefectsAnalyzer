using System;
using System.Collections.Generic;
using System.IO;
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
    /// Логика взаимодействия для HelpWindow.xaml
    /// </summary>
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        private void OnUserGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Путь к PDF в папке приложения
                var pdfPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Resources", "UserGuide.pdf");

                if (File.Exists(pdfPath))
                {
                    // Открываем PDF в программе по умолчанию
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = pdfPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Пробуем найти в ресурсах и сохранить во временную папку
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var resourceName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(r => r.EndsWith("UserGuide.pdf"));

                    if (resourceName != null)
                    {
                        var tempPath = System.IO.Path.Combine(
                            System.IO.Path.GetTempPath(),
                            "UserGuide.pdf");

                        using (var stream = assembly.GetManifestResourceStream(resourceName))
                        using (var fileStream = File.Create(tempPath))
                        {
                            stream?.CopyTo(fileStream);
                        }

                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = tempPath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show(
                            "Файл руководства пользователя не найден.",
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось открыть руководство пользователя:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
