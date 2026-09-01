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
        try
        {
            if (File.Exists("kernel.db")) File.Delete("kernel.db");
            if (File.Exists("identity.db")) File.Delete("identity.db");
            if (File.Exists("catalog.db")) File.Delete("catalog.db");
            if (File.Exists("orders.db")) File.Delete("orders.db");
        }
        catch { }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        if (!client.DefaultRequestHeaders.Contains("X-Forwarded-For"))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", $"10.99.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}");
        }

        var retries = 10;
        HttpResponseMessage response = null!;
        while (retries-- > 0)
        {
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists("kernel.db")) File.Delete("kernel.db");
        }
        catch { }
    }
}
