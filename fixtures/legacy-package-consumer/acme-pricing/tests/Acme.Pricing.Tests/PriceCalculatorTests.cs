using Acme.Pricing;
using Xunit;

public class PriceCalculatorTests
{
    [Fact]
    public void Adds_tax() => Assert.Equal(12.00m, new PriceCalculator().WithTax(new Money(10m, "EUR"), 0.2m).Amount);
}
