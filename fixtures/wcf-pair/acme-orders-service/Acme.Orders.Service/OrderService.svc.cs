using Acme.Orders.Contracts;

namespace Acme.Orders
{
    public class OrderService : IOrderService
    {
        public OrderStatus GetStatus(int orderId) => new OrderStatus { OrderId = orderId, State = "Shipped" };
    }
}
