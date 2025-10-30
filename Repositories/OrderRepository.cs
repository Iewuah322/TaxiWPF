using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TaxiWPF.Models;


namespace TaxiWPF.Repositories
{
    // Репозиторий для работы с таблицей Orders
    public class OrderRepository
    {
        private readonly string _connectionString;

        public OrderRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaxiDB"].ConnectionString;
        }

        // Создание нового заказа


        public List<Order> GetPastOrdersByClientId(int clientId)
        {
            var orders = new List<Order>();
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // --- НАЧАЛО ИСПРАВЛЕНИЯ: Добавлено o.PaymentMethod в запрос ---
                    var command = new SqlCommand(
                        @"SELECT o.*, c.full_name as client_name, d.full_name as driver_name, d.DriverPhotoUrl 
                   FROM Orders o
                   JOIN Users c ON o.client_id = c.user_id 
                   LEFT JOIN Users d ON o.driver_id = d.user_id
                   WHERE o.client_id = @clientId AND o.Status IN (@completed, @archived)
                   ORDER BY o.order_id DESC", connection);
                    // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
                    command.Parameters.AddWithValue("@clientId", clientId);
                    command.Parameters.AddWithValue("@completed", (int)OrderState.TripCompleted);
                    command.Parameters.AddWithValue("@archived", (int)OrderState.Archived);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(MapReaderToOrder(reader));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении истории заказов: {ex.Message}");
                }
            }
            return orders;
        }

        public Order CreateOrder(Order newOrder)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // ИЗМЕНЕН SQL-ЗАПРОС: Добавлены поле PaymentMethod и параметр @PaymentMethod
                    var command = new SqlCommand(
                        @"INSERT INTO Orders (client_id, PointA, PointB, Status, Tariff, TotalPrice, PaymentMethod) 
                  OUTPUT INSERTED.order_id 
                  VALUES (@client_id, @PointA, @PointB, @Status, @Tariff, @TotalPrice, @PaymentMethod)", connection);

                    command.Parameters.AddWithValue("@client_id", newOrder.OrderClient.client_id);
                    command.Parameters.AddWithValue("@PointA", newOrder.PointA);
                    command.Parameters.AddWithValue("@PointB", newOrder.PointB);
                    command.Parameters.AddWithValue("@Status", (int)newOrder.Status);
                    command.Parameters.AddWithValue("@Tariff", newOrder.Tariff);
                    command.Parameters.AddWithValue("@TotalPrice", newOrder.TotalPrice);
                    // ДОБАВЛЕНО: Передаем способ оплаты в базу данных
                    command.Parameters.AddWithValue("@PaymentMethod", (object)newOrder.PaymentMethod ?? DBNull.Value);

                    newOrder.order_id = (int)command.ExecuteScalar();
                    return newOrder;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при создании заказа: {ex.Message}");
                    MessageBox.Show($"Не удалось создать заказ: {ex.Message}", "Ошибка БД");
                    return null;
                }
            }
        }

        // Обновление статуса и/или водителя заказа
        public bool UpdateOrder(Order order)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // Максимально упрощенный и прямой запрос для диагностики
                    var command = new SqlCommand(
                        @"UPDATE Orders SET 
                    Status = @Status, 
                    driver_id = @driver_id 
                  WHERE order_id = @order_id", connection);

                    command.Parameters.AddWithValue("@order_id", order.order_id);
                    command.Parameters.AddWithValue("@Status", (int)order.Status);

                    // Самый надежный способ передать ID водителя или NULL
                    if (order.AssignedDriver != null && order.AssignedDriver.driver_id > 0)
                    {
                        command.Parameters.AddWithValue("@driver_id", order.AssignedDriver.driver_id);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@driver_id", DBNull.Value);
                    }

                    int rowsAffected = command.ExecuteNonQuery();

                    // Логирование результата в консоль отладки
                    if (rowsAffected == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"ПРЕДУПРЕЖДЕНИЕ: Запрос UPDATE не затронул ни одной строки для order_id {order.order_id}. Проверьте, существует ли такой заказ.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"УСПЕХ: Обновлено {rowsAffected} строк для order_id {order.order_id}. Новый статус: {order.Status}, driver_id: {order.AssignedDriver?.driver_id}");
                    }

                    return rowsAffected > 0;
                }
                catch (Exception ex)
                {
                    // Логирование критической ошибки
                    System.Diagnostics.Debug.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА в UpdateOrder для заказа {order.order_id}: {ex.Message}");
                    MessageBox.Show($"Ошибка при обновлении заказа: {ex.Message}", "Ошибка БД");
                    return false;
                }
            }
        }

        // Получение заказа по ID (включая данные клиента и водителя, если есть)
        public Order GetOrderById(int orderId)
        {
            Order order = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // Используем LEFT JOIN, чтобы получить заказ, даже если водитель еще не назначен
                    var command = new SqlCommand(
   @"SELECT o.*, c.full_name as client_name, d.full_name as driver_name, d.DriverPhotoUrl 
     FROM Orders o
     JOIN Users c ON o.client_id = c.user_id 
     LEFT JOIN Users d ON o.driver_id = d.user_id
     WHERE o.order_id = @order_id", connection);
                    command.Parameters.AddWithValue("@order_id", orderId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            order = MapReaderToOrder(reader);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении заказа {orderId}: {ex.Message}");
                }
            }
            return order;
        }

        // Получение списка доступных заказов (для водителя)
        public List<Order> GetAvailableOrders()
        {
            var orders = new List<Order>();
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // Выбираем заказы со статусом Searching (1) и присоединяем имя клиента
                    var command = new SqlCommand(
                        @"SELECT o.*, c.full_name as client_name 
                           FROM Orders o
                           JOIN Users c ON o.client_id = c.user_id 
                           WHERE o.Status = @Status AND o.driver_id IS NULL", connection);
                    command.Parameters.AddWithValue("@Status", (int)OrderState.Searching);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(MapReaderToOrder(reader));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении доступных заказов: {ex.Message}");
                }
            }
            return orders;
        }

        // Вспомогательный метод для маппинга данных из SqlDataReader в объект Order
        private Order MapReaderToOrder(SqlDataReader reader)
        {
            var order = new Order
            {
                order_id = (int)reader["order_id"],
                PointA = reader["PointA"].ToString(),
                PointB = reader["PointB"].ToString(),
                Status = (OrderState)(int)reader["Status"], // Преобразуем int в enum
                Tariff = reader["Tariff"].ToString(),
                TotalPrice = (decimal)reader["TotalPrice"],

                PaymentMethod = reader["PaymentMethod"] as string,
                OrderClient = new Client
                {
                    client_id = (int)reader["client_id"],
                    full_name = reader["client_name"]?.ToString() // Имя клиента из JOIN
                }
            };
            

            // Проверяем, есть ли водитель (driver_id не NULL?)
            if (reader["driver_id"] != DBNull.Value)
            {
                order.AssignedDriver = new Driver
                {
                    driver_id = (int)reader["driver_id"],
                    full_name = reader["driver_name"]?.ToString(),
                    DriverPhotoUrl = reader["DriverPhotoUrl"] as string
                };
            }

            return order;
        }
    }
}