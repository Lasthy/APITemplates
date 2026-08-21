using Kernel.Common;
using Kernel.Requests;
using System.Text.Json;

namespace Kernel.Configuration.Requests;

public record UpdateSettingsRequest(string SectionName, string Key, JsonElement Value) : IRequest<bool>;

public class UpdateSettingsRequestHandler : IRequestHandler<UpdateSettingsRequest, bool>
{
    private readonly DynamicSettings _dynamicSettings;

    public UpdateSettingsRequestHandler(DynamicSettings dynamicSettings)
    {
        _dynamicSettings = dynamicSettings;
    }

    public Task<Result<bool>> HandleAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var section = _dynamicSettings.GetSection(request.SectionName);
            if (section.IsReadonly)
            {
                return Task.FromResult(Result.Failure<bool>(new InvalidOperationException($"Settings section '{request.SectionName}' is read-only.")));
            }

            section.SetValue(request.Key, request.Value);

            return Task.FromResult(Result.Success(true));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(Result.Failure<bool>(new KeyNotFoundException($"Settings section '{request.SectionName}' not found.")));
        }
    }
}
