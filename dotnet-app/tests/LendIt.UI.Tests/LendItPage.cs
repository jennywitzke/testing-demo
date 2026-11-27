using Microsoft.Playwright;

namespace LendIt.UI.Tests;

public class LendItPage(IPage page, string baseUrl)
{
    public Task GotoAsync() => page.GotoAsync(baseUrl);

    public async Task AddItemAsync(string name)
    {
        await page.GetByTestId("new-item-name").FillAsync(name);
        await page.GetByTestId("add-item-button").ClickAsync();
    }

    public Task SetBorrowerAsync(string borrower) =>
        page.GetByLabel("Borrower").FillAsync(borrower);

    public Task LendAsync(int id) =>
        page.GetByTestId($"lend-button-{id}").ClickAsync();

    public ILocator ItemStatus(int id) =>
        page.GetByTestId($"item-{id}-status");

    public ILocator LendButton(int id) =>
        page.GetByTestId($"lend-button-{id}");

    public ILocator ItemByName(string name) =>
        page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = name });

    public ILocator Message =>
        page.GetByTestId("message");
}
