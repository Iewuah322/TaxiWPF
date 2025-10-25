using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiWPF.Models
{
    public class Car
    {
        public int CarId { get; set; }
        public int DriverId { get; set; } // К какому водителю привязана

        public string ModelName { get; set; } // "Kia Rio"
        public string LicensePlate { get; set; } // "A123BC77"
        public string MainImageUrl { get; set; } // URL или путь к фото

        // --- Поля для "Деталей" (на будущее) ---
        public string EngineInfo { get; set; } // "1.6 MPI"
        public string InsuranceInfo { get; set; } // "РОСГОССТРАХ до 10.2025"
        public List<string> PhotoGallery { get; set; } // Список URL

        public Car()
        {
            PhotoGallery = new List<string>();
        }
    }
}