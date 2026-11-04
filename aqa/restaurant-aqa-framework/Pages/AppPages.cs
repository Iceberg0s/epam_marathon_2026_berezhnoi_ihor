using Microsoft.Playwright;
using RestaurantAqa.Config;

namespace RestaurantAqa.Pages;

public class MainPage(IPage page)
{
    public async Task OpenAsync() => await page.GotoAsync(TestSettings.FrontendUrl);
    public ILocator PopularDishesHeading => page.GetByText("Most Popular Dishes", new() { Exact = false });
    public ILocator ViewMenuButton => page.GetByRole(AriaRole.Button, new() { Name = "View Menu" });
}

public class MenuPage(IPage page)
{
    public async Task OpenAsync() => await page.GotoAsync($"{TestSettings.FrontendUrl}/menu");
    public ILocator AppetizersFilter => page.GetByText("Appetizers", new() { Exact = false }).First;
    public ILocator DessertsFilter => page.GetByText("Desserts", new() { Exact = false }).First;
    public ILocator DishCards => page.Locator("text=$");
}

public class GuestJoinPage(IPage page)
{
    public async Task OpenInviteAsync(string token) => await page.GotoAsync($"{TestSettings.FrontendUrl}/join/{token}");
    public ILocator Body => page.Locator("body");
}
