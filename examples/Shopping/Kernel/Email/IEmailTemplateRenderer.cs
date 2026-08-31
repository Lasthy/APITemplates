namespace Kernel.Email;

/// <summary>
/// Abstraction for rendering email body templates with dynamic variable replacements.
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders an email template by name or template content with the provided variables.
    /// </summary>
    /// <param name="templateName">The template name, file path, or raw template content.</param>
    /// <param name="variables">Key-value pairs to substitute into the template.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rendered template string.</returns>
    Task<string> RenderAsync(string templateName, Dictionary<string, string> variables, CancellationToken ct = default);
}
