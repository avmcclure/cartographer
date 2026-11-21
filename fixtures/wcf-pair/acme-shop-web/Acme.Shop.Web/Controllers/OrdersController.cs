using System.Web.Mvc;
using Acme.Orders.Contracts;

namespace Acme.Shop.Web.Controllers
{
    public class OrdersController : Controller
    {
        public ActionResult Status(int id)
        {
            using (var client = new OrderServiceClient())
            {
                return View(client.GetStatus(id));
            }
        }
    }
}
