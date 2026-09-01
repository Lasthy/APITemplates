using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Shopping.Tests;

public class ShoppingApiTestFactory : WebApplicationFactory<Program>
{
    public ShoppingApiTestFactory()
    {
        CleanupDbFiles();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", $"10.99.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}");

        var retries = 10;
        HttpResponseMessage response = null!;
        while (retries-- > 0)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", $"10.99.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}");

            response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new
            {
                Email = email,
                Password = password
            });

            if (response.IsSuccessStatusCode) break;
            await Task.Delay(300);
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("accessToken").GetString()!;
        return token;
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateApiKeyClient(string apiKey, string headerName = "X-API-Key")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(headerName, apiKey);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        CleanupDbFiles();
    }

    private static void CleanupDbFiles()
    {
        try
        {
            var locations = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Host")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Host"))
            };

            foreach (var loc in locations)
            {
                if (Directory.Exists(loc))
                {
                    foreach (var file in Directory.GetFiles(loc, "*.db"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
        }
        catch { }
    }
}
