using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Json;

public static class WebApplicationBuilderExtensions
{
    public static void AddJsonSerializer(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IJsonSerializer, JsonSerializer>();
    }
}