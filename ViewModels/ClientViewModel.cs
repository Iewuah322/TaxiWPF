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
using TaxiWPF.Services;
using GMap.NET.MapProviders;



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
        private User _currentUser;
        private bool _isProfilePanelVisible = false;




        public User CurrentUser
        {
            get => _currentUser;
            set { _currentUser = value; OnPropertyChanged(); }
        }

        public bool IsProfilePanelVisible
        {
            get => _isProfilePanelVisible;
            set { _isProfilePanelVisible = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Order> PastTrips { get; set; }
        public ICommand ToggleProfilePanelCommand { get; }


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

        public ClientViewModel(User loggedInUser)
        {
            // --- НОВОЕ ---
            CurrentUser = loggedInUser;
            PastTrips = new ObservableCollection<Order>();
            ToggleProfilePanelCommand = new RelayCommand(ToggleProfilePanel);
            // -----------

            // --- Твой старый код из конструктора ---
            Tariffs = new ObservableCollection<string> { "Эконом", "Комфорт", "Бизнес" };
            SelectedTariff = Tariffs[0];
            UpdatePointACommand = new RelayCommand(async () => await UpdatePointFromAddress(FromAddress, p => PointA = p));
            UpdatePointBCommand = new RelayCommand(async () => await UpdatePointFromAddress(ToAddress, p => PointB = p));
            FindTaxiCommand = new RelayCommand(async () => await FindTaxi(), () => !IsSearching && !string.IsNullOrWhiteSpace(FromAddress) && !string.IsNullOrWhiteSpace(ToAddress));
            // ------------------------------------
        }

        private void ToggleProfilePanel()
        {
            IsProfilePanelVisible = !IsProfilePanelVisible;
            if (IsProfilePanelVisible)
            {
                LoadPastTrips();
            }
        }

        private void LoadPastTrips()
        {
            // --- ЗАГЛУШКА ---
            // Когда придешь в шарагу, здесь будет вызов:
            // var trips = _repository.GetTripsForClient(CurrentUser.user_id);
            PastTrips.Clear();
            PastTrips.Add(new Order
            {
                order_id = 101,
                PointA = "ул. Ленина, 10",
                PointB = "пр. Победы, 5",
                TotalPrice = 250,
                Status = "Завершен",
                AssignedDriver = new Driver { full_name = "Петров П." }
            });
            PastTrips.Add(new Order
            {
                order_id = 102,
                PointA = "Вокзал",
                PointB = "Аэропорт",
                TotalPrice = 800,
                Status = "Завершен",
                AssignedDriver = new Driver { full_name = "Сидоров А." }
            });
            // ------------------
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

            // 1. Создаем объект заказа
            var newOrder = new Order
            {
                PointA = this.FromAddress,
                PointB = this.ToAddress,
                Tariff = this.SelectedTariff,
                TotalPrice = this.TotalPrice,
                OrderClient = new Client
                {
                    // В реальной БД мы бы подгрузили ID, ФИО и телефон клиента.
                    // В заглушке - просто создадим его на лету из того, что есть.
                    client_id = this.CurrentUser.user_id,
                    full_name = this.CurrentUser.username
                }
            };

            // 2. Отправляем заказ на "сервер"
            var createdOrder = await OrderService.Instance.SubmitOrder(newOrder);

            // 3. Обрабатываем ответ
            if (createdOrder.Status == "Водитель назначен")
            {
                StatusMessage = $"Водитель найден! {createdOrder.AssignedDriver.full_name} уже едет к вам.";
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

        // ТОЧКА -> АДРЕС (на OpenStreetMap)
        private async Task UpdateAddressFromPoint(PointLatLng point, Action<string> setAddressAction)
        {
            var addresses = await Task.Run(() =>
            {
                try
                {
                    // Используем OpenStreetMap
                    var pointInfo = GMap.NET.MapProviders.OpenStreetMapProvider.Instance.GetPlacemark(point, out var status);
                    if (status == GeoCoderStatusCode.OK && pointInfo.HasValue)
                    {
                        var p = pointInfo.Value;

                        // Собираем адрес. HouseNo часто будет пустым (null)
                        string addressString = $"{p.LocalityName}, {p.ThoroughfareName}, {p.HouseNo}";

                        // Очистка
                        addressString = System.Text.RegularExpressions.Regex.Replace(addressString, @"(,\s*,)+", ",");
                        addressString = addressString.Trim(' ', ',');

                        if (string.IsNullOrWhiteSpace(addressString)) return "Не удалось определить адрес";
                        return addressString;
                    }
                }
                catch (Exception ex) { /* Молчание */ }
                return "Не удалось определить адрес";
            });
            setAddressAction(addresses);
        }

        // АДРЕС -> ТОЧКА (на OpenStreetMap)
        private async Task UpdatePointFromAddress(string address, Action<PointLatLng> setPointAction)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            var point = await Task.Run(() =>
            {
                try
                {
                    // Объявляем 'pointLatLngs' (List)
                    List<PointLatLng> pointLatLngs;

                    // 'status' получает результат (GeoCoderStatusCode)
                    // 'pointLatLngs' получает список (out)
                    var status = GMap.NET.MapProviders.GMapProviders.OpenStreetMap.GetPoints(address, out pointLatLngs);

                    // Проверяем
                    if (status == GeoCoderStatusCode.OK && pointLatLngs != null && pointLatLngs.Count > 0)
                    {
                        return pointLatLngs[0];
                    }
                }
                catch (Exception ex) { /* Молчание */ }
                return PointLatLng.Empty;
            });

            if (!point.IsEmpty)
            {
                setPointAction(point);
            }
        }

    }
}
