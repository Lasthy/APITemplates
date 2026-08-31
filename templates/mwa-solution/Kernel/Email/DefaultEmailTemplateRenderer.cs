namespace Kernel.Email;

/// <summary>
/// Default implementation of <see cref="IEmailTemplateRenderer"/> that performs token replacement
/// (supporting <c>{{Key}}</c> and <c>{Key}</c> placeholders) on raw template content or template files.
/// </summary>
public class DefaultEmailTemplateRenderer : IEmailTemplateRenderer
{
    /// <inheritdoc />
    public async Task<string> RenderAsync(string templateName, Dictionary<string, string> variables, CancellationToken ct = default)
    {
        var content = await LoadTemplateContentAsync(templateName, ct);

        if (string.IsNullOrEmpty(content) || variables.Count == 0)
        {
            return content;
        }

        foreach (var (key, value) in variables)
        {
            content = content.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
            content = content.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return content;
    }

    private static async Task<string> LoadTemplateContentAsync(string templateName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return string.Empty;
        }

        // 1. Direct file path check
        if (File.Exists(templateName))
        {
            return await File.ReadAllTextAsync(templateName, ct);
        }

        // 2. Base directory / Templates directory check
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, templateName),
            Path.Combine(baseDir, "Templates", templateName),
            Path.Combine(baseDir, "Templates", $"{templateName}.html"),
            Path.Combine(baseDir, "Templates", $"{templateName}.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateName),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", $"{templateName}.html"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", $"{templateName}.txt")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return await File.ReadAllTextAsync(candidate, ct);
            }
        }

        // 3. Fallback: treat templateName as raw template content directly
        return templateName;
    }
}
