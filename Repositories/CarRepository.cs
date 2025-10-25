using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiWPF.Models;

namespace TaxiWPF.Repositories
{
    public class CarRepository
    {
        // --- ЗАГЛУШКА БД ---
        private static List<Car> _inMemoryCars = new List<Car>
        {
            new Car
            {
                CarId = 1, DriverId = 2, // 2 - ID твоего test_driver
                ModelName = "Kia Rio (Белая)",
                LicensePlate = "A 123 AA 77",
                // Используем "pack" URI для изображений, которые лежат в проекте
                // (Тебе нужно будет добавить папку 'Assets' и кинуть туда фотки)
                // Либо используй полные URL из интернета: "https://.../image.png"
                MainImageUrl = "https://avatars.mds.yandex.net/get-autoru-vos/4559796/10a86c493ee7f90a9301ea675c37fdd0/1200x900",
                EngineInfo = "1.6л, 123 л.с.",
                InsuranceInfo = "Активна до 12.2025"
            },
            new Car
            {
                CarId = 2, DriverId = 2,
                ModelName = "Lada Vesta (Черная)",
                LicensePlate = "B 456 BB 77",
                MainImageUrl = "https://motor.ru/imgs/2023/11/28/09/6246686/4457420b454590c34e7825b25414a0f79392f520.jpg",
                EngineInfo = "1.8л, 122 л.с.",
                InsuranceInfo = "Активна до 05.2026"
            }
        };
        // ------------------

        // Получаем все машины водителя
        public List<Car> GetCarsByDriverId(int driverId)
        {
            // Игнорируем ID в заглушке и просто возвращаем все
            return _inMemoryCars;
        }

        // (Метод для будущей кнопки "Добавить")
        public void AddCar(Car car)
        {
            // В заглушке мы просто добавляем в список
            car.CarId = _inMemoryCars.Any() ? _inMemoryCars.Max(c => c.CarId) + 1 : 1;

            // (В реальном приложении ты бы не сохранял URL из галереи отдельно,
            // но в заглушке это нормально, чтобы они появились в 'Деталях')
            foreach (var photoUrl in car.PhotoGallery)
            {
                if (photoUrl != car.MainImageUrl)
                {
                    car.PhotoGallery.Add(photoUrl);
                }
            }

            _inMemoryCars.Add(car);
        }
        public void UpdateCar(Car carToUpdate)
        {
            // Находим старую машину в "БД" по ID
            var existingCar = _inMemoryCars.FirstOrDefault(c => c.CarId == carToUpdate.CarId);
            if (existingCar != null)
            {
                // Обновляем ее данные (в реальной БД это был бы UPDATE)
                existingCar.ModelName = carToUpdate.ModelName;
                existingCar.LicensePlate = carToUpdate.LicensePlate;
                existingCar.EngineInfo = carToUpdate.EngineInfo;
                existingCar.InsuranceInfo = carToUpdate.InsuranceInfo;
                existingCar.MainImageUrl = carToUpdate.MainImageUrl;
                existingCar.PhotoGallery = carToUpdate.PhotoGallery;
            }
        }

        public void DeleteCar(Car carToDelete)
        {
            var existingCar = _inMemoryCars.FirstOrDefault(c => c.CarId == carToDelete.CarId);
            if (existingCar != null)
            {
                _inMemoryCars.Remove(existingCar);
            }
        }
    }
}
