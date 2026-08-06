using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Configuration;

public static class WebApplicationExtensions
{
    public static void AddDynamicSettingsSection(this WebApplication app, string source, string name)
    {
        var dynamicSettings = app.Services.GetRequiredService<DynamicSettings>();
        dynamicSettings.CreateSection(source, name);
    }
}