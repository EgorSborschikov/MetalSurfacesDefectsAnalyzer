using MetalSurfacesDefectsAnalyzer.App.Models;
using MetalSurfacesDefectsAnalyzer.Core.Config;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Логика взаимодействия для ModelSelectionWindow.xaml
    /// </summary>
    public partial class ModelSelectionWindow : Window
    {
        private double _selectedEta = 0.04;

        public ModelSelectionWindow(double currentEta = 0.04)
        {
            InitializeComponent();
            LoadModels(currentEta);
        }

        private void LoadModels(double currentEta)
        {
            var models = new ObservableCollection<ModelInfo>();

            // Данные из таблицы 2 вашей статьи
            var modelData = new[]
            {
            new { Eta = 0.00, NMI = 0.335, ARI = 0.157, H = 0.337, Type = "Очень жёсткая", Desc = "Базовая VaDE без регуляризации" },
            new { Eta = 0.04, NMI = 0.542, ARI = 0.323, H = 0.379, Type = "Оптимальная", Desc = "Рекомендуется: лучший баланс качества и мягкости" },
            new { Eta = 0.07, NMI = 0.490, ARI = 0.290, H = 0.410, Type = "Мягкая", Desc = "Повышенная энтропия для смешанных принадлежностей" },
        };

            foreach (var data in modelData)
            {
                models.Add(new ModelInfo
                {
                    Eta = data.Eta,
                    NMI = data.NMI,
                    ARI = data.ARI,
                    Entropy = data.H,
                    Type = data.Type,
                    Description = data.Desc,
                    TypeColor = GetTypeColor(data.Type),
                    IsSelected = Math.Abs(data.Eta - currentEta) < 0.001
                });
            }

            LvModels.ItemsSource = models;

            // Выделяем текущую модель
            var currentModel = models.FirstOrDefault(m => m.IsSelected);
            if (currentModel != null)
            {
                LvModels.SelectedItem = currentModel;
                BtnSelect.IsEnabled = true;
            }
        }

        private Brush GetTypeColor(string type)
        {
            return type switch
            {
                "Очень жёсткая" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                "Оптимальная" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                "Умеренно мягкая" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22")),
                "Мягкая" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                "Очень мягкая" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD")),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        private void LvModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvModels.SelectedItem is ModelInfo selected)
            {
                _selectedEta = selected.Eta;
                BtnSelect.IsEnabled = true;
            }
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                $"Выбрать модель с η = {_selectedEta:F2}?\n\n" +
                $"Тип кластеризации: {(LvModels.SelectedItem as ModelInfo)?.Type}\n" +
                $"Будет выполнена перезагрузка модели.",
                "Подтверждение выбора модели",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Сохраняем выбор
                Models.AppConfig.Instance.SelectedEta = _selectedEta;

                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
