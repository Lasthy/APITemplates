using Kernel.Common;
using Kernel.Requests;
using System.Text.Json;

namespace Kernel.Configuration.Features;

public record UpdateSettings(string SectionName, string Key, JsonElement Value) : IRequest<bool>;

[Logging]
public class UpdateSettingsHandler : IRequestHandler<UpdateSettings, bool>
{
    private readonly DynamicSettings _dynamicSettings;

    public UpdateSettingsHandler(DynamicSettings dynamicSettings)
    {
        _dynamicSettings = dynamicSettings;
    }

    public Task<Result<bool>> HandleAsync(UpdateSettings request, CancellationToken cancellationToken = default)
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
                return Task.FromResult(Result.Failure<bool>(new KeyNotFoundException($"Settings section '{request.SectionName}' not found.")));
            }
        }

        if (section.IsReadonly)
        {
            return Task.FromResult(Result.Failure<bool>(new InvalidOperationException($"Settings section '{request.SectionName}' is read-only.")));
        }

        section.SetValue(request.Key, request.Value);
        return Task.FromResult(Result.Success(true));
    }
}
