using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiWPF.Models;
using TaxiWPF.Repositories;


namespace TaxiWPF.Services
{
    // Сервис остается Singleton, но теперь работает через OrderRepository
    public class OrderService
    {
        private readonly OrderRepository _orderRepository; // <-- Новый репозиторий
                                                           // --- СПИСОК _activeOrders УДАЛЕН ---

        private static readonly Lazy<OrderService> _instance = new Lazy<OrderService>(() => new OrderService());

        public event Action<Order> OrderUpdated;

        // Приватный конструктор для Singleton
        private OrderService()
        {
            _orderRepository = new OrderRepository(); // Создаем репозиторий один раз
        }

        public static OrderService Instance => _instance.Value;

        // Клиент вызывает этот метод
        public Order SubmitOrder(Order newOrder)
        {
            newOrder.Status = OrderState.Searching; // Устанавливаем начальный статус

            // Создаем заказ в БД через репозиторий
            var createdOrder = _orderRepository.CreateOrder(newOrder);

            if (createdOrder != null)
            {
                Notify(createdOrder); // Оповещаем о НОВОМ заказе
            }
            return createdOrder; // Возвращаем заказ с ID
        }

        // Водитель вызывает, когда принимает заказ
        public void AcceptOrder(Order order, Driver driver)
        {
            // --- НАЧАЛО ИЗМЕНЕНИЙ ---
            // Добавляем проверку, чтобы убедиться, что у нас есть корректные ID
            if (order == null || order.order_id == 0 || driver == null || driver.driver_id == 0)
            {
                System.Diagnostics.Debug.WriteLine("ОШИБКА: AcceptOrder получил неверный ID заказа или водителя.");
                return;
            }

            var existingOrder = _orderRepository.GetOrderById(order.order_id);
            // --- КОНЕЦ ИЗМЕНЕНИЙ ---

            if (existingOrder != null && existingOrder.Status == OrderState.Searching)
            {
                existingOrder.AssignedDriver = driver;
                existingOrder.Status = OrderState.DriverEnRoute;

                if (_orderRepository.UpdateOrder(existingOrder))
                {
                    Notify(existingOrder);
                }
            }
        }

        // Водитель нажал "Прибыл"
        public void DriverArrived(Order order)
        {
            UpdateOrderStatus(order, OrderState.DriverEnRoute, OrderState.DriverArrived);
        }

        // Водитель нажал "Начать поездку"
        public void StartTrip(Order order)
        {
            UpdateOrderStatus(order, OrderState.DriverArrived, OrderState.TripInProgress);
        }

        // Водитель нажал "Завершить"
        public void CompleteOrder(Order order)
        {
            UpdateOrderStatus(order, OrderState.TripInProgress, OrderState.TripCompleted);
        }

        // Вызывается, когда заказ можно убрать из вида (например, отменен клиентом или обе стороны поставили оценку)
        public void ArchiveOrder(Order order)
        {
            // --- ИЗМЕНЕНИЕ: Получаем самую свежую версию заказа из БД ---
            var freshOrder = _orderRepository.GetOrderById(order.order_id);
            if (freshOrder == null) return;

            if (freshOrder.Status == OrderState.TripCompleted)
            {
                UpdateOrderStatus(freshOrder, OrderState.TripCompleted, OrderState.Archived);
            }
            else if (freshOrder.Status == OrderState.Searching)
            {
                UpdateOrderStatus(freshOrder, OrderState.Searching, OrderState.Archived);
            }
        }


        // Водитель вызывает этот метод (делегируем репозиторию)
        public List<Order> GetAvailableOrders()
        {
            // Просто возвращаем результат из репозитория
            return _orderRepository.GetAvailableOrders();
        }

        // Вспомогательный метод для обновления статуса заказа в БД
        private void UpdateOrderStatus(Order orderWithDriverInfo, OrderState expectedCurrentState, OrderState newState)
        {
            var existingOrder = _orderRepository.GetOrderById(orderWithDriverInfo.order_id);

            // Обновляем, только если текущий статус совпадает с ожидаемым
            if (existingOrder != null && existingOrder.Status == expectedCurrentState)
            {
                existingOrder.Status = newState;
                // --- НАЧАЛО ИСПРАВЛЕНИЯ ---
                // Сохраняем информацию о водителе и машине из принятого заказа,
                // так как GetOrderById не загружает данные о машине.
                existingOrder.AssignedDriver = orderWithDriverInfo.AssignedDriver;
                // --- КОНЕЦ ИСПРАВЛЕНИЯ ---

                if (_orderRepository.UpdateOrder(existingOrder))
                {
                    Notify(existingOrder); // Оповещаем об изменении
                }
            }
        }


        // Главный оповещатель (остался без изменений)
        private void Notify(Order order)
        {
            OrderUpdated?.Invoke(order);
        }
    }
}
