using Kernel.Common;
using Kernel.Requests;

namespace Kernel.Configuration.Requests;

public record GetSettingsRequest(string SectionName) : IRequest<string>;

public class GetSettingsRequestHandler : IRequestHandler<GetSettingsRequest, string>
{
    private readonly DynamicSettings _dynamicSettings;

    public GetSettingsRequestHandler(DynamicSettings dynamicSettings)
    {
        _dynamicSettings = dynamicSettings;
    }

    public Task<Result<string>> HandleAsync(GetSettingsRequest request, CancellationToken cancellationToken = default)
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
