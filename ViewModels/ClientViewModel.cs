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
using System.Threading;
using System.Windows;



namespace TaxiWPF.ViewModels
{

    public enum OrderState
    {
        Idle,           // Исходное состояние
        Searching,      // Идет поиск
        DriverEnRoute,  // Водитель едет
        DriverArrived,  // Водитель прибыл
        TripInProgress, // Поездка идет
        TripCompleted   // Поездка завершена
    }

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
        private OrderState _currentOrderState = OrderState.Idle;
        private Order _currentOrder; // Храним текущий заказ
        private Timer _driverArrivalTimer;
        private Timer _tripCompletionTimer;
        private int _driverDistance;
        private int _currentRating = 0;
        public bool IsInputMode => CurrentOrderState == OrderState.Idle;
        public bool IsOrderSummaryVisible => CurrentOrderState != OrderState.Idle && CurrentOrderState != OrderState.TripCompleted;
        public bool IsSearchingState => CurrentOrderState == OrderState.Searching;

        // Additional Rating Criteria
        private bool _wasPolite;
        private bool _wasClean;
        private bool _goodDriving;
        public bool WasPolite { get => _wasPolite; set { _wasPolite = value; OnPropertyChanged(); } }
        public bool WasClean { get => _wasClean; set { _wasClean = value; OnPropertyChanged(); } }
        public bool GoodDriving { get => _goodDriving; set { _goodDriving = value; OnPropertyChanged(); } }


        public ICommand CancelSearchCommand { get; }
        public ICommand SkipRatingCommand { get; }




        public OrderState CurrentOrderState
        {
            get => _currentOrderState;
            set { _currentOrderState = value; OnPropertyChanged(); UpdateButtonStates(); }
        }

        private bool CanFindTaxi()
        {
            // Заказывать можно только если нет активного заказа
            return CurrentOrderState == OrderState.Idle &&
                   !string.IsNullOrWhiteSpace(FromAddress) &&
                   !string.IsNullOrWhiteSpace(ToAddress);
        }

