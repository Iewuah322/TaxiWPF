using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiWPF.Models;

namespace TaxiWPF.Repositories
{
    // Это класс-заглушка для имитации работы с финансами водителя
    public class WalletRepository
    {
        // --- ЗАГЛУШКА ДАННЫХ ---
        private static decimal _balance = 1250.75m;
        private static List<PaymentCard> _savedCards = new List<PaymentCard>
{
    new PaymentCard { CardNumber = "4276000000001234", CardExpiry = "12/26" },
    new PaymentCard { CardNumber = "5555000000004567", CardExpiry = "10/25" }
};
        private static List<Transaction> _transactions = new List<Transaction>
        {
            new Transaction { Date = DateTime.Now.AddHours(-2), Description = "Поездка #8123", Amount = 150, Type = TransactionType.Earning },
            new Transaction { Date = DateTime.Now.AddHours(-5), Description = "Поездка #8120", Amount = 320, Type = TransactionType.Earning },
            new Transaction { Date = DateTime.Now.AddDays(-1), Description = "Вывод средств", Amount = -1000, Type = TransactionType.Withdrawal },
            new Transaction { Date = DateTime.Now.AddDays(-1), Description = "Поездка #8115", Amount = 220.75m, Type = TransactionType.Earning },
            new Transaction { Date = DateTime.Now.AddDays(-2), Description = "Поездка #8111", Amount = 560, Type = TransactionType.Earning },
        };
        // ------------------

        // (Мы передаем user_id, чтобы имитировать реальную БД, хотя заглушка его игнорирует)

        public decimal GetBalance(int userId)
        {
            return _balance;
        }

        public List<Transaction> GetTransactions(int userId)
        {
            return _transactions.OrderByDescending(t => t.Date).ToList();
        }

        public List<PaymentCard> GetSavedCards(int userId)
        {
            // Возвращаем копии, а не оригиналы
            return _savedCards.Select(c => new PaymentCard
            {
                CardNumber = c.CardNumber,
                CardExpiry = c.CardExpiry
            }).ToList();
        }

        public bool WithdrawFunds(int userId, decimal amount, PaymentCard card, bool saveCard)
        {
            if (amount > _balance || amount <= 0) return false;

            _balance -= amount;
            _transactions.Insert(0, new Transaction
            {
                Date = DateTime.Now,
                Description = "Вывод средств",
                Amount = -amount,
                Type = TransactionType.Withdrawal
            });

            if (saveCard)
            {
                // ==== ИЗМЕНЕННАЯ ЛОГИКА СОХРАНЕНИЯ ====
                // Убираем пробелы из номера карты для сравнения
                string cleanCardNumber = new string(card.CardNumber.Where(char.IsDigit).ToArray());

                // Если карты с таким номером еще нет, добавляем
                if (!_savedCards.Any(c => c.CardNumber == cleanCardNumber))
                {
                    _savedCards.Add(new PaymentCard
                    {
                        CardNumber = cleanCardNumber,
                        CardExpiry = card.CardExpiry
                        // CVV не сохраняем!
                    });
                }
                // ---------------------------------
            }

            return true;
        }
        public void DeleteCard(int userId, PaymentCard cardToDelete)
        {
            // Убираем пробелы из номера карты для поиска
            string cleanCardNumber = new string(cardToDelete.CardNumber.Where(char.IsDigit).ToArray());

            var card = _savedCards.FirstOrDefault(c => c.CardNumber == cleanCardNumber);
            if (card != null)
            {
                _savedCards.Remove(card);
            }
        }

        public void AddEarning(int userId, decimal amount, int orderId)
        {
            // Просто добавляем в заглушку
            _balance += amount;
            _transactions.Insert(0, new Transaction
            {
                Date = DateTime.Now,
                Description = $"Поездка #{orderId}",
                Amount = amount,
                Type = TransactionType.Earning
            });
        }
    }
}