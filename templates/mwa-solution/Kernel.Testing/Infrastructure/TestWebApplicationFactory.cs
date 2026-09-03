using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Kernel.Caching;
using Kernel.Configuration;
using Kernel.Email;
using Kernel.Encryption;
using Kernel.Events;
using Kernel.Notifications;
using Kernel.Persistence;
using Kernel.RateLimiting;
using Kernel.Storage;
using Kernel.Testing.Fakes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kernel.Testing.Infrastructure;

/// <summary>
/// Pre-configured <see cref="WebApplicationFactory{TProgram}"/> for full integration testing of the Host and its modules.
/// Uses an isolated in-memory database and provides test fakes for EventBus, EmailSender, Cache, and Encryption.
/// </summary>
/// <typeparam name="TProgram">The entry point class of the host application (e.g. <c>Program</c>).</typeparam>
public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private readonly Lazy<string> _databaseName = new(() => $"IntegrationTestDb_{Guid.NewGuid():N}");

    /// <summary>
    /// The unique in-memory database name used by this factory instance.
    /// </summary>
    public string DatabaseName => _databaseName.Value;

    /// <summary>
    /// The test fake event bus.
    /// </summary>
    public FakeEventBus EventBus { get; } = new();

    /// <summary>
    /// The test fake email sender.
    /// </summary>
    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>
    /// The test fake cache service.
    /// </summary>
    public FakeCacheService CacheService { get; } = new();

    /// <summary>
    /// The test fake encryption service.
    /// </summary>
    public FakeEncryptionService EncryptionService { get; } = new();

    /// <summary>
    /// The test fake file storage service.
    /// </summary>
    public FakeFileStorage FileStorage { get; } = new();

    /// <summary>
    /// The test fake notification service.
    /// </summary>
    public FakeNotificationService NotificationService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace EventBus, EmailSender, CacheService, EncryptionService, FileStorage, NotificationService with test fakes
            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(EventBus);
            services.AddSingleton(EventBus);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
            services.AddSingleton(EmailSender);

            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService>(CacheService);
            services.AddSingleton(CacheService);

            services.RemoveAll<IEncryptionService>();
            services.AddSingleton<IEncryptionService>(EncryptionService);
            services.AddSingleton(EncryptionService);

            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(FileStorage);
            services.AddSingleton(FileStorage);

            services.RemoveAll<IFileRegistry>();
            services.AddSingleton<IFileRegistry>(FileStorage.Registry);

            services.RemoveAll<INotificationService>();
            services.AddSingleton<INotificationService>(NotificationService);
            services.AddSingleton(NotificationService);

            // Configure in-memory database in DataSettings
            var dynamicSettings = services
                .Where(d => d.ServiceType == typeof(DynamicSettings))
                .Select(d => d.ImplementationInstance as DynamicSettings)
                .FirstOrDefault();

            if (dynamicSettings != null)
            {
                var dataSettings = dynamicSettings.GetSection<DataSettings>();
                if (dataSettings != null)
                {
                    dataSettings.AutoSave = false;
                    dataSettings.SetValue("Provider", "InMemory");
                    dataSettings.SetValue("ConnectionString", DatabaseName);
                }

                var rateLimitingSettings = dynamicSettings.GetSection<RateLimitingSettings>();
                if (rateLimitingSettings != null)
                {
                    rateLimitingSettings.AutoSave = false;
                    rateLimitingSettings.SetValue("Enabled", false);
                }
            }

            // Register test authentication scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });
        });
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> configured with a Bearer authorization header.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> configured with test authentication headers for the specified user and roles.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(DefaultId userId, params string[] roles)
    {
        return CreateAuthenticatedClient(userId, null, roles);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> configured with test authentication headers for the specified user, claims, and roles.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(DefaultId userId, IReadOnlyDictionary<string, string>? claims, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        }
        if (claims is { Count: > 0 })
        {
            client.DefaultRequestHeaders.Add("X-Test-Claims", string.Join(";", claims.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        return client;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> configured with an API key header.
    /// </summary>
    public HttpClient CreateApiKeyClient(string apiKey, string headerName = "X-API-Key")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(headerName, apiKey);
        return client;
    }

    /// <summary>
    /// Executes an asynchronous delegate inside a fresh DI scope.
    /// </summary>
    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Executes an asynchronous function returning a result inside a fresh DI scope.
    /// </summary>
    public async Task<TResult> ExecuteScopeAsync<TResult>(Func<IServiceProvider, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Executes an asynchronous delegate on a resolved DbContext inside a fresh DI scope.
    /// </summary>
    public async Task ExecuteDbContextAsync<TDbContext>(Func<TDbContext, Task> action)
        where TDbContext : DbContext
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(db);
    }

    /// <summary>
    /// Executes an asynchronous function on a resolved DbContext inside a fresh DI scope.
    /// </summary>
    public async Task<TResult> ExecuteDbContextAsync<TDbContext, TResult>(Func<TDbContext, Task<TResult>> action)
        where TDbContext : DbContext
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await action(db);
    }
}

/// <summary>
/// Internal test authentication handler that authenticates requests using X-Test-UserId and X-Test-Roles headers,
/// or delegates to ApiKey and JwtBearer schemes when those headers are present.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue("X-Test-UserId", out var userIdValues))
        {
            var userId = userIdValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId),
                    new("sub", userId)
                };

                if (Request.Headers.TryGetValue("X-Test-Roles", out var rolesValues))
                {
                    var rolesStr = rolesValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rolesStr))
                    {
                        var roles = rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var role in roles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role));
                        }
                    }
                }

                if (Request.Headers.TryGetValue("X-Test-Claims", out var claimsValues))
                {
                    foreach (var claimHeader in claimsValues)
                    {
                        if (!string.IsNullOrWhiteSpace(claimHeader))
                        {
                            var pairs = claimHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            foreach (var pair in pairs)
                            {
                                var parts = pair.Split('=', 2);
                                if (parts.Length == 2)
                                {
                                    claims.Add(new Claim(parts[0].Trim(), parts[1].Trim()));
                                }
                            }
                        }
                    }
                }

                var identity = new ClaimsIdentity(claims, "TestScheme");
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, "TestScheme");

                return AuthenticateResult.Success(ticket);
            }
        }

        // Delegate to ApiKey scheme if present
        if (Request.Headers.ContainsKey("X-API-Key") || Request.Headers.ContainsKey("ApiKey"))
        {
            var apiKeyResult = await Context.AuthenticateAsync("ApiKey");
            if (apiKeyResult.Succeeded)
            {
                return apiKeyResult;
            }
        }

        // Delegate to Bearer scheme if present
        if (Request.Headers.ContainsKey("Authorization"))
        {
            var bearerResult = await Context.AuthenticateAsync("Bearer");
            if (bearerResult.Succeeded)
            {
                return bearerResult;
            }
        }

        return AuthenticateResult.NoResult();
    }
}
