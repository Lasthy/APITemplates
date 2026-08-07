using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static void AddKernel(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
    }
}
