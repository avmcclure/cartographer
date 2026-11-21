using System.Runtime.Serialization;
using System.ServiceModel;

namespace Acme.Orders.Contracts
{
    [ServiceContract(Namespace = "http://acme.example/orders")]
    public interface IOrderService
    {
        [OperationContract]
        OrderStatus GetStatus(int orderId);
    }

    [DataContract(Namespace = "http://acme.example/orders")]
    public class OrderStatus
    {
        [DataMember] public int OrderId { get; set; }
        [DataMember] public string State { get; set; }
    }
}
