using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetalSurfacesDefectsAnalyzer.App.Models
{
    public class AppConfig
    {
        private static AppConfig? _instance;
        private static readonly object _lock = new();

        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new AppConfig();
                    }
                }
                return _instance;
            }
        }

        private AppConfig() { }

        /// <summary>
        /// Выбранный параметр энтропийной регуляризации
        /// </summary>
        public double SelectedEta { get; set; } = 0.04;

        /// <summary>
        /// Текущая тема
        /// </summary>
        public string Theme { get; set; } = "Light";

        /// <summary>
        /// Текущий язык
        /// </summary>
        public string Language { get; set; } = "ru";
    }
}
