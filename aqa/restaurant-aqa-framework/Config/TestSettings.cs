namespace RestaurantAqa.Config;

public static class TestSettings
{
    static TestSettings()
    {
        LoadEnvFile(".env");
        LoadEnvFile(".env.example");
    }

    public static string FrontendUrl => Get("FRONTEND_URL", "https://d109zq6ui73t1v.cloudfront.net").TrimEnd('/');
    public static string ApiUrl => Get("API_URL", "https://c3ka44ycdc.execute-api.us-west-2.amazonaws.com/develop").TrimEnd('/');
    public static string DefaultLocationId => Get("DEFAULT_LOCATION_ID", "loc-rustaveli");
    public static string Dish1Id => Get("DEFAULT_DISH_1_ID", "dish-spring-salad");
    public static string Dish2Id => Get("DEFAULT_DISH_2_ID", "dish-avocado-bowl");
    public static string DefaultPassword => Get("TEST_USER_PASSWORD", "SecurePass1!");
    public static bool Headless => !string.Equals(Get("HEADLESS", "true"), "false", StringComparison.OrdinalIgnoreCase);

    private static string Get(string key, string fallback)
    {
        var val = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(val) ? fallback : val;
    }

    private static void LoadEnvFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(path)) return;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(parts[0].Trim())))
            {
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
    }
}
