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
            // Получаем ПОСЛЕДНЮЮ версию заказа из БД (вдруг статус уже изменился)
            var existingOrder = _orderRepository.GetOrderById(order.order_id);

            if (existingOrder != null && existingOrder.Status == OrderState.Searching)
            {
                existingOrder.AssignedDriver = driver; // Присваиваем водителя
                existingOrder.Status = OrderState.DriverEnRoute; // Меняем статус

                // Обновляем заказ в БД
                if (_orderRepository.UpdateOrder(existingOrder))
                {
                    Notify(existingOrder); // Оповещаем об изменении статуса
                }
            }
            // else: Либо заказ не найден, либо уже принят/отменен - ничего не делаем
        }

        // Водитель нажал "Прибыл"
        public void DriverArrived(Order order)
        {
            UpdateOrderStatus(order.order_id, OrderState.DriverEnRoute, OrderState.DriverArrived);
        }

        // Водитель нажал "Начать поездку"
        public void StartTrip(Order order)
        {
            UpdateOrderStatus(order.order_id, OrderState.DriverArrived, OrderState.TripInProgress);
        }

        // Водитель нажал "Завершить"
        public void CompleteOrder(Order order)
        {
            UpdateOrderStatus(order.order_id, OrderState.TripInProgress, OrderState.TripCompleted);
        }

        // Вызывается, когда заказ можно убрать из вида (например, отменен клиентом или обе стороны поставили оценку)
        public void ArchiveOrder(Order order)
        {
            // В реальной системе статус Archived может быть не нужен,
            // или можно просто удалить заказ, если оценки поставлены.
            // Пока просто обновим статус.
            UpdateOrderStatus(order.order_id, OrderState.TripCompleted, OrderState.Archived);
            // Или можно добавить UpdateOrderStatus(order.order_id, OrderState.Searching, OrderState.Archived); для отмены
        }


        // Водитель вызывает этот метод (делегируем репозиторию)
        public List<Order> GetAvailableOrders()
        {
            // Просто возвращаем результат из репозитория
            return _orderRepository.GetAvailableOrders();
        }

        // Вспомогательный метод для обновления статуса заказа в БД
        private void UpdateOrderStatus(int orderId, OrderState expectedCurrentState, OrderState newState)
        {
            var existingOrder = _orderRepository.GetOrderById(orderId);

            // Обновляем, только если текущий статус совпадает с ожидаемым
            if (existingOrder != null && existingOrder.Status == expectedCurrentState)
            {
                existingOrder.Status = newState;
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
