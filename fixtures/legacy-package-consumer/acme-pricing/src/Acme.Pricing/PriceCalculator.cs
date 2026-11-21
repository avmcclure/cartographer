namespace Acme.Pricing;

public sealed class PriceCalculator
{
    public Money WithTax(Money net, decimal rate) => net with { Amount = decimal.Round(net.Amount * (1 + rate), 2) };
}
