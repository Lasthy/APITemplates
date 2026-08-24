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
        var sectionName = request.SectionName;
        if (!_dynamicSettings.TryGetSection(sectionName, out var section))
        {
            if (string.Equals(sectionName, "Host", StringComparison.OrdinalIgnoreCase) &&
                _dynamicSettings.TryGetSection(string.Empty, out var hostSection))
            {
                section = hostSection;
            }
            else
            {
                return Task.FromResult(Result.Failure<string>(new KeyNotFoundException($"Settings section '{request.SectionName}' not found.")));
            }
        }

        return Task.FromResult(Result.Success<string>(section.GetAsJson()));
    }
}
