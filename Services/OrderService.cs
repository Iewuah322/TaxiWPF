using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiWPF.Models;
    

namespace TaxiWPF.Services
{
    // Это будет наш "фейковый сервер"
    // Используем "Singleton" паттерн (один экземпляр на все приложение)
    public class OrderService
    {
        // --- Наша "база данных" заказов ---
        private readonly List<Order> _activeOrders = new List<Order>();
        private static readonly Lazy<OrderService> _instance = new Lazy<OrderService>(() => new OrderService());
        private int _orderCounter = 1;

        // --- Публичный доступ к сервису ---
        public static OrderService Instance => _instance.Value;

        // Клиент вызывает этот метод
        public async Task<Order> SubmitOrder(Order newOrder)
        {
            // Имитируем, что заказ попадает в систему
            newOrder.order_id = _orderCounter++;
            newOrder.Status = "Поиск водителя";

            // (В будущем здесь будет логика поиска водителя по GeoPosition)

            _activeOrders.Add(newOrder);

            // Имитируем поиск
            await Task.Delay(3000);

            // В нашей заглушке водитель "находится" мгновенно
            // В реальной БД мы бы ждали, пока водитель примет заказ
            newOrder.Status = "Водитель назначен";
            newOrder.AssignedDriver = new Driver { full_name = "Иванов Иван (Заглушка)" }; // Фейковый водитель

            return newOrder;
        }

        // Водитель вызывает этот метод
        public List<Order> GetAvailableOrders()
        {
            // Возвращаем все заказы, которые ищут водителя
            // В нашей простой заглушке мы просто вернем все,
            // как будто они все "новые"
            return _activeOrders
                .Where(o => o.Status == "Поиск водителя")
                .ToList();
        }

        // Водитель вызывает, когда принимает заказ
        public void AcceptOrder(Order order, Driver driver)
        {
            var existingOrder = _activeOrders.FirstOrDefault(o => o.order_id == order.order_id);
            if (existingOrder != null)
            {
                existingOrder.AssignedDriver = driver;
                existingOrder.Status = "Водитель едет к клиенту";
            }
        }

        // Водитель вызывает, когда завершает заказ
        public void CompleteOrder(Order order)
        {
            var existingOrder = _activeOrders.FirstOrDefault(o => o.order_id == order.order_id);
            if (existingOrder != null)
            {
                existingOrder.Status = "Завершен";
                // Здесь мы могли бы переместить его в "архив"
                _activeOrders.Remove(existingOrder);
            }
        }
    }
}
