using Kernel.Common;
using Kernel.Requests;

namespace Kernel.Configuration.Features;

public record GetSettings(string SectionName) : IRequest<string>;

[Logging]
public class GetSettingsHandler : IRequestHandler<GetSettings, string>
{
    private readonly DynamicSettings _dynamicSettings;

    public GetSettingsHandler(DynamicSettings dynamicSettings)
    {
        _dynamicSettings = dynamicSettings;
    }

    public Task<Result<string>> HandleAsync(GetSettings request, CancellationToken cancellationToken = default)
    {
        try
        {
            var section = _dynamicSettings.GetSection(request.SectionName);
            return Task.FromResult(Result.Success<string>(section.GetAsJson()));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(Result.Failure<string>(new KeyNotFoundException($"Settings section '{request.SectionName}' not found.")));
        }
    }
}
