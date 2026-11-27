using LendIt.Api.Domain;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace LendIt.Tests.Unit;

[Trait("Category", "Unit")]
public class LoanServiceTests
{
    private readonly IItemRepository _items = Substitute.For<IItemRepository>();
    private readonly ILoanRepository _loans = Substitute.For<ILoanRepository>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly LoanService _sut;

    public LoanServiceTests()
    {
        _loans.Add(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(call => new Loan(42, call.ArgAt<int>(0), call.ArgAt<string>(1), call.ArgAt<DateOnly>(2), null));

        _sut = new LoanService(_items, _loans, _clock);
    }

    [Fact]
    public void Lending_an_available_item_marks_it_unavailable_and_records_today()
    {
        _items.Get(1).Returns(new Item(1, "Drill", Available: true));
        _loans.CountActive("alice").Returns(0);

        var loan = _sut.Lend(1, "alice");

        Assert.Equal(new DateOnly(2026, 5, 1), loan.LentOn);
        _items.Received(1).Save(new Item(1, "Drill", Available: false));
    }

    [Fact]
    public void Lending_an_unavailable_item_is_rejected()
    {
        _items.Get(1).Returns(new Item(1, "Drill", Available: false));

        Assert.Throws<RuleViolationException>(() => _sut.Lend(1, "alice"));
        _loans.DidNotReceiveWithAnyArgs().Add(default, default!, default);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public void Active_loan_limit_is_three(int activeLoans, bool shouldSucceed)
    {
        _items.Get(1).Returns(new Item(1, "Drill", Available: true));
        _loans.CountActive("alice").Returns(activeLoans);

        if (shouldSucceed)
        {
            var loan = _sut.Lend(1, "alice");
            Assert.Equal("alice", loan.Borrower);
            _items.Received(1).Save(new Item(1, "Drill", Available: false));
        }
        else
        {
            Assert.Throws<RuleViolationException>(() => _sut.Lend(1, "alice"));
            _items.DidNotReceive().Save(Arg.Any<Item>());
        }
    }

    [Theory]
    [InlineData(10, 0.00)]
    [InlineData(20, 99.00)]
    public void Returning_frees_the_item_and_computes_late_fee_from_clock(int daysKept, double expectedFee)
    {
        _loans.Get(42).Returns(new Loan(42, 1, "alice", new DateOnly(2026, 5, 1), null));
        _items.Get(1).Returns(new Item(1, "Drill", Available: false));
        _clock.Advance(TimeSpan.FromDays(daysKept));

        var result = _sut.Return(42);

        Assert.Equal((decimal)expectedFee, result.LateFee);
        Assert.Equal(new DateOnly(2026, 5, 1).AddDays(daysKept), result.Loan.ReturnedOn);
        _items.Received(1).Save(new Item(1, "Drill", Available: true));
    }

    [Fact]
    public void Returning_an_already_returned_loan_is_rejected()
    {
        _loans.Get(42).Returns(new Loan(42, 1, "alice", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)));

        Assert.Throws<RuleViolationException>(() => _sut.Return(42));
    }
}
