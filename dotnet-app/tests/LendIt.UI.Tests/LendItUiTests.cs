using System.Net.Http.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace LendIt.UI.Tests;

[Category("UI")]
[Parallelizable(ParallelScope.Self)]
public class LendItUiTests : PageTest
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";

    private static readonly HttpClient Api = new() { BaseAddress = new Uri(BaseUrl) };

    private record ItemDto(int Id, string Name, bool Available);

    private static async Task<ItemDto> CreateItemViaApiAsync(string name)
    {
        var res = await Api.PostAsJsonAsync("/items", new { name });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ItemDto>())!;
    }

    [Test]
    public async Task Lending_an_item_updates_status_and_disables_button()
    {
        var item = await CreateItemViaApiAsync($"UI-Drill-{Guid.NewGuid():N}");
        var borrower = $"ui-alice-{Guid.NewGuid():N}";
        var ui = new LendItPage(Page, BaseUrl);

        await ui.GotoAsync();
        await Expect(ui.ItemStatus(item.Id)).ToHaveTextAsync("Available");

        await ui.SetBorrowerAsync(borrower);
        await ui.LendAsync(item.Id);

        await Expect(ui.ItemStatus(item.Id)).ToHaveTextAsync("Lent out");
        await Expect(ui.LendButton(item.Id)).ToBeDisabledAsync();
        await Expect(ui.Message).ToHaveTextAsync($"Lent to {borrower}");
    }

    [Test]
    public async Task Adding_an_item_shows_it_in_the_list_as_available()
    {
        var itemName = $"UI-Saw-{Guid.NewGuid():N}";
        var ui = new LendItPage(Page, BaseUrl);

        await ui.GotoAsync();
        await ui.AddItemAsync(itemName);

        await Expect(ui.Message).ToHaveTextAsync($"Added {itemName}");
        await Expect(ui.ItemByName(itemName)).ToContainTextAsync("Available");
    }
}
