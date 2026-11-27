using LendIt.Api.Domain;

namespace LendIt.Tests.Unit;

[Trait("Category", "Unit")]
public class LateFeeCalculatorTests
{
    private static readonly DateOnly LentOn = new(2026, 1, 1);

    [Theory]
    [InlineData(0, 0.00)]
    [InlineData(14, 0.00)]
    [InlineData(15, 0.50)]
    [InlineData(20, 3.00)]
    [InlineData(34, 10.00)]
    [InlineData(100, 10.00)]
    public void Fee_depends_on_the_number_of_days_kept(int daysKept, double expectedFee)
    {
        var fee = LateFeeCalculator.Calculate(LentOn, LentOn.AddDays(daysKept));

        Assert.Equal((decimal)expectedFee, fee);
    }

    [Fact]
    public void Returning_before_lending_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => LateFeeCalculator.Calculate(LentOn, LentOn.AddDays(-1)));
    }
}
