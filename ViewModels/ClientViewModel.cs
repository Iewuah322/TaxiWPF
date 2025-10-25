using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GMap.NET;
using TaxiWPF.Models;

namespace TaxiWPF.ViewModels
{
    public class ClientViewModel : INotifyPropertyChanged
    {
        private string _fromAddress;
        private string _toAddress;
        private string _selectedTariff;
        private decimal _totalPrice;
        private string _statusMessage;
        private bool _isSearching;
        private PointLatLng _pointA;
        private PointLatLng _pointB;
        private double _distanceKm;

        public string FromAddress
        {
            get => _fromAddress;
            set
            {
                _fromAddress = value;
                OnPropertyChanged();
                // Не будем запускать поиск по каждому изменению, а только по потере фокуса
                // Это мы настроим в XAML
            }
        }

        public ICommand UpdatePointACommand { get; }
        public ICommand UpdatePointBCommand { get; }

        public PointLatLng PointA
        {
            get => _pointA;
            set
            {
                _pointA = value;
                OnPropertyChanged();
                UpdateAddressFromPoint(value, (address) => FromAddress = address).ConfigureAwait(false);
                RecalculatePrice();
            }
        }

        public PointLatLng PointB
        {
            get => _pointB;
            set
            {
                _pointB = value;
                OnPropertyChanged();
                UpdateAddressFromPoint(value, (address) => ToAddress = address).ConfigureAwait(false);
                RecalculatePrice();
            }
        }

        public double DistanceKm
        {
            get => _distanceKm;
            set { _distanceKm = value; OnPropertyChanged(); }
        }

       

        public string ToAddress
        {
            get => _toAddress;
            set
            {
                _toAddress = value;
                OnPropertyChanged();
            }
        }

        public string SelectedTariff
        {
            get => _selectedTariff;
            set { _selectedTariff = value; OnPropertyChanged(); RecalculatePrice(); }
        }

        public decimal TotalPrice
        {
            get => _totalPrice;
            set { _totalPrice = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set { _isSearching = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Tariffs { get; set; }
        public ICommand FindTaxiCommand { get; }

        public ClientViewModel()
        {
            // --- ЗАГЛУШКА ДАННЫХ ---
            // В будущем эти данные будут загружаться из репозитория
            Tariffs = new ObservableCollection<string> { "Эконом", "Комфорт", "Бизнес" };
            SelectedTariff = Tariffs[0];
            // ------------------------

            UpdatePointACommand = new RelayCommand(async () => await UpdatePointFromAddress(FromAddress, p => PointA = p));
            UpdatePointBCommand = new RelayCommand(async () => await UpdatePointFromAddress(ToAddress, p => PointB = p));

            FindTaxiCommand = new RelayCommand(async () => await FindTaxi(), () => !IsSearching && !string.IsNullOrWhiteSpace(FromAddress) && !string.IsNullOrWhiteSpace(ToAddress));
        }

        private void RecalculatePrice()
        {
            if (PointA.IsEmpty || PointB.IsEmpty)
            {
                TotalPrice = 0;
                return;
            }

            // Получаем маршрут
            var route = GMap.NET.MapProviders.OpenStreetMapProvider.Instance.GetRoute(PointA, PointB, false, false, 15);
            if (route != null)
            {
                DistanceKm = route.Distance; // Расстояние в км

                decimal basePrice = 50; // Цена за подачу
                decimal pricePerKm = 15; // Цена за километр
                decimal tariffMultiplier = 1;

                if (SelectedTariff == "Комфорт") tariffMultiplier = 1.5m;
                if (SelectedTariff == "Бизнес") tariffMultiplier = 2.0m;

                TotalPrice = basePrice + ((decimal)DistanceKm * pricePerKm * tariffMultiplier);
            }
            else
            {
                TotalPrice = 0;
                StatusMessage = "Не удалось построить маршрут.";
            }
        }


        private async Task FindTaxi()
        {
            IsSearching = true;
            StatusMessage = "Ищем водителя...";

            // --- ЗАГЛУШКА ПОИСКА ---
            // Имитируем поиск водителя с задержкой в 3 секунды
            await Task.Delay(3000);

            // Создаем фейкового водителя, как будто мы его нашли
            var foundDriver = new Driver
            {
                full_name = "Иванов Иван",
                car_model = "Kia Rio",
                license_plate = "A123BC78"
            };
            // ------------------------

            // TODO: Когда будет репозиторий, здесь будет вызов:
            // var foundDriver = await _orderRepository.FindDriverForOrder(newOrder);

            if (foundDriver != null)
            {
                StatusMessage = $"Водитель найден! {foundDriver.full_name} на {foundDriver.car_model} ({foundDriver.license_plate}) уже едет к вам.";
            }
            else
            {
                StatusMessage = "Свободных водителей не найдено. Попробуйте позже.";
            }

            IsSearching = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task UpdateAddressFromPoint(PointLatLng point, Action<string> setAddressAction)
        {
            var addresses = await Task.Run(() =>
            {
                // Используем провайдер OpenStreetMap для получения адреса
                var pointInfo = GMap.NET.MapProviders.GMapProviders.OpenStreetMap.GetPlacemark(point, out var status);
                if (status == GeoCoderStatusCode.OK && pointInfo.HasValue)
                {
                    return pointInfo.Value.Address;
                }
                return "Не удалось определить адрес";
            });
            setAddressAction(addresses);
        }

        private async Task UpdatePointFromAddress(string address, Action<PointLatLng> setPointAction)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            var point = await Task.Run(() =>
            {
                var pointLatLng = GMap.NET.MapProviders.GMapProviders.OpenStreetMap.GetPoint(address, out var status);
                if (status == GeoCoderStatusCode.OK && pointLatLng.HasValue)
                {
                    return pointLatLng.Value;
                }
                return PointLatLng.Empty;
            });

            if (!point.IsEmpty)
            {
                setPointAction(point);
            }
        }

    }
}
