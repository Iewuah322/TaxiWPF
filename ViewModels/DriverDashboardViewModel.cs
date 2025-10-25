using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using TaxiWPF.Models;
using TaxiWPF.Repositories;
using TaxiWPF.Views;



namespace TaxiWPF.ViewModels
{
    public class DriverDashboardViewModel : INotifyPropertyChanged
    {
        private readonly User _currentUser;
        private readonly WalletRepository _walletRepository;

        private decimal _totalBalance;
        private string _cardNumber;
        private string _cardExpiry;
        private string _cardCVV;
        private bool _rememberCard;
        private decimal _withdrawAmount;
        private bool _isWithdrawPanelVisible = false;
        private string _amountError;
        private string _cardNumberError;
        private string _cardExpiryError;
        private string _cvvError;
        public ICommand DeleteCardCommand { get; }
        

        public ObservableCollection<PaymentCard> SavedCards { get; set; }
        private PaymentCard _selectedSavedCard;

        public PaymentCard SelectedSavedCard
        {
            get => _selectedSavedCard;
            set
            {
                _selectedSavedCard = value;
                OnPropertyChanged();
                LoadCardFromSelection(value);

                // ==== ДОБАВЬ ЭТУ СТРОКУ ====
                (DeleteCardCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }


        public string AmountError { get => _amountError; set { _amountError = value; OnPropertyChanged(); } }
        public string CardNumberError { get => _cardNumberError; set { _cardNumberError = value; OnPropertyChanged(); } }
        public string CardExpiryError { get => _cardExpiryError; set { _cardExpiryError = value; OnPropertyChanged(); } }
        public string CvvError { get => _cvvError; set { _cvvError = value; OnPropertyChanged(); } }
        public ObservableCollection<Transaction> RecentEarnings { get; set; }
        public ObservableCollection<Transaction> RecentWithdrawals { get; set; }

        // Данные для графика (Заглушка)
        public ObservableCollection<KeyValuePair<string, double>> DailyEarnings { get; set; }

        public decimal TotalBalance { get => _totalBalance; set { _totalBalance = value; OnPropertyChanged(); } }
        public string CardNumber
        {
            get => _cardNumber;
            set
            {
                // ... (твой код форматирования "1234 5678 ...")
                string digits = new string(value.Where(char.IsDigit).ToArray());
                if (digits.Length > 16) digits = digits.Substring(0, 16);
                string formatted = string.Join(" ", Enumerable.Range(0, (digits.Length + 3) / 4)
                    .Select(i => digits.Substring(i * 4, Math.Min(4, digits.Length - i * 4))));
                _cardNumber = formatted;
                // ... (конец форматирования)

                OnPropertyChanged();
                ValidateCardNumber(); // Валидируем только это поле
            }
        }

        public string CardExpiry
        {
            get => _cardExpiry;
            set
            {
                // ... (твой код форматирования "MM/YY")
                string digits = new string(value.Where(char.IsDigit).ToArray());
                if (digits.Length > 4) digits = digits.Substring(0, 4);
                string formatted = digits;
                if (digits.Length > 2)
                {
                    formatted = digits.Insert(2, "/");
                }
                _cardExpiry = formatted;
                // ... (конец форматирования)

                OnPropertyChanged();
                ValidateCardExpiry(); // Валидируем только это поле
            }
        }

        public string CardCVV
        {
            get => _cardCVV;
            set
            {
                // ... (твой код форматирования CVV)
                string digits = new string(value.Where(char.IsDigit).ToArray());
                if (digits.Length > 3) digits = digits.Substring(0, 3);
                _cardCVV = digits;
                // ... (конец форматирования)

                OnPropertyChanged();
                ValidateCvv(); // Валидируем только это поле
            }
        }

        public bool RememberCard { get => _rememberCard; set { _rememberCard = value; OnPropertyChanged(); } }
        public decimal WithdrawAmount
        {
            get => _withdrawAmount;
            set
            {
                _withdrawAmount = value;
                OnPropertyChanged();
                ValidateAmount(); // Валидируем только это поле
            }
        }
        public bool IsWithdrawPanelVisible { get => _isWithdrawPanelVisible; set { _isWithdrawPanelVisible = value; OnPropertyChanged(); } }


        private void ValidateAmount(bool forceUpdate = false)
        {
            if (WithdrawAmount <= 0)
                AmountError = "Сумма должна быть больше нуля.";
            else if (WithdrawAmount > TotalBalance)
                AmountError = "Недостаточно средств на балансе.";
            else
                AmountError = null;

            if (forceUpdate) (WithdrawCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ValidateCardNumber(bool forceUpdate = false)
        {
            if (string.IsNullOrWhiteSpace(CardNumber) || CardNumber.Length < 19) // 16 цифр + 3 пробела
                CardNumberError = "Введите полный номер карты (16 цифр).";
            else
                CardNumberError = null;

            if (forceUpdate) (WithdrawCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ValidateCardExpiry(bool forceUpdate = false)
        {
            if (string.IsNullOrWhiteSpace(CardExpiry) || CardExpiry.Length < 5) // MM/YY
                CardExpiryError = "Введите срок действия (ММ/ГГ).";
            else
                CardExpiryError = null;

            if (forceUpdate) (WithdrawCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ValidateCvv(bool forceUpdate = false)
        {
            if (string.IsNullOrWhiteSpace(CardCVV) || CardCVV.Length < 3)
                CvvError = "Введите CVV (3 цифры).";
            else
                CvvError = null;

            if (forceUpdate) (WithdrawCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }



        private bool CanWithdraw()
        {
            // Пере-валидируем все поля на случай, если TotalBalance изменился
            ValidateAmount();
            ValidateCardNumber();
            ValidateCardExpiry();
            ValidateCvv();

            return string.IsNullOrEmpty(AmountError) &&
                   string.IsNullOrEmpty(CardNumberError) &&
                   string.IsNullOrEmpty(CardExpiryError) &&
                   string.IsNullOrEmpty(CvvError);
        }

        public ICommand ToggleWithdrawPanelCommand { get; }
        public ICommand WithdrawCommand { get; }
        public ICommand GoToMapCommand { get; }
        public ICommand LoadSavedCardCommand { get; }

        public DriverDashboardViewModel(User user)
        {
            _currentUser = user;
            _walletRepository = new WalletRepository();

            RecentEarnings = new ObservableCollection<Transaction>();
            RecentWithdrawals = new ObservableCollection<Transaction>();
            DailyEarnings = new ObservableCollection<KeyValuePair<string, double>>();

            // ==== ВОТ ЭТА СТРОКА, СКОРЕЕ ВСЕГО, ПРОПУЩЕНА ====
            // ==== Убедись, что она здесь есть ====
            SavedCards = new ObservableCollection<PaymentCard>();
            // ===============================================

            ToggleWithdrawPanelCommand = new RelayCommand(() => IsWithdrawPanelVisible = !IsWithdrawPanelVisible);
            WithdrawCommand = new RelayCommand(Withdraw, CanWithdraw);
            GoToMapCommand = new RelayCommand(GoToMap);
            DeleteCardCommand = new RelayCommand(DeleteCard, CanDeleteCard); // <-- ДОБАВЬ ЭТУ СТРОКУ

            LoadDashboardData();
        }

        private bool CanDeleteCard()
        {
            // Мы можем удалить карту, только если она выбрана И это не "Новая карта"
            return SelectedSavedCard != null && !string.IsNullOrEmpty(SelectedSavedCard.CardNumber);
        }

        private void DeleteCard()
        {
            if (!CanDeleteCard()) return;

            var cardToDelete = SelectedSavedCard;

            // 1. Говорим репозиторию удалить ее (для БД)
            _walletRepository.DeleteCard(_currentUser.user_id, cardToDelete);

            // 2. Удаляем ее из нашего списка в UI
            SavedCards.Remove(cardToDelete);

            // 3. Сбрасываем выбор на "Новая карта"
            SelectedSavedCard = SavedCards.FirstOrDefault(c => string.IsNullOrEmpty(c.CardNumber));
        }

        private void LoadCardFromSelection(PaymentCard card)
        {
            if (card == null || string.IsNullOrEmpty(card.CardNumber))
            {
                // Если выбрали "Новая карта" (или сбросили выбор)
                CardNumber = "";
                CardExpiry = "";
                CardCVV = "";
                RememberCard = true; // По умолчанию предлагаем сохранить новую карту
            }
            else
            {
                // Заполняем поля из выбранной карты
                CardNumber = card.CardNumber;
                CardExpiry = card.CardExpiry;
                CardCVV = ""; // CVV всегда вводим заново
                RememberCard = false; // Карта уже сохранена
            }
        }


        private void LoadDashboardData()
        {
            TotalBalance = _walletRepository.GetBalance(_currentUser.user_id);
            var allTransactions = _walletRepository.GetTransactions(_currentUser.user_id);

            SavedCards.Clear();
            // Добавляем "пустую" карту, чтобы можно было ввести новую
            SavedCards.Add(new PaymentCard { CardNumber = null, CardExpiry = null }); // Отобразится как "Новая карта"

            // Загружаем сохраненные
            var cardsFromRepo = _walletRepository.GetSavedCards(_currentUser.user_id);
            foreach (var card in cardsFromRepo)
            {
                SavedCards.Add(card);
            }


            // Разделяем на 2 списка для UI
            RecentEarnings.Clear();
            allTransactions.Where(t => t.Type == TransactionType.Earning).Take(5).ToList().ForEach(t => RecentEarnings.Add(t));

            RecentWithdrawals.Clear();
            allTransactions.Where(t => t.Type == TransactionType.Withdrawal).Take(3).ToList().ForEach(w => RecentWithdrawals.Add(w));

            // --- Заглушка для Графика ---
            DailyEarnings.Clear();
            var rand = new Random();
            for (int i = 6; i >= 0; i--)
            {
                // \n - это перенос строки. Теперь будет "Пт\n25.10"
                DailyEarnings.Add(new KeyValuePair<string, double>(DateTime.Now.AddDays(-i).ToString("ddd\ndd.MM"), rand.Next(50, 200)));
            }
        }

        

       

        private void Withdraw()
        {
            var card = new PaymentCard
            {
                CardNumber = this.CardNumber,
                CardExpiry = this.CardExpiry,
                CardCVV = this.CardCVV // В реальном приложении CVV не передается на сервер
            };

            if (_walletRepository.WithdrawFunds(_currentUser.user_id, WithdrawAmount, card, RememberCard))
            {
                MessageBox.Show($"Запрос на вывод {WithdrawAmount:C} успешно создан.\nСредства поступят в течение 3-5 рабочих дней.", "Успех");
                LoadDashboardData(); // Обновляем баланс и списки
                IsWithdrawPanelVisible = false; // Скрываем панель
                WithdrawAmount = 0;
                CardCVV = "";
            }
            else
            {
                MessageBox.Show("Ошибка вывода. Проверьте сумму и баланс.", "Ошибка");
            }
        }

        private void GoToMap()
        {
            // 1. Создаем и открываем окно выбора авто
            var carSelectionView = new CarSelectionView(_currentUser);

            // 2. Показываем его как модальное окно (ShowDialog)
            //    Программа "встанет на паузу" здесь, пока окно не закроется
            bool? dialogResult = carSelectionView.ShowDialog();

            // 3. Проверяем, нажал ли водитель "Выбрать" (а не "Отмена")
            if (dialogResult == true)
            {
                // 4. Получаем выбранную машину из окна
                Car selectedCar = carSelectionView.SelectedCar;

                // 5. Открываем карту, ПЕРЕДАВАЯ В НЕЕ ВЫБРАННУЮ МАШИНУ
                var driverVM = new DriverViewModel(_currentUser, selectedCar);
                var driverView = new DriverView();
                driverView.DataContext = driverVM;
                driverView.Show();

                // 6. Закрываем этот дашборд
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        window.Close();
                        break;
                    }
                }
            }
            // Если dialogResult == false (нажата "Отмена"), ничего не делаем
            // и просто остаемся в Дашборде.
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
