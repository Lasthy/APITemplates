using System.Text.Json;
using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Revisions;

/// <summary>
/// Query to compare two revisions of a content item and produce a field-by-field diff.
/// </summary>
public record CompareRevisions(
    DefaultId ContentId,
    int FromRevision,
    int ToRevision) : IRequest<RevisionComparisonDto>;

[Logging(Order = 1)]
public class CompareRevisionsHandler : IRequestHandler<CompareRevisions, RevisionComparisonDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public CompareRevisionsHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<RevisionComparisonDto>> HandleAsync(CompareRevisions request, CancellationToken cancellationToken = default)
    {
        var revisions = await _dbContext.Revisions
            .AsNoTracking()
            .Where(r => r.ContentItemId == request.ContentId &&
                        (r.RevisionNumber == request.FromRevision || r.RevisionNumber == request.ToRevision))
            .ToListAsync(cancellationToken);

        var fromRev = revisions.FirstOrDefault(r => r.RevisionNumber == request.FromRevision);
        if (fromRev == null)
        {
            return Result.Failure<RevisionComparisonDto>(
                new KeyNotFoundException($"Revision {request.FromRevision} not found for content item '{request.ContentId}'."));
        }

        var toRev = revisions.FirstOrDefault(r => r.RevisionNumber == request.ToRevision);
        if (toRev == null)
        {
            return Result.Failure<RevisionComparisonDto>(
                new KeyNotFoundException($"Revision {request.ToRevision} not found for content item '{request.ContentId}'."));
        }

        var changes = new List<FieldDiff>();

        if (!string.Equals(fromRev.Title, toRev.Title, StringComparison.Ordinal))
        {
            changes.Add(new FieldDiff("Title", fromRev.Title, toRev.Title));
        }

        if (!string.Equals(fromRev.Body, toRev.Body, StringComparison.Ordinal))
        {
            changes.Add(new FieldDiff("Body", fromRev.Body, toRev.Body));
        }

        if (!string.Equals(fromRev.Excerpt, toRev.Excerpt, StringComparison.Ordinal))
        {
            changes.Add(new FieldDiff("Excerpt", fromRev.Excerpt, toRev.Excerpt));
        }

        CompareJsonFields("Fields", fromRev.FieldsJson, toRev.FieldsJson, changes);
        CompareJsonFields("Metadata", fromRev.MetadataJson, toRev.MetadataJson, changes);

        return Result.Success(new RevisionComparisonDto(request.FromRevision, request.ToRevision, changes));
    }

    private static void CompareJsonFields(string prefix, string? jsonA, string? jsonB, List<FieldDiff> changes)
    {
        if (string.Equals(jsonA, jsonB, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var dictA = !string.IsNullOrWhiteSpace(jsonA)
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonA)
                : null;
            var dictB = !string.IsNullOrWhiteSpace(jsonB)
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonB)
                : null;

            if (dictA != null || dictB != null)
            {
                var keysA = dictA?.Keys ?? Enumerable.Empty<string>();
                var keysB = dictB?.Keys ?? Enumerable.Empty<string>();
                var allKeys = keysA.Union(keysB, StringComparer.OrdinalIgnoreCase).OrderBy(k => k);

                foreach (var key in allKeys)
                {
                    var valA = dictA != null && dictA.TryGetValue(key, out var elemA) ? elemA.ToString() : null;
                    var valB = dictB != null && dictB.TryGetValue(key, out var elemB) ? elemB.ToString() : null;

                    if (!string.Equals(valA, valB, StringComparison.Ordinal))
                    {
                        changes.Add(new FieldDiff($"{prefix}.{key}", valA, valB));
                    }
                }
                return;
            }
        }
        catch
        {
            // Fallback to raw string diff
        }

        changes.Add(new FieldDiff($"{prefix}Json", jsonA, jsonB));
    }
}
