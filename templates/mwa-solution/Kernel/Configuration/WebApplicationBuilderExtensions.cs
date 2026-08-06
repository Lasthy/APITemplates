using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Configuration;

public static class WebApplicationBuilderExtensions
{
	public static void AddDynamicSettings(this WebApplicationBuilder builder) 
	{
		builder.Services.AddSingleton<DynamicSettings>();
	}
}
