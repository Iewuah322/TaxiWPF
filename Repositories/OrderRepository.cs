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
        public Order CreateOrder(Order newOrder)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new SqlCommand(
                        @"INSERT INTO Orders (client_id, PointA, PointB, Status, Tariff, TotalPrice) 
                          OUTPUT INSERTED.order_id 
                          VALUES (@client_id, @PointA, @PointB, @Status, @Tariff, @TotalPrice)", connection);

                    command.Parameters.AddWithValue("@client_id", newOrder.OrderClient.client_id); // Берем ID клиента
                    command.Parameters.AddWithValue("@PointA", newOrder.PointA);
                    command.Parameters.AddWithValue("@PointB", newOrder.PointB);
                    command.Parameters.AddWithValue("@Status", (int)newOrder.Status); // Сохраняем enum как int
                    command.Parameters.AddWithValue("@Tariff", newOrder.Tariff);
                    command.Parameters.AddWithValue("@TotalPrice", newOrder.TotalPrice);
                    // driver_id пока NULL

                    // Получаем ID созданного заказа
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
                    // Обновляем статус и водителя (если он назначен)
                    var command = new SqlCommand(
                        @"UPDATE Orders SET 
                    Status = @Status, 
                    driver_id = @driver_id 
                  WHERE order_id = @order_id", connection); // <-- ПРОВЕРЬ ЭТОТ ЗАПРОС

                    command.Parameters.AddWithValue("@order_id", order.order_id);
                    command.Parameters.AddWithValue("@Status", (int)order.Status); // int <-> enum
                    command.Parameters.AddWithValue("@driver_id", (object)order.AssignedDriver?.driver_id ?? DBNull.Value);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0; // Возвращает true, если хотя бы одна строка обновлена
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении заказа {order.order_id}: {ex.Message}");
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
                       @"SELECT o.*, c.full_name as client_name, d.full_name as driver_name, d.car_model, d.license_plate, d.DriverPhotoUrl, d.CarPhotoUrl 
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
                OrderClient = new Client // Используем временный Client, чтобы передать ID и имя
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
                    full_name = reader["driver_name"]?.ToString(), // Имя водителя из JOIN
                    // Добавляем поля, которые могут быть полезны в UI
                    car_model = reader["car_model"]?.ToString(),
                    license_plate = reader["license_plate"]?.ToString(),
                    DriverPhotoUrl = reader["DriverPhotoUrl"] as string,
                    CarPhotoUrl = reader["CarPhotoUrl"] as string // Предполагаем, что CarPhotoUrl есть в Users
                };
            }

            return order;
        }
    }
}