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
    // Мы убрали enum OrderState отсюда. Теперь он в Models/OrderState.cs

    public class ClientViewModel : INotifyPropertyChanged
    {
        // --- Private поля ---
        private string _fromAddress;
        private string _toAddress;
        private string _selectedTariff;
        private decimal _totalPrice;
        private string _statusMessage;
        private PointLatLng _pointA;
        private PointLatLng _pointB;
        private double _distanceKm;
        private User _currentUser;
        private bool _isProfilePanelVisible = false;
        private OrderState _currentOrderState = OrderState.Idle;
        private Order _currentOrder; // Храним текущий заказ

        // Таймеры УДАЛЕНЫ

        private int _driverDistance;
        private int _currentRating = 0;
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
        public Order CurrentOrder
        {
            get => _currentOrder;
            set { _currentOrder = value; OnPropertyChanged(); }
        }

        // --- Свойства для UI ---
        public bool IsInputMode => CurrentOrderState == OrderState.Idle;
        public bool IsOrderSummaryVisible => CurrentOrderState != OrderState.Idle && CurrentOrderState != OrderState.TripCompleted;
        public bool IsSearchingState => CurrentOrderState == OrderState.Searching;
        public bool IsRatingVisible => CurrentOrderState == OrderState.TripCompleted;
        public bool CanRequestNewOrder => CurrentOrderState == OrderState.Idle;

        public string DriverInfoText { get; private set; }
        public int CurrentRating
        {
            get => _currentRating;
            set { 
                _currentRating = value; OnPropertyChanged();
                (RateDriverCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // Кнопка StartTripCommand УДАЛЕНА
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
            set { _fromAddress = value; OnPropertyChanged(); }
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
            set { _toAddress = value; OnPropertyChanged(); }
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
        public ObservableCollection<string> Tariffs { get; set; }
        public ICommand FindTaxiCommand { get; }

        public ClientViewModel(User loggedInUser)
        {
            CurrentUser = loggedInUser;
            PastTrips = new ObservableCollection<Order>();
            ToggleProfilePanelCommand = new RelayCommand(ToggleProfilePanel);

            Tariffs = new ObservableCollection<string> { "Эконом", "Комфорт", "Бизнес" };
            SelectedTariff = Tariffs[0];
            UpdatePointACommand = new RelayCommand(async () => await UpdatePointFromAddress(FromAddress, p => PointA = p));
            UpdatePointBCommand = new RelayCommand(async () => await UpdatePointFromAddress(ToAddress, p => PointB = p));

            FindTaxiCommand = new RelayCommand(FindTaxi, () => CanRequestNewOrder && !string.IsNullOrWhiteSpace(FromAddress) && !string.IsNullOrWhiteSpace(ToAddress));

            // Команда StartTripCommand УДАЛЕНА

            RateDriverCommand = new RelayCommand(RateDriver, () => CurrentRating > 0);
            CancelSearchCommand = new RelayCommand(CancelSearch, () => CurrentOrderState == OrderState.Searching);
            SkipRatingCommand = new RelayCommand(SkipRating);

            // --- НОВОЕ: Подписываемся на "радиостанцию" ---
            OrderService.Instance.OrderUpdated += OnOrderUpdated;
        }

        // --- НОВЫЙ МЕТОД: Обработчик событий от OrderService ---
        private void OnOrderUpdated(Order updatedOrder)
        {
            // Если это не наш заказ, игнорируем
            if (CurrentOrder == null || updatedOrder.order_id != CurrentOrder.order_id)
            {
                return;
            }

            // Важно: Обновляем UI в основном потоке
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Обновляем и заказ, и состояние
                CurrentOrder = updatedOrder;
                CurrentOrderState = updatedOrder.Status;

                // Обновляем текстовые сообщения для клиента
                switch (updatedOrder.Status)
                {
                    case OrderState.DriverEnRoute:
                        // (Здесь можно было бы симулировать DriverDistance)
                        _driverDistance = 5; // Заглушка
                        UpdateDriverInfoText();
                        StatusMessage = "Водитель в пути";
                        break;
                    case OrderState.DriverArrived:
                        StatusMessage = "Водитель прибыл. Ожидайте начала поездки.";
                        DriverInfoText = $"{CurrentOrder.AssignedDriver.full_name} ожидает";
                        OnPropertyChanged(nameof(DriverInfoText));
                        break;
                    case OrderState.TripInProgress:
                        StatusMessage = "Поездка началась...";
                        DriverInfoText = ""; // Убираем инфо о водителе
                        OnPropertyChanged(nameof(DriverInfoText));
                        break;
                    case OrderState.TripCompleted:
                        StatusMessage = "Поездка завершена! Оцените водителя.";
                        CurrentRating = 0; // Сбрасываем рейтинг
                        break;
                    case OrderState.Archived:
                        // Заказ ушел в архив (например, водитель отменил)
                        ResetToIdleState();
                        break;
                }
            });
        }

        // --- ИЗМЕНЕНО: Метод FindTaxi (убрали async/await и таймеры) ---
        private void FindTaxi()
        {
            CurrentOrderState = OrderState.Searching;
            StatusMessage = "Ищем водителя...";

            var newOrder = new Order
            {
                PointA = this.FromAddress,
                PointB = this.ToAddress,
                Tariff = this.SelectedTariff,
                TotalPrice = this.TotalPrice,
                OrderClient = new Client { client_id = CurrentUser.user_id, full_name = CurrentUser.username } // Заглушка
            };

            // Просто отправляем заказ и СОХРАНЯЕМ ЕГО (с ID)
            // Сервис сам оповестит нас через OnOrderUpdated, когда найдет водителя
            CurrentOrder = OrderService.Instance.SubmitOrder(newOrder);
        }

        private void CancelSearch()
        {
            // (В идеале, надо оповестить сервис, но пока просто сбросим у себя)
            OrderService.Instance.ArchiveOrder(CurrentOrder); // Используем Archive
            ResetToIdleState();
        }

        private void RateDriver()
        {
            MessageBox.Show($"Спасибо за вашу оценку: {CurrentRating} звезд(ы)!", "Рейтинг");
            CurrentOrder.ClientRated = true;
            // (Если и водитель оценил, можно архивировать)
            // if (CurrentOrder.DriverRated) OrderService.Instance.ArchiveOrder(CurrentOrder);
            ResetToIdleState();
        }

        private void SkipRating()
        {
            MessageBox.Show("Оценка пропущена.", "Рейтинг");
            CurrentOrder.ClientRated = true;
            // if (CurrentOrder.DriverRated) OrderService.Instance.ArchiveOrder(CurrentOrder);
            ResetToIdleState();
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
        }

        private void UpdateButtonStates()
        {
            // OnPropertyChanged(nameof(IsStartTripButtonVisible)); // <-- УДАЛЕНО
            OnPropertyChanged(nameof(IsRatingVisible));
            OnPropertyChanged(nameof(CanRequestNewOrder));
            OnPropertyChanged(nameof(IsInputMode));
            OnPropertyChanged(nameof(IsOrderSummaryVisible));
            OnPropertyChanged(nameof(IsSearchingState));
            (FindTaxiCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }


        // --- ТВОИ СТАРЫЕ МЕТОДЫ (ОСТАЮТСЯ БЕЗ ИЗМЕНЕНИЙ) ---
        #region (Методы, которые не менялись)
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
            PastTrips.Clear();
            PastTrips.Add(new Order
            {
                order_id = 101,
                PointA = "ул. Ленина, 10",
                PointB = "пр. Победы, 5",
                TotalPrice = 250,
                Status = OrderState.Archived, // Используем enum
                AssignedDriver = new Driver { full_name = "Петров П." }
            });
            PastTrips.Add(new Order
            {
                order_id = 102,
                PointA = "Вокзал",
                PointB = "Аэропорт",
                TotalPrice = 800,
                Status = OrderState.Archived, // Используем enum
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

            var route = GMap.NET.MapProviders.OpenStreetMapProvider.Instance.GetRoute(PointA, PointB, false, false, 15);
            if (route != null)
            {
                DistanceKm = route.Distance; // Расстояние в км
                decimal basePrice = 50;
                decimal pricePerKm = 15;
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task UpdateAddressFromPoint(PointLatLng point, Action<string> setAddressAction)
        {
            var addresses = await Task.Run(() =>
            {
                try
                {
                    var pointInfo = GMap.NET.MapProviders.OpenStreetMapProvider.Instance.GetPlacemark(point, out var status);
                    if (status == GeoCoderStatusCode.OK && pointInfo.HasValue)
                    {
                        var p = pointInfo.Value;
                        string addressString = $"{p.LocalityName}, {p.ThoroughfareName}, {p.HouseNo}";
                        addressString = System.Text.RegularExpressions.Regex.Replace(addressString, @"(,\s*,)+", ",");
                        addressString = addressString.Trim(' ', ',');
                        if (string.IsNullOrWhiteSpace(addressString)) return "Не удалось определить адрес";
                        return addressString;
                    }
                }
                catch (Exception) { /* Молчание */ }
                return "Не удалось определить адрес";
            });
            setAddressAction(addresses);
        }

        private async Task UpdatePointFromAddress(string address, Action<PointLatLng> setPointAction)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            var point = await Task.Run(() =>
            {
                try
                {
                    List<PointLatLng> pointLatLngs;
                    var status = GMap.NET.MapProviders.GMapProviders.OpenStreetMap.GetPoints(address, out pointLatLngs);
                    if (status == GeoCoderStatusCode.OK && pointLatLngs != null && pointLatLngs.Count > 0)
                    {
                        return pointLatLngs[0];
                    }
                }
                catch (Exception) { /* Молчание */ }
                return PointLatLng.Empty;
            });
            if (!point.IsEmpty) { setPointAction(point); }
        }
        #endregion
    }
}
