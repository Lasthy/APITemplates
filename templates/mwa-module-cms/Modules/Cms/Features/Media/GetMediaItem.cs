using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Media;

/// <summary>
/// Query to get media metadata by ID.
/// </summary>
public record GetMediaItem(DefaultId Id) : IRequest<ContentMediaDto>;

[Logging(Order = 1)]
public class GetMediaItemHandler : IRequestHandler<GetMediaItem, ContentMediaDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetMediaItemHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ContentMediaDto>> HandleAsync(GetMediaItem request, CancellationToken cancellationToken = default)
    {
        var media = await _dbContext.Media
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (media == null)
        {
            return Result.Failure<ContentMediaDto>(new KeyNotFoundException($"Media '{request.Id}' not found."));
        }

        return Result.Success(media.ToDto());
    }
}
