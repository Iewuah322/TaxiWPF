using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TaxiWPF.Models
{
    public class Car : INotifyPropertyChanged
    {
        public int CarId { get; set; }
        public int DriverId { get; set; } // К какому водителю привязана

        public string ModelName { get; set; } // "Kia Rio"
        public string LicensePlate { get; set; } // "A123BC77"
        public string MainImageUrl { get; set; } // URL или путь к фото
        
        // Дополнительные поля для UI
        public string Color { get; set; } = "Не указан";
        public string Tariff { get; set; } = "Эконом";
        public string PhotoPath => MainImageUrl; // Алиас для конвертера

        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { _isSelected = value; OnPropertyChanged(); }
        }

        // --- Поля для "Деталей" (на будущее) ---
        public string EngineInfo { get; set; } // "1.6 MPI"
        public string InsuranceInfo { get; set; } // "РОСГОССТРАХ до 10.2025"
        public List<string> PhotoGallery { get; set; } // Список URL

        public Car()
        {
            PhotoGallery = new List<string>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}