using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiWPF.Models;
using System.Data.SqlClient; // <-- Добавлено
using System.Configuration; // <-- Добавлено
using System.Windows;

namespace TaxiWPF.Repositories
{
    public class WalletRepository
    {
        private readonly string _connectionString; // <-- Добавлено

        // --- ЗАГЛУШКА ДАННЫХ УДАЛЕНА ---

        public WalletRepository() // <-- Добавлен конструктор
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaxiDB"].ConnectionString;
        }


        // Получаем текущий баланс водителя (сумма всех транзакций)
        public decimal GetBalance(int driverId)
        {
            decimal balance = 0;
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // Суммируем поле Amount для конкретного водителя
                    var command = new SqlCommand(
                        "SELECT ISNULL(SUM(Amount), 0) FROM WalletTransactions WHERE driver_id = @driverId",
                        connection);
                    command.Parameters.AddWithValue("@driverId", driverId);

                    // ExecuteScalar возвращает одно значение (первая ячейка результата)
                    balance = (decimal)command.ExecuteScalar();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении баланса: {ex.Message}");
                    MessageBox.Show($"Не удалось загрузить баланс: {ex.Message}", "Ошибка БД");
                }
            }
            return balance;
        }

        // Получаем список транзакций (например, последние 20)
        public List<Transaction> GetTransactions(int driverId, int limit = 20)
        {
            var transactions = new List<Transaction>();
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // Выбираем последние 'limit' транзакций, сортируя по дате
                    var command = new SqlCommand(
                        @"SELECT TOP (@limit) * FROM WalletTransactions 
                          WHERE driver_id = @driverId 
                          ORDER BY TransactionDate DESC", connection);
                    command.Parameters.AddWithValue("@driverId", driverId);
                    command.Parameters.AddWithValue("@limit", limit);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            transactions.Add(new Transaction
                            {
                                Date = (DateTime)reader["TransactionDate"],
                                Description = reader["Description"].ToString(),
                                Amount = (decimal)reader["Amount"],
                                Type = (TransactionType)(int)reader["TransactionType"] // Преобразуем int в enum
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении транзакций: {ex.Message}");
                    MessageBox.Show($"Не удалось загрузить историю операций: {ex.Message}", "Ошибка БД");
                }
            }
            return transactions;
        }

        // Получаем сохраненные карты водителя
        public List<PaymentCard> GetSavedCards(int driverId)
        {
            var cards = new List<PaymentCard>();
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new SqlCommand("SELECT MaskedNumber, CardExpiry FROM PaymentCards WHERE driver_id = @driverId", connection);
                    command.Parameters.AddWithValue("@driverId", driverId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cards.Add(new PaymentCard
                            {
                                // Важно: В CardNumber записываем маску из БД
                                CardNumber = reader["MaskedNumber"].ToString(),
                                CardExpiry = reader["CardExpiry"].ToString()
                                // CVV не храним и не читаем
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении карт: {ex.Message}");
                    MessageBox.Show($"Не удалось загрузить сохраненные карты: {ex.Message}", "Ошибка БД");
                }
            }
            return cards;
        }

        // Вывод средств
        public bool WithdrawFunds(int driverId, decimal amount, PaymentCard card, bool saveCard)
        {
            if (amount <= 0) return false; // Сумма должна быть положительной

            using (var connection = new SqlConnection(_connectionString))
            {
                SqlTransaction transaction = null;
                try
                {
                    connection.Open();
                    transaction = connection.BeginTransaction();

                    // 1. Проверяем баланс (пессимистическая блокировка на всякий случай)
                    //    В реальной системе нужна более сложная логика проверки и блокировки
                    var balanceCmd = new SqlCommand(
                        "SELECT ISNULL(SUM(Amount), 0) FROM WalletTransactions WHERE driver_id = @driverId",
                         connection, transaction);
                    balanceCmd.Parameters.AddWithValue("@driverId", driverId);
                    decimal currentBalance = (decimal)balanceCmd.ExecuteScalar();

                    if (amount > currentBalance)
                    {
                        MessageBox.Show("Недостаточно средств на балансе.", "Ошибка вывода");
                        transaction.Rollback();
                        return false;
                    }

                    // 2. Добавляем транзакцию вывода (сумма отрицательная)
                    var withdrawCmd = new SqlCommand(
                        @"INSERT INTO WalletTransactions (driver_id, Description, Amount, TransactionType) 
                          VALUES (@driverId, @Description, @Amount, @TransactionType)", connection, transaction);
                    withdrawCmd.Parameters.AddWithValue("@driverId", driverId);
                    withdrawCmd.Parameters.AddWithValue("@Description", "Вывод средств");
                    withdrawCmd.Parameters.AddWithValue("@Amount", -amount); // Сумма с минусом
                    withdrawCmd.Parameters.AddWithValue("@TransactionType", (int)TransactionType.Withdrawal); // Тип = Вывод
                    withdrawCmd.ExecuteNonQuery();

                    // 3. Сохраняем карту, если нужно
                    if (saveCard)
                    {
                        // Генерируем маску
                        string cleanCardNumber = new string(card.CardNumber.Where(char.IsDigit).ToArray());
                        string maskedNumber = "Карта";
                        if (cleanCardNumber.Length >= 4)
                        {
                            if (cleanCardNumber.StartsWith("4")) maskedNumber = "Visa";
                            else if (cleanCardNumber.StartsWith("5")) maskedNumber = "Mastercard";
                            // ... можно добавить другие системы
                            maskedNumber += " **** " + cleanCardNumber.Substring(cleanCardNumber.Length - 4);
                        }

                        // Проверяем, есть ли уже такая маска у водителя
                        var checkCardCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM PaymentCards WHERE driver_id = @driverId AND MaskedNumber = @MaskedNumber",
                             connection, transaction);
                        checkCardCmd.Parameters.AddWithValue("@driverId", driverId);
                        checkCardCmd.Parameters.AddWithValue("@MaskedNumber", maskedNumber);

                        if ((int)checkCardCmd.ExecuteScalar() == 0)
                        {
                            // Если такой маски нет, добавляем
                            var addCardCmd = new SqlCommand(
                               @"INSERT INTO PaymentCards (driver_id, MaskedNumber, CardExpiry) 
                                  VALUES (@driverId, @MaskedNumber, @CardExpiry)", connection, transaction);
                            addCardCmd.Parameters.AddWithValue("@driverId", driverId);
                            addCardCmd.Parameters.AddWithValue("@MaskedNumber", maskedNumber);
                            addCardCmd.Parameters.AddWithValue("@CardExpiry", card.CardExpiry);
                            addCardCmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при выводе средств: {ex.Message}");
                    MessageBox.Show($"Ошибка при выводе средств: {ex.Message}", "Ошибка БД");
                    try { transaction?.Rollback(); } catch { }
                    return false;
                }
            }
        }

        // Удаление сохраненной карты
        public bool DeleteCard(int driverId, PaymentCard cardToDelete)
        {
            // Используем маску для удаления
            string maskedNumber = cardToDelete.MaskedName; // Используем свойство MaskedName для получения маски
            if (maskedNumber == "Новая карта") return false; // Нельзя удалить "Новую карту"

            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new SqlCommand(
                        "DELETE FROM PaymentCards WHERE driver_id = @driverId AND MaskedNumber = @MaskedNumber",
                         connection);
                    command.Parameters.AddWithValue("@driverId", driverId);
                    command.Parameters.AddWithValue("@MaskedNumber", maskedNumber);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении карты: {ex.Message}");
                    MessageBox.Show($"Ошибка при удалении карты: {ex.Message}", "Ошибка БД");
                    return false;
                }
            }
        }

        // Добавление заработка (вызывается из OrderService или DriverViewModel)
        public void AddEarning(int driverId, decimal amount, int orderId)
        {
            if (amount <= 0) return;

            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new SqlCommand(
                       @"INSERT INTO WalletTransactions (driver_id, Description, Amount, TransactionType) 
                          VALUES (@driverId, @Description, @Amount, @TransactionType)", connection);
                    command.Parameters.AddWithValue("@driverId", driverId);
                    command.Parameters.AddWithValue("@Description", $"Поездка #{orderId}");
                    command.Parameters.AddWithValue("@Amount", amount); // Сумма положительная
                    command.Parameters.AddWithValue("@TransactionType", (int)TransactionType.Earning); // Тип = Заработок
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    // ВАЖНО: Эту ошибку нельзя просто показать водителю,
                    // так как она может произойти в фоновом режиме. Нужен лог.
                    System.Diagnostics.Debug.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА: Не удалось добавить заработок водителю {driverId} за заказ {orderId}: {ex.Message}");
                    // В реальном приложении здесь должна быть запись в лог-файл или систему мониторинга.
                }
            }
        }
    }
}