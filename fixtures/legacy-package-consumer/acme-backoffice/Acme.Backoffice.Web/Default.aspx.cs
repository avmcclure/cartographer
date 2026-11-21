using System;
using Acme.Legacy.Tax;
using Acme.Pricing;

namespace Acme.Backoffice.Web
{
    public partial class Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var net = new Money(100m, "EUR");
            var gross = new PriceCalculator().WithTax(net, TaxTable.RateFor("NL"));
            Total.Text = gross.Amount.ToString("0.00");
        }
    }
}
