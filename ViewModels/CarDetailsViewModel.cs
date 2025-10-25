using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using TaxiWPF.Models;
using TaxiWPF.Repositories;
using System.Runtime.CompilerServices;

namespace TaxiWPF.ViewModels
{
    public class CarDetailsViewModel : INotifyPropertyChanged
    {
        private readonly CarRepository _carRepository;
        private readonly User _driver;
        private Car _car;
        private string _newPhotoUrl;
        public string NewPhotoUrl { get => _newPhotoUrl; set { _newPhotoUrl = value; OnPropertyChanged(); } }

        private bool _isAddMode; // Режим "Добавление" (true) или "Просмотр" (false)
        private string _selectedPhotoUrl;

        // --- Свойства для привязки ---
        public bool IsAddMode { get => _isAddMode; set { _isAddMode = value; OnPropertyChanged(); } }
        public Car Car { get => _car; set { _car = value; OnPropertyChanged(); } }

        // Галерея
        public ObservableCollection<string> PhotoGallery { get; set; }

        // Текущее большое фото
        public string SelectedPhotoUrl
        {
            get => _selectedPhotoUrl;
            set { _selectedPhotoUrl = value; OnPropertyChanged(); }
        }

        public ICommand CloseCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand AddPhotoUrlCommand { get; } // Заглушка
        public ICommand RemovePhotoUrlCommand { get; } // Заглушка
        public ICommand DeleteCarCommand { get; }

        // Событие для закрытия
        public event Action RequestClose;

        // Конструктор
        public CarDetailsViewModel(Car car, User driver)
        {
            _carRepository = new CarRepository();
            _driver = driver;

            // Если переданная машина "пустая" (CarId = 0), это режим добавления
            if (car.CarId == 0)
            {
                IsAddMode = true;
                Car = car; // Работаем с новым пустым объектом
                Car.DriverId = _driver.user_id; // Сразу привязываем к водителю
            }
            else
            {
                IsAddMode = false;
                Car = car; // Работаем с существующим объектом
            }

            // --- Команды ---
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
            SaveCommand = new RelayCommand(SaveCar, CanSaveCar);
            DeleteCarCommand = new RelayCommand(DeleteCar, () => !IsAddMode); // Удалять можно только существующую

            // --- Заглушки для фото ---
            AddPhotoUrlCommand = new RelayCommand(AddPhotoUrl);
            RemovePhotoUrlCommand = new RelayCommand(RemovePhotoUrl, () => !string.IsNullOrEmpty(SelectedPhotoUrl));

            // --- Инициализация галереи ---
            PhotoGallery = new ObservableCollection<string>();
            // Добавляем главное фото
            if (!string.IsNullOrEmpty(Car.MainImageUrl))
            {
                PhotoGallery.Add(Car.MainImageUrl);
            }
            // Добавляем остальные фото
            foreach (var photo in Car.PhotoGallery)
            {
                PhotoGallery.Add(photo);
            }

            // Выбираем первое фото для отображения
            SelectedPhotoUrl = PhotoGallery.FirstOrDefault();
        }

        private bool CanSaveCar()
        {
            // Простая проверка (в заглушке достаточно)
            return !string.IsNullOrEmpty(Car.ModelName) &&
                   !string.IsNullOrEmpty(Car.LicensePlate) &&
                   !string.IsNullOrEmpty(Car.MainImageUrl);
        }

        private void SaveCar()
        {
            if (IsAddMode)
            {
                _carRepository.AddCar(Car);
                MessageBox.Show("Автомобиль успешно добавлен!", "Успех");
            }
            else
            {
                _carRepository.UpdateCar(Car);
                MessageBox.Show("Изменения сохранены!", "Успех");
            }
            RequestClose?.Invoke();
        }

        private void DeleteCar()
        {
            if (MessageBox.Show($"Вы уверены, что хотите удалить {Car.ModelName}?",
                                "Подтверждение",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _carRepository.DeleteCar(Car);
                MessageBox.Show("Автомобиль удален.");
                RequestClose?.Invoke();
            }
        }

        // --- ЗАГЛУШКИ ДЛЯ УПРАВЛЕНИЯ ФОТО ---
        private void AddPhotoUrl()
        {
            if (string.IsNullOrWhiteSpace(NewPhotoUrl))
            {
                MessageBox.Show("Сначала вставьте URL в поле.");
                return;
            }

            // Добавляем и в UI, и в саму модель
            PhotoGallery.Add(NewPhotoUrl);
            Car.PhotoGallery.Add(NewPhotoUrl);

            // Если это первое фото, делаем его главным
            if (string.IsNullOrEmpty(Car.MainImageUrl))
            {
                Car.MainImageUrl = NewPhotoUrl;
                OnPropertyChanged(nameof(Car)); // Обновляем привязку к главному фото
            }
            NewPhotoUrl = ""; // Очищаем TextBox
        }

        private void RemovePhotoUrl()
        {
            if (string.IsNullOrEmpty(SelectedPhotoUrl)) return;

            // Если удаляем главное фото, выбираем следующее
            if (SelectedPhotoUrl == Car.MainImageUrl)
            {
                Car.MainImageUrl = PhotoGallery.FirstOrDefault(url => url != SelectedPhotoUrl);
                OnPropertyChanged(nameof(Car)); // Обновляем привязку
            }

            // Удаляем из модели и из UI
            Car.PhotoGallery.Remove(SelectedPhotoUrl);
            PhotoGallery.Remove(SelectedPhotoUrl);

            SelectedPhotoUrl = PhotoGallery.FirstOrDefault(); // Выбираем первое
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
