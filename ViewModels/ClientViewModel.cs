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
using System.Windows.Threading;
using TaxiWPF.Repositories;
using TaxiWPF.Views;


namespace TaxiWPF.ViewModels
{
    // Мы убрали enum OrderState отсюда. Теперь он в Models/OrderState.cs

    public class ClientViewModel : INotifyPropertyChanged
    {


        private readonly WalletRepository _walletRepository;
        private bool _isCardPayment = true;
        private PaymentCard _selectedSavedCard;
        private bool _rememberCard;

        // Поля для форматирования и валидации
        private string _cardNumber;
        private string _cardExpiry;
        private string _cardCVV;
        private string _cardNumberError;
        private string _cardExpiryError;
        private string _cvvError;

        public bool IsCardPayment { get => _isCardPayment; set { _isCardPayment = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCashPayment)); } }
        public bool IsCashPayment { get => !_isCardPayment; set { _isCardPayment = !value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCashPayment)); } }
        public ObservableCollection<PaymentCard> SavedCards { get; set; }

        public bool IsNewCardSelected => SelectedSavedCard != null && string.IsNullOrEmpty(SelectedSavedCard.CardNumber);
        public bool RememberCard { get => _rememberCard; set { _rememberCard = value; OnPropertyChanged(); } }


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
        private Order _currentOrder;
        private DispatcherTimer _orderTimer;
        private readonly RatingRepository _ratingRepository;
        private readonly OrderRepository _orderRepository;
       

        // Таймеры УДАЛЕНЫ

        private int _driverDistance;
        private int _currentRating = 0;
        private bool _wasPolite;
        private bool _wasClean;
        private bool _goodDriving;

        public bool WasPolite { get => _wasPolite; set { _wasPolite = value; OnPropertyChanged(); } }
        public bool WasClean { get => _wasClean; set { _wasClean = value; OnPropertyChanged(); } }
        public bool GoodDriving { get => _goodDriving; set { _goodDriving = value; OnPropertyChanged(); } }

        public PaymentCard SelectedSavedCard
        {
            get => _selectedSavedCard;
            set
            {
                _selectedSavedCard = value;
                OnPropertyChanged();
                LoadCardFromSelection(value);
                OnPropertyChanged(nameof(IsNewCardSelected));
            }
        }
        public string CardNumber
        {
            get => _cardNumber;
            set
            {
                string digits = new string(value.Where(char.IsDigit).ToArray());
                if (digits.Length > 16) digits = digits.Substring(0, 16);
                string formatted = string.Join(" ", Enumerable.Range(0, (digits.Length + 3) / 4)
                    .Select(i => digits.Substring(i * 4, Math.Min(4, digits.Length - i * 4))));
                _cardNumber = formatted;
                OnPropertyChanged();
                ValidateCardNumber();
            }
        }

        public string CardExpiry
        {
            get => _cardExpiry;
            set
            {
                string digits = new string(value.Where(char.IsDigit).ToArray());
                if (digits.Length > 4) digits = digits.Substring(0, 4);
                string formatted = digits;
                if (digits.Length > 2) formatted = digits.Insert(2, "/");
                _cardExpiry = formatted;
                OnPropertyChanged();
                ValidateCardExpiry();
            }
        }
        public string CardCVV
        {
            get => _cardCVV;
            set
            {
                string digits = new string(value.Where(char.IsDigit).ToArray());
                if (digits.Length > 3) digits = digits.Substring(0, 3);
                _cardCVV = digits;
                OnPropertyChanged();
                ValidateCvv();
            }
        }
        public string CardNumberError { get => _cardNumberError; set { _cardNumberError = value; OnPropertyChanged(); } }
        public string CardExpiryError { get => _cardExpiryError; set { _cardExpiryError = value; OnPropertyChanged(); } }
        public string CvvError { get => _cvvError; set { _cvvError = value; OnPropertyChanged(); } }


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

        public ICommand ContactSupportCommand { get; }
        private readonly SupportRepository _supportRepository;

        public ClientViewModel(User loggedInUser)
        {
            _supportRepository = new SupportRepository();
            _walletRepository = new WalletRepository();
            SavedCards = new ObservableCollection<PaymentCard>();
            _supportRepository = new SupportRepository();
            _ratingRepository = new RatingRepository();
            _orderRepository = new OrderRepository();
            CurrentUser = loggedInUser;
            PastTrips = new ObservableCollection<Order>();
            ToggleProfilePanelCommand = new RelayCommand(ToggleProfilePanel);
            LoadSavedCards();
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
            ContactSupportCommand = new RelayCommand(OpenSupportChat);
        }

        private void OpenSupportChat()
        {
            // Этот вызов теперь будет работать без ошибок
            var ticket = SupportService.Instance.GetOrCreateTicketForUser(CurrentUser);

            ticket.UserInfo = CurrentUser;

            var chatView = new SupportChatView(CurrentUser, ticket);
            chatView.Show();
        }

        private void LoadSavedCards()
        {
            SavedCards.Clear();
            SavedCards.Add(new PaymentCard { CardNumber = null }); // Для выбора "Новая карта"
            var cardsFromRepo = _walletRepository.GetSavedCards(CurrentUser.user_id);
            foreach (var card in cardsFromRepo)
            {
                SavedCards.Add(card);
            }
            SelectedSavedCard = SavedCards.FirstOrDefault();
        }

        private void LoadCardFromSelection(PaymentCard card)
        {
            if (card == null || string.IsNullOrEmpty(card.CardNumber))
            {
                CardNumber = "";
                CardExpiry = "";
                CardCVV = "";
                RememberCard = true;
            }
            else
            {
                CardNumber = card.CardNumber;
                CardExpiry = card.CardExpiry;
                CardCVV = "";
                RememberCard = false;
            }
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
                        DriverInfoText = $"Вас везет {CurrentOrder.AssignedDriver.full_name}";
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
            if (!CanRequestNewOrder) return;

            if (IsCardPayment)
            {
                bool isCardValid = true;
                if (IsNewCardSelected)
                {
                    ValidateCardNumber(true);
                    ValidateCardExpiry(true);
                    ValidateCvv(true);
                    isCardValid = string.IsNullOrEmpty(CardNumberError) &&
                                  string.IsNullOrEmpty(CardExpiryError) &&
                                  string.IsNullOrEmpty(CvvError);
                }

                if (!isCardValid)
                {
                    MessageBox.Show("Пожалуйста, введите корректные данные карты.", "Ошибка оплаты");
                    return;
                }
            }

            CurrentOrderState = OrderState.Searching;
            StatusMessage = "Ищем водителя...";

            if (IsCardPayment && IsNewCardSelected && RememberCard)
            {
                var card = new PaymentCard { CardNumber = this.CardNumber, CardExpiry = this.CardExpiry };
                _walletRepository.AddCard(CurrentUser.user_id, card);
            }

            var newOrder = new Order
            {
                PointA = this.FromAddress,
                PointB = this.ToAddress,
                Tariff = this.SelectedTariff,
                TotalPrice = this.TotalPrice,
                // --- НАЧАЛО ИСПРАВЛЕНИЯ: Добавлен недостающий объект OrderClient ---
                OrderClient = new Client { client_id = CurrentUser.user_id, full_name = CurrentUser.full_name },
                // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
                PaymentMethod = IsCardPayment ? "Карта" : "Наличные"
            };

            CurrentOrder = OrderService.Instance.SubmitOrder(newOrder);
        }

        private void ValidateCardNumber(bool force = false)
        {
            CardNumberError = (string.IsNullOrWhiteSpace(CardNumber) || CardNumber.Replace(" ", "").Length < 16) ? "Введите полный номер карты." : null;
            if (force) (FindTaxiCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        private void ValidateCardExpiry(bool force = false)
        {
            CardExpiryError = (string.IsNullOrWhiteSpace(CardExpiry) || CardExpiry.Length < 5) ? "Введите срок." : null;
            if (force) (FindTaxiCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        private void ValidateCvv(bool force = false)
        {
            CvvError = (string.IsNullOrWhiteSpace(CardCVV) || CardCVV.Length < 3) ? "Введите CVV." : null;
            if (force) (FindTaxiCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void CancelSearch()
        {
            // (В идеале, надо оповестить сервис, но пока просто сбросим у себя)
            OrderService.Instance.ArchiveOrder(CurrentOrder); // Используем Archive
            ResetToIdleState();
        }

        private void RateDriver()
        {
            // --- НАЧАЛО ИЗМЕНЕНИЙ ---
            bool success = _ratingRepository.AddRating(
                CurrentOrder,
                _currentUser.user_id,
                CurrentOrder.AssignedDriver.driver_id,
                CurrentRating,
                WasPolite,
                WasClean,
                GoodDriving
            );

            if (success)
            {
                MessageBox.Show($"Спасибо за вашу оценку: {CurrentRating} звезд(ы)!", "Рейтинг");
            }
            else
            {
                MessageBox.Show("Не удалось сохранить оценку.", "Ошибка");
            }

            CurrentOrder.ClientRated = true;
            OrderService.Instance.ArchiveOrder(CurrentOrder);
            ResetToIdleState();

        }

        private void SkipRating()
        {
            MessageBox.Show("Оценка пропущена.", "Рейтинг");
            CurrentOrder.ClientRated = true;
            OrderService.Instance.ArchiveOrder(CurrentOrder);
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
                // --- НАЧАЛО ИЗМЕНЕНИЙ ---
                // 1. Загружаем самую свежую версию пользователя из БД (с обновленным рейтингом)
                var updatedUser = new UserRepository().GetUserByUsername(CurrentUser.username);
                if (updatedUser != null)
                {
                    CurrentUser = updatedUser;
                }

                // 2. Загружаем актуальную историю поездок
                LoadPastTrips();
                // --- КОНЕЦ ИЗМЕНЕНИЙ ---
            }
        }

        private void LoadPastTrips()
        {
            
            PastTrips.Clear();
            var trips = _orderRepository.GetPastOrdersByClientId(CurrentUser.user_id);
            foreach (var trip in trips)
            {
                PastTrips.Add(trip);
            }
            
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
