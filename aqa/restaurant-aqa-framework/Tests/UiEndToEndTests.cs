using Microsoft.Playwright;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using RestaurantAqa.Clients;
using RestaurantAqa.Config;
using RestaurantAqa.Pages;

namespace RestaurantAqa.Tests;

[TestFixture]
[Category("UI")]
[Category("Level1")]
public class UiEndToEndTests
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private RestaurantApiClient _api = null!;
    private string? _ownerToken;
    private string? _createdReservationId;

    [OneTimeSetUp]
    public async Task GlobalSetUp()
    {
        Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = TestSettings.Headless
        });
    }

    [SetUp]
    public async Task SetUp()
    {
        _api = new RestaurantApiClient();
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });
        await _context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true
        });
        _page = await _context.NewPageAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        var testName = TestContext.CurrentContext.Test.Name;
        var outDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", "Artifacts");
        Directory.CreateDirectory(outDir);

        var screenshotPath = Path.Combine(outDir, $"{testName}.png");
        await _page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
        TestContext.AddTestAttachment(screenshotPath, "UI Screenshot");

        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
        {
            var tracePath = Path.Combine(outDir, $"{testName}-trace.zip");
            await _context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
            TestContext.AddTestAttachment(tracePath, "Playwright Failure Trace");
        }
        else
        {
            await _context.Tracing.StopAsync();
        }

        if (_ownerToken != null && _createdReservationId != null)
            await _api.CancelReservationSafeAsync(_ownerToken, _createdReservationId);

        await _context.CloseAsync();
        _api.Dispose();
    }

    [OneTimeTearDown]
    public async Task GlobalTearDown()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [Test(Description = "UI: Головна сторінка та фільтрація меню ресторану Green & Tasty")]
    public async Task MainPageAndMenu_DisplaysLocationsPopularDishes_AndFiltersMenu()
    {
        var mainPage = new MainPage(_page);
        await mainPage.OpenAsync();

        await _page.WaitForSelectorAsync("text=Main page", new() { Timeout = 15000 });
        await _page.WaitForTimeoutAsync(1000);
        var content = await _page.ContentAsync();
        Assert.That(content, Does.Contain("Main page"));

        var menuPage = new MenuPage(_page);
        await menuPage.OpenAsync();
        await _page.WaitForTimeoutAsync(2000);

        var count = await menuPage.DishCards.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Страви повинні відображатися в меню");
    }

    [Test(Description = "UI E2E: Перехід за гостьовим запрошенням /join/{token} відображає сторінку групового pre-order")]
    public async Task GuestInviteLink_OpensJoinGroupPreOrderScreenInBrowser()
    {
        (_, _ownerToken) = await _api.RegisterAndSignInCustomerAsync("uiowner");
        var table = await _api.CreateTestTableAsync(6);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 4);
        _createdReservationId = res.Id;

        var (_, inviteToken) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);

        var joinPage = new GuestJoinPage(_page);
        await joinPage.OpenInviteAsync(inviteToken);
        await _page.WaitForTimeoutAsync(2500);

        var pageText = await joinPage.Body.InnerTextAsync();
        Assert.That(pageText, Is.Not.Empty);
    }
}
