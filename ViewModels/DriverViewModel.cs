using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using GMap.NET;
using TaxiWPF.Models;
using GMap.NET.MapProviders;
using TaxiWPF.Services;
using System.Linq;
using System.Windows;


namespace TaxiWPF.ViewModels
{
    public class DriverViewModel : INotifyPropertyChanged
    {
        private readonly User _currentUser;
        private readonly Car _currentCar;
        private DispatcherTimer _orderTimer;

        private bool _isOnline = true;
        private bool _isOnOrder = false;
        private string _panelTitle = "Доступные заказы";
        private string _statusMessage;

        private Order _selectedOrder;
        private Order _acceptedOrder;

        public event Action<PointLatLng, PointLatLng> OnRouteRequired;
        public PointLatLng DriverLocation { get; set; }
        public PointLatLng PassengerLocation { get; set; }

        public ObservableCollection<Order> AvailableOrders { get; set; }

        // --- НОВОЕ: Логика для оценки клиента ---
        private bool _isRatingClient = false;
        private int _clientRating = 0;
        public bool IsRatingClient
        {
            get => _isRatingClient;
            set { _isRatingClient = value; OnPropertyChanged(); UpdatePanelVisibility(); }
        }
        public int ClientRating
        {
            get => _clientRating;
            set { _clientRating = value; OnPropertyChanged(); (RateClientCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
        }
        public ICommand RateClientCommand { get; }
        public ICommand SkipRateClientCommand { get; }
        // ---------------------------------------


        public Order SelectedOrder
        {
            get => _selectedOrder;
            set { _selectedOrder = value; OnPropertyChanged(); UpdateCommandStates(); }

        }
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }

        }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                _isOnline = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }

        public bool IsOnOrder
        {
            get => _isOnOrder;
            set
            {
                _isOnOrder = value;
                OnPropertyChanged();
                UpdatePanelVisibility();
            }
        }

        public Order AcceptedOrder
        {
            get => _acceptedOrder;
            set { _acceptedOrder = value; OnPropertyChanged(); }
        }

        public string PanelTitle
        {
            get => _panelTitle;
            set { _panelTitle = value; OnPropertyChanged(); }
        }

        public ICommand AcceptOrderCommand { get; }
        public ICommand DeclineOrderCommand { get; }
        // --- НОВЫЕ КОМАНДЫ ---
        public ICommand ArrivedCommand { get; }
        public ICommand StartTripCommand { get; }
        public ICommand CompleteOrderCommand { get; }
        public ICommand GoToDashboardCommand { get; }

        // ---------------------

        // --- НОВЫЕ СВОЙСТВА ДЛЯ ВИДИМОСТИ КНОПОК ---
        public bool CanShowAcceptDecline => IsOnline && !IsOnOrder && SelectedOrder != null;
        public bool CanShowArrived => IsOnOrder && AcceptedOrder?.Status == OrderState.DriverEnRoute;
        public bool CanShowStartTrip => IsOnOrder && AcceptedOrder?.Status == OrderState.DriverArrived;
        public bool CanShowCompleteTrip => IsOnOrder && AcceptedOrder?.Status == OrderState.TripInProgress;
        // -----------------------------------------


        public DriverViewModel(User user, Car car)
        {
            _currentUser = user;
            _currentCar = car;
            StatusMessage = $"На линии на {_currentCar.ModelName}. Ожидание заказов...";
            AvailableOrders = new ObservableCollection<Order>();

            // --- ИЗМЕНЕНЫ CanExecute ---
            AcceptOrderCommand = new RelayCommand(async () => await AcceptOrder(), () => CanShowAcceptDecline);
            DeclineOrderCommand = new RelayCommand(DeclineOrder, () => CanShowAcceptDecline);

            // --- НОВЫЕ КОМАНДЫ ---
            ArrivedCommand = new RelayCommand(DriverArrived, () => CanShowArrived);
            StartTripCommand = new RelayCommand(StartTrip, () => CanShowStartTrip);
            CompleteOrderCommand = new RelayCommand(CompleteOrder, () => CanShowCompleteTrip);
            RateClientCommand = new RelayCommand(RateClient, () => ClientRating > 0);
            SkipRateClientCommand = new RelayCommand(SkipRateClient);
            GoToDashboardCommand = new RelayCommand(GoToDashboard, () => !IsOnOrder);
            // ---------------------

            DriverLocation = new PointLatLng(55.17, 61.38); // Заглушка

            _orderTimer = new DispatcherTimer();
            _orderTimer.Interval = TimeSpan.FromSeconds(5);
            _orderTimer.Tick += FetchNewOrders;
            if (IsOnline) _orderTimer.Start();

            // --- НОВОЕ: Подписываемся на "радиостанцию" ---
            OrderService.Instance.OrderUpdated += OnOrderUpdated;
        }

