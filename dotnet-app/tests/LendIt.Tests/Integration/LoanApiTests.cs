using System.Net;
using System.Net.Http.Json;
using LendIt.Api.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LendIt.Tests.Integration;

[Trait("Category", "Integration")]
public class LoanApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Creating_an_item_and_lending_it_updates_its_availability()
    {
        var created = await (await _client.PostAsJsonAsync("/items", new { name = "Ladder" }))
            .Content.ReadFromJsonAsync<Item>();

        var lendResponse = await _client.PostAsJsonAsync("/loans", new { itemId = created!.Id, borrower = "ana-1" });
        Assert.Equal(HttpStatusCode.Created, lendResponse.StatusCode);

        var after = await _client.GetFromJsonAsync<Item>($"/items/{created.Id}");
        Assert.False(after!.Available);
    }

    [Fact]
    public async Task Lending_an_already_lent_item_returns_409()
    {
        var created = await (await _client.PostAsJsonAsync("/items", new { name = "Saw" }))
            .Content.ReadFromJsonAsync<Item>();

        await _client.PostAsJsonAsync("/loans", new { itemId = created!.Id, borrower = "ana-2" });
        var second = await _client.PostAsJsonAsync("/loans", new { itemId = created.Id, borrower = "bob-2" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Lending_a_missing_item_returns_404()
    {
        var response = await _client.PostAsJsonAsync("/loans", new { itemId = 999_999, borrower = "ana-3" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Blank_item_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/items", new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Blank_borrower_returns_400()
    {
        var created = await (await _client.PostAsJsonAsync("/items", new { name = "Hammer" }))
            .Content.ReadFromJsonAsync<Item>();

        var response = await _client.PostAsJsonAsync("/loans", new { itemId = created!.Id, borrower = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Fourth_loan_for_same_borrower_returns_409()
    {
        for (int i = 1; i <= 3; i++)
        {
            var item = await (await _client.PostAsJsonAsync("/items", new { name = $"Tool-{i}" }))
                .Content.ReadFromJsonAsync<Item>();
            var res = await _client.PostAsJsonAsync("/loans", new { itemId = item!.Id, borrower = "ana-4" });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        var fourthItem = await (await _client.PostAsJsonAsync("/items", new { name = "Tool-4" }))
            .Content.ReadFromJsonAsync<Item>();
        var fourthResponse = await _client.PostAsJsonAsync("/loans", new { itemId = fourthItem!.Id, borrower = "ana-4" });

        Assert.Equal(HttpStatusCode.Conflict, fourthResponse.StatusCode);
    }

    [Fact]
    public async Task Return_makes_item_available_again_and_returning_twice_gives_409()
    {
        var created = await (await _client.PostAsJsonAsync("/items", new { name = "Wrench" }))
            .Content.ReadFromJsonAsync<Item>();

        var loan = await (await _client.PostAsJsonAsync("/loans", new { itemId = created!.Id, borrower = "ana-5" }))
            .Content.ReadFromJsonAsync<Loan>();

        var firstReturn = await _client.PostAsync($"/loans/{loan!.Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, firstReturn.StatusCode);

        var itemAfterReturn = await _client.GetFromJsonAsync<Item>($"/items/{created.Id}");
        Assert.True(itemAfterReturn!.Available);

        var secondReturn = await _client.PostAsync($"/loans/{loan.Id}/return", null);
        Assert.Equal(HttpStatusCode.Conflict, secondReturn.StatusCode);
    }
}
