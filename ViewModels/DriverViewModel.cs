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


namespace TaxiWPF.ViewModels
{
    public class DriverViewModel : INotifyPropertyChanged
    {
        private Order _selectedOrder;
        private string _statusMessage; 
        private DispatcherTimer _orderTimer;
        private bool _isOnline = true;
        private bool _isOnOrder = false;
        private Order _acceptedOrder;
        private string _panelTitle = "Доступные заказы";

        public PointLatLng DriverLocation { get; set; } // Текущее местоположение водителя (заглушка)
        public PointLatLng PassengerLocation { get; set; } // Местоположение клиента из заказа

        public ObservableCollection<Order> AvailableOrders { get; set; }
        public Order SelectedOrder
        {
            get => _selectedOrder;
            set { _selectedOrder = value; OnPropertyChanged(); }
            
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
                PanelTitle = value ? "Текущий заказ" : "Доступные заказы";
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
        public ICommand CompleteOrderCommand { get; }

        // Событие для оповещения View о необходимости построить маршрут
        public event Action<PointLatLng, PointLatLng> OnRouteRequired;


        public DriverViewModel()
        {
            AvailableOrders = new ObservableCollection<Order>();
            AcceptOrderCommand = new RelayCommand(async () => await AcceptOrder(), () => SelectedOrder != null && !IsOnOrder);
            DeclineOrderCommand = new RelayCommand(DeclineOrder, () => SelectedOrder != null && !IsOnOrder);
            CompleteOrderCommand = new RelayCommand(CompleteOrder, () => IsOnOrder);

            DriverLocation = new PointLatLng(55.17, 61.38); // Начальное положение водителя (заглушка)

            _orderTimer = new DispatcherTimer();
            _orderTimer.Interval = TimeSpan.FromSeconds(5); 
            _orderTimer.Tick += GenerateNewOrder;

            if (IsOnline)
            {
                _orderTimer.Start();
            }
        }

        private void UpdateStatus()
        {
            if (IsOnline && !IsOnOrder)
            {
                _orderTimer.Start();
                StatusMessage = "Вы на линии. Ожидание заказов...";
            }
            else
            {
                _orderTimer.Stop();
                AvailableOrders.Clear();
                StatusMessage = IsOnOrder ? $"Выполняется заказ #{AcceptedOrder.order_id}" : "Вы не на линии.";
            }
        }

        private void GenerateNewOrder(object sender, EventArgs e)
        {
            var random = new Random();
            var newOrder = new Order
            {
                order_id = random.Next(1000, 9999),
                PointA = "ул. Ленина, " + random.Next(1, 100),
                PointB = "пр. Победы, " + random.Next(1, 100),
                TotalPrice = random.Next(150, 500),
                Tariff = "Комфорт",
                OrderClient = new Client { full_name = "Петров Петр" }
            };
             AvailableOrders.Add(newOrder);
        }

        private async Task AcceptOrder()
        {
            if (SelectedOrder == null) return;

            AcceptedOrder = SelectedOrder;
            IsOnOrder = true;

            StatusMessage = $"Заказ #{AcceptedOrder.order_id} принят. Направляйтесь к клиенту: {AcceptedOrder.PointA}"; 
            
            // Получаем координаты точки А (местоположение клиента)
            var point = await GetPointFromAddress(AcceptedOrder.PointA);
            if (!point.IsEmpty)
            {
                PassengerLocation = point;
                // Вызываем событие, чтобы View построил маршрут
                OnRouteRequired?.Invoke(DriverLocation, PassengerLocation);
            }

            AvailableOrders.Clear();
            SelectedOrder = null;
            UpdateStatus(); // Останавливаем таймер и обновляем статус
        }

        private void DeclineOrder()
        {
            if (SelectedOrder == null) return;
            StatusMessage = $"Заказ #{SelectedOrder.order_id} отклонен."; 
            AvailableOrders.Remove(SelectedOrder);
            SelectedOrder = null;
        }

        private void CompleteOrder()
        {
            StatusMessage = $"Заказ #{AcceptedOrder.order_id} успешно завершен.";
            AcceptedOrder = null;
            IsOnOrder = false;
            // Сбрасываем маршрут, можно передать пустые точки
            OnRouteRequired?.Invoke(PointLatLng.Empty, PointLatLng.Empty);
            UpdateStatus(); // Запускаем таймер, если водитель на линии
        }

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
    }
}