        // --- НОВЫЙ МЕТОД: Обработчик событий от OrderService ---
        private void OnOrderUpdated(Order updatedOrder)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Если это наш текущий заказ, обновляем его статус
                if (AcceptedOrder != null && updatedOrder.order_id == AcceptedOrder.order_id)
                {
                    AcceptedOrder = updatedOrder; // Получаем обновленный заказ
                    OnPropertyChanged(nameof(AcceptedOrder));
                    UpdateStatus(); // Обновляем StatusMessage
                    UpdateCommandStates(); // Обновляем видимость кнопок
                }
                else if (IsOnline && updatedOrder.Status == OrderState.Searching && !AvailableOrders.Any(o => o.order_id == updatedOrder.order_id))
                {
                    // Добавляем новый заказ в список
                    AvailableOrders.Add(updatedOrder);
                }
                else if (updatedOrder.Status == OrderState.Archived)
                {
                    // Если заказ отменен, убираем его
                    var orderToRemove = AvailableOrders.FirstOrDefault(o => o.order_id == updatedOrder.order_id);
                    if (orderToRemove != null) AvailableOrders.Remove(orderToRemove);
                }
            });
        }

        private void UpdateStatus()
        {
            if (IsOnline)
            {
                if (IsOnOrder)
                {
                    _orderTimer.Stop();
                    // --- НОВОЕ: Более детальный статус ---
                    switch (AcceptedOrder.Status)
                    {
                        case OrderState.DriverEnRoute:
                            StatusMessage = $"Едем к клиенту: {AcceptedOrder.PointA}";
                            break;
                        case OrderState.DriverArrived:
                            StatusMessage = "Ожидаем клиента...";
                            break;
                        case OrderState.TripInProgress:
                            StatusMessage = $"Везем клиента в: {AcceptedOrder.PointB}";
                            break;
                        case OrderState.TripCompleted:
                            StatusMessage = "Поездка завершена. Оцените клиента.";
                            break;
                    }
                }
                else
                {
                    _orderTimer.Start();
                    StatusMessage = "Вы на линии. Ожидание заказов...";
                    FetchNewOrders(null, null); // Сразу ищем
                }
            }
            else
            {
                _orderTimer.Stop();
                AvailableOrders.Clear();
                StatusMessage = "Вы не на линии.";
            }
        }

        private void FetchNewOrders(object sender, EventArgs e)
        {
            if (!IsOnline || IsOnOrder) return;

            var orders = OrderService.Instance.GetAvailableOrders();

            // --- УМНОЕ ОБНОВЛЕНИЕ СПИСКА ---

            // 1. Находим ID заказов, которые УЖЕ есть в списке
            var existingOrderIds = AvailableOrders.Select(o => o.order_id).ToList();

            // 2. Находим ID заказов, которые ПРИШЛИ с сервера
            var newOrderIds = orders.Select(o => o.order_id).ToList();

            // 3. Добавляем новые (которых нет в списке)
            foreach (var order in orders)
            {
                if (!existingOrderIds.Contains(order.order_id))
                {
                    AvailableOrders.Add(order);
                }
            }

            // 4. Удаляем пропавшие (которые есть в списке, но не пришли с сервера)
            // (Мы должны создать копию .ToList() для безопасного удаления из коллекции)
            foreach (var order in AvailableOrders.ToList())
            {
                if (!newOrderIds.Contains(order.order_id))
                {
                    // Если это был выбранный заказ, сбрасываем выбор
                    if (SelectedOrder == order)
                    {
                        SelectedOrder = null;
                    }
                    AvailableOrders.Remove(order);
                }
            }
        }

        private async Task AcceptOrder()
        {
            if (SelectedOrder == null) return;

            AcceptedOrder = SelectedOrder;
            IsOnOrder = true; // Это скроет список и покажет панель заказа

            // --- ИЗМЕНЕНО: Просто вызываем сервис ---
            var driverInfo = new Driver
            {
                driver_id = _currentUser.user_id,
                full_name = _currentUser.username,
                car_model = _currentCar.ModelName,
                license_plate = _currentCar.LicensePlate
                // (Здесь можно добавить и фото)
            };
            OrderService.Instance.AcceptOrder(AcceptedOrder, driverInfo);
            // -------------------------------------

            var point = await GetPointFromAddress(AcceptedOrder.PointA);
            if (!point.IsEmpty)
            {
                PassengerLocation = point;
                OnRouteRequired?.Invoke(DriverLocation, PassengerLocation);
            }

            AvailableOrders.Clear();
            SelectedOrder = null;
        }

        private void DeclineOrder()
        {
            if (SelectedOrder == null) return;
            AvailableOrders.Remove(SelectedOrder);
            SelectedOrder = null;
        }

        // --- НОВЫЕ МЕТОДЫ ДЛЯ КНОПОК ---
        private void DriverArrived()
        {
            OrderService.Instance.DriverArrived(AcceptedOrder);
        }

        private void StartTrip()
        {
            OrderService.Instance.StartTrip(AcceptedOrder);
            // (Очищаем маршрут до клиента и строим до точки Б)
            DriverLocation = PassengerLocation; // Мы "телепортировались" к клиенту
            GetPointFromAddress(AcceptedOrder.PointB).ContinueWith(task => {
                Application.Current.Dispatcher.Invoke(() => // Обернули в Dispatcher
                {
                    if (!task.Result.IsEmpty)
                    {
                        PassengerLocation = task.Result;
                        OnRouteRequired?.Invoke(DriverLocation, PassengerLocation);
                    }
                });
            });
        }

        private void CompleteOrder()
        {
            OrderService.Instance.CompleteOrder(AcceptedOrder);

            var walletRepo = new Repositories.WalletRepository();
            walletRepo.AddEarning(_currentUser.user_id, AcceptedOrder.TotalPrice, AcceptedOrder.order_id);

            OnRouteRequired?.Invoke(PointLatLng.Empty, PointLatLng.Empty); // Очищаем карту

            // --- НОВОЕ: Показываем экран оценки ---
            IsRatingClient = true;
        }

        // --- НОВЫЕ МЕТОДЫ ОЦЕНКИ ---
        private void RateClient()
        {
            MessageBox.Show($"Клиенту поставлена оценка: {ClientRating}");
            ResetAfterRating();
        }

        private void SkipRateClient()
        {
            MessageBox.Show("Оценка клиента пропущена.");
            ResetAfterRating();
        }

        private void ResetAfterRating()
        {
            AcceptedOrder.DriverRated = true;
            // (Если и клиент оценил, можно архивировать)
            if (AcceptedOrder.ClientRated) OrderService.Instance.ArchiveOrder(AcceptedOrder);

            AcceptedOrder = null;
            IsOnOrder = false;
            IsRatingClient = false;
            ClientRating = 0;
            UpdateStatus(); // Возвращаемся на линию
        }
        // -----------------------------


        // --- НОВЫЕ МЕТОДЫ ОБНОВЛЕНИЯ UI ---
        private void UpdateCommandStates()
        {
            (AcceptOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeclineOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ArrivedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StartTripCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CompleteOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GoToDashboardCommand as RelayCommand)?.RaiseCanExecuteChanged();

            OnPropertyChanged(nameof(CanShowAcceptDecline));
            OnPropertyChanged(nameof(CanShowArrived));
            OnPropertyChanged(nameof(CanShowStartTrip));
            OnPropertyChanged(nameof(CanShowCompleteTrip));
        }

        private void UpdatePanelVisibility()
        {
            OnPropertyChanged(nameof(IsOnOrder));
            OnPropertyChanged(nameof(IsRatingClient));
            PanelTitle = IsOnOrder ? (IsRatingClient ? "Оценка клиента" : "Текущий заказ") : "Доступные заказы";
        }
        // ---------------------------------

        private async Task<PointLatLng> GetPointFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return PointLatLng.Empty;
            return await Task.Run(() =>
            {
                var pointLatLng = GMapProviders.OpenStreetMap.GetPoint(address, out var status);
                if (status == GeoCoderStatusCode.OK && pointLatLng.HasValue)
                {
                    return pointLatLng.Value;
                }
                return PointLatLng.Empty;
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void GoToDashboard()
        {
            // 1. Находим скрытое окно Дашборда по его типу ViewModel
            var dashboardWindow = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.DataContext is DriverDashboardViewModel);

            if (dashboardWindow != null)
            {
                dashboardWindow.Show(); // Показываем его
            }

            // 2. Закрываем текущее окно (DriverView)
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    // Отписываемся от сервиса, чтобы не было утечек памяти
                    OrderService.Instance.OrderUpdated -= OnOrderUpdated;
                    window.Close();
                    break;
                }
            }
        }
    }
}