        // Вспомогательный метод для обновления CanExecute кнопок
        private void UpdateButtonStates()
        {
            OnPropertyChanged(nameof(IsStartTripButtonVisible));
            OnPropertyChanged(nameof(IsRatingVisible));
            OnPropertyChanged(nameof(CanRequestNewOrder));
            OnPropertyChanged(nameof(IsInputMode)); // Trigger updates for these too
            OnPropertyChanged(nameof(IsOrderSummaryVisible));
            OnPropertyChanged(nameof(IsSearchingState));
            (FindTaxiCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Add this
        }

        public Order CurrentOrder
        {
            get => _currentOrder;
            set { _currentOrder = value; OnPropertyChanged(); }
        }

        // Текст типа "Иванов Иван (Kia Rio) - 3 мин до вас"
        public string DriverInfoText { get; private set; }

        public bool IsStartTripButtonVisible => CurrentOrderState == OrderState.DriverArrived;
        public bool IsRatingVisible => CurrentOrderState == OrderState.TripCompleted;
        public bool CanRequestNewOrder => CurrentOrderState == OrderState.Idle;

        public int CurrentRating
        {
            get => _currentRating;
            set { _currentRating = value; OnPropertyChanged(); }
        }

        // --- НОВЫЕ КОМАНДЫ ---
        public ICommand StartTripCommand { get; }
        public ICommand RateDriverCommand { get; }






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
            StartTripCommand = new RelayCommand(StartTrip);
            RateDriverCommand = new RelayCommand(RateDriver);
            CancelSearchCommand = new RelayCommand(CancelSearch, () => CurrentOrderState == OrderState.Searching);
            SkipRatingCommand = new RelayCommand(SkipRating);
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
            CurrentOrderState = OrderState.Searching;
            // Make sure CanExecute updates for the Cancel button
            (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            StatusMessage = "Ищем водителя...";
            // Hide inputs, show summary (will happen via binding)
            OnPropertyChanged(nameof(IsInputMode));
            OnPropertyChanged(nameof(IsOrderSummaryVisible));

            var newOrder = new Order
            {
                // ==== Убедись, что эти строки есть ====
                PointA = this.FromAddress, // Должен быть адрес из TextBox
                PointB = this.ToAddress,   // Должен быть адрес из TextBox
                Tariff = this.SelectedTariff, // Должен быть выбранный тариф
                TotalPrice = this.TotalPrice, // Должна быть рассчитанная цена
                                              // =====================================
                OrderClient = new Client { /* ... */ }
            };

            CurrentOrder = await OrderService.Instance.SubmitOrder(newOrder);

            // --- SIMULATE BETTER DRIVER DATA ---
            if (CurrentOrder.Status == "Водитель назначен")
            {

                CurrentOrder.AssignedDriver.car_model = "Kia Rio (Белая)"; // Заглушка
                CurrentOrder.AssignedDriver.license_plate = "A 123 AA 77"; // Заглушка
                CurrentOrder.AssignedDriver.DriverPhotoUrl = "https://avatars.mds.yandex.net/get-autoru-vos/6366648/75f696f7d155a5dc96b1d831fee1c71b/456x342"; // Заглушка
                CurrentOrder.AssignedDriver.CarPhotoUrl = "https://avatars.mds.yandex.net/i?id=9a7db850ba17a5462f84928aaa94c093_l-5286004-images-thumbs&n=13"; // Заглушка
                OnPropertyChanged(nameof(CurrentOrder)); // Сообщаем UI об изменениях
                

                CurrentOrderState = OrderState.DriverEnRoute;
                _driverDistance = 5;
                UpdateDriverInfoText();
                StatusMessage = "Водитель в пути";
                _driverArrivalTimer = new Timer(DriverArrivedCallback, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
            }
            else
            {
                StatusMessage = "Свободных водителей не найдено. Попробуйте позже.";
                CurrentOrderState = OrderState.Idle;
            }
    // Update button states again after search completes
    (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(IsInputMode));
            OnPropertyChanged(nameof(IsOrderSummaryVisible));
        }

        private void CancelSearch()
        {
            // TODO: In a real app, tell OrderService to stop searching for this order.
            // OrderService.Instance.CancelOrder(CurrentOrder);

            StatusMessage = "Поиск отменен.";
            CurrentOrderState = OrderState.Idle;
            CurrentOrder = null;
            // Update button states and visibility
            (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(IsInputMode));
            OnPropertyChanged(nameof(IsOrderSummaryVisible));
            OnPropertyChanged(nameof(IsSearchingState)); // Hide cancel button
        }



        private void UpdateDriverInfoText()
        {
            if (CurrentOrder?.AssignedDriver != null)
            {
                DriverInfoText = $"{CurrentOrder.AssignedDriver.full_name} ({CurrentOrder.AssignedDriver.car_model ?? "Машина"}) - {_driverDistance} мин до вас";
            }
            else
            {
                DriverInfoText = "";
            }
            OnPropertyChanged(nameof(DriverInfoText));
        }

        // Вызывается по таймеру, когда водитель "прибыл"
        private void DriverArrivedCallback(object state)
        {
            // Важно: Обновляем UI в основном потоке
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentOrderState = OrderState.DriverArrived;
                StatusMessage = "Водитель прибыл. Можете начинать поездку.";
                DriverInfoText = $"{CurrentOrder.AssignedDriver.full_name} ожидает";
                OnPropertyChanged(nameof(DriverInfoText));
                _driverArrivalTimer?.Dispose(); // Останавливаем таймер
            });
        }

        // Вызывается при нажатии кнопки "Начать поездку"
        private void StartTrip()
        {
            CurrentOrderState = OrderState.TripInProgress;
            StatusMessage = "Поездка началась...";
            DriverInfoText = ""; // Убираем инфо о водителе
            OnPropertyChanged(nameof(DriverInfoText));

            // Запускаем таймер завершения поездки (сработает через 10 секунд)
            _tripCompletionTimer = new Timer(TripCompletedCallback, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
        }

        // Вызывается по таймеру, когда поездка "завершена"
        private void TripCompletedCallback(object state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentOrderState = OrderState.TripCompleted;
                StatusMessage = "Поездка завершена! Оцените водителя.";
                _tripCompletionTimer?.Dispose(); // Останавливаем таймер
                CurrentRating = 0; // Сбрасываем рейтинг
            });
        }

        // Вызывается при нажатии на звезду рейтинга
        private void RateDriver()
        {
            // Здесь можно было бы сохранить рейтинг в БД
            MessageBox.Show($"Спасибо за вашу оценку: {CurrentRating} звезд(ы)!", "Рейтинг");
            ResetToIdleState();

            // Возвращаемся в исходное состояние
            CurrentOrderState = OrderState.Idle;
            CurrentOrder = null;
            StatusMessage = "";
            CurrentRating = 0;
        }

        private void SkipRating()
        {
            MessageBox.Show("Оценка пропущена.", "Рейтинг");
            ResetToIdleState(); // Use the same helper
        }


        private void ResetToIdleState()
        {
            CurrentOrderState = OrderState.Idle;
            CurrentOrder = null;
            StatusMessage = "";
            CurrentRating = 0;
            WasPolite = false;
            WasClean = false;
            GoodDriving = false;
            // Update UI states
            OnPropertyChanged(nameof(IsInputMode));
            OnPropertyChanged(nameof(IsOrderSummaryVisible));
            (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
