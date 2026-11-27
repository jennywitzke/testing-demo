using System.Net;
using System.Net.Http.Json;

namespace LendIt.E2E.Tests;

[Trait("Category", "E2E")]
public class LendItJourneyTests
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";

    private readonly HttpClient _client = new() { BaseAddress = new Uri(BaseUrl) };

    private record ItemDto(int Id, string Name, bool Available);
    private record LoanDto(int Id, int ItemId, string Borrower);

    [Fact]
    public async Task Full_item_lifecycle_journey()
    {
        var borrower1 = $"alice-{Guid.NewGuid():N}";
        var borrower2 = $"bob-{Guid.NewGuid():N}";

        var createRes = await _client.PostAsJsonAsync("/items", new { name = "E2E Drill" });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var item = await createRes.Content.ReadFromJsonAsync<ItemDto>();

        var lendRes = await _client.PostAsJsonAsync("/loans", new { itemId = item!.Id, borrower = borrower1 });
        Assert.Equal(HttpStatusCode.Created, lendRes.StatusCode);
        var loan = await lendRes.Content.ReadFromJsonAsync<LoanDto>();

        var afterLend = await _client.GetFromJsonAsync<ItemDto>($"/items/{item.Id}");
        Assert.False(afterLend!.Available);

        var secondLend = await _client.PostAsJsonAsync("/loans", new { itemId = item.Id, borrower = borrower2 });
        Assert.Equal(HttpStatusCode.Conflict, secondLend.StatusCode);

        var returnRes = await _client.PostAsync($"/loans/{loan!.Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, returnRes.StatusCode);

        var afterReturn = await _client.GetFromJsonAsync<ItemDto>($"/items/{item.Id}");
        Assert.True(afterReturn!.Available);
    }

    [Fact]
    public async Task Loan_limit_journey_allows_fourth_after_returning_one()
    {
        var borrower = $"limit-user-{Guid.NewGuid():N}";
        var loans = new List<LoanDto>();

        for (int i = 1; i <= 3; i++)
        {
            var item = await (await _client.PostAsJsonAsync("/items", new { name = $"Tool-{i}" }))
                .Content.ReadFromJsonAsync<ItemDto>();
            var res = await _client.PostAsJsonAsync("/loans", new { itemId = item!.Id, borrower });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            loans.Add((await res.Content.ReadFromJsonAsync<LoanDto>())!);
        }

        var fourthItem = await (await _client.PostAsJsonAsync("/items", new { name = "Tool-4" }))
            .Content.ReadFromJsonAsync<ItemDto>();
        var refused = await _client.PostAsJsonAsync("/loans", new { itemId = fourthItem!.Id, borrower });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var returnRes = await _client.PostAsync($"/loans/{loans[0].Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, returnRes.StatusCode);

        var succeeded = await _client.PostAsJsonAsync("/loans", new { itemId = fourthItem.Id, borrower });
        Assert.Equal(HttpStatusCode.Created, succeeded.StatusCode);
    }
}
