using System.Collections.Concurrent;
using System.Reflection;

namespace Cms.ContentTypes;

/// <summary>
/// Descriptor containing metadata and field definitions for a registered content type.
/// </summary>
public record ContentTypeDescriptor(
    string Name,
    string Description,
    string Icon,
    bool HasBody,
    string BodyFormat,
    IReadOnlyList<ContentField> Fields,
    bool SupportsTags,
    bool SupportsMedia,
    int? MaxRevisions,
    Type ImplementationType);

/// <summary>
/// Contract for the content type registry.
/// </summary>
public interface IContentTypeRegistry
{
    void Register<T>() where T : class, IContentType;
    void Register(Type contentType);
    void RegisterFromAssembly(Assembly assembly);
    ContentTypeDescriptor? Get(string name);
    bool TryGet(string name, out ContentTypeDescriptor? descriptor);
    IReadOnlyCollection<ContentTypeDescriptor> GetAll();
}

/// <summary>
/// Thread-safe registry of code-defined content types.
/// </summary>
public class ContentTypeRegistry : IContentTypeRegistry
{
    private readonly ConcurrentDictionary<string, ContentTypeDescriptor> _types = new(StringComparer.OrdinalIgnoreCase);

    public void Register<T>() where T : class, IContentType
    {
        Register(typeof(T));
    }

    public void Register(Type contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        if (!typeof(IContentType).IsAssignableFrom(contentType) || contentType.IsAbstract || contentType.IsInterface)
        {
            throw new ArgumentException(
                $"Type '{contentType.FullName}' must implement {nameof(IContentType)} and be a concrete class.",
                nameof(contentType));
        }

        var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        var nameProp = contentType.GetProperty(nameof(IContentType.Name), flags);
        var descProp = contentType.GetProperty(nameof(IContentType.Description), flags);
        var iconProp = contentType.GetProperty(nameof(IContentType.Icon), flags);
        var hasBodyProp = contentType.GetProperty(nameof(IContentType.HasBody), flags);
        var bodyFormatProp = contentType.GetProperty(nameof(IContentType.BodyFormat), flags);
        var fieldsProp = contentType.GetProperty(nameof(IContentType.Fields), flags);
        var supportsTagsProp = contentType.GetProperty(nameof(IContentType.SupportsTags), flags);
        var supportsMediaProp = contentType.GetProperty(nameof(IContentType.SupportsMedia), flags);
        var maxRevisionsProp = contentType.GetProperty(nameof(IContentType.MaxRevisions), flags);

        if (nameProp == null)
        {
            throw new InvalidOperationException($"Type '{contentType.FullName}' does not declare static Name property.");
        }

        var name = (string)nameProp.GetValue(null)!;
        var description = (string)(descProp?.GetValue(null) ?? string.Empty);
        var icon = (string)(iconProp?.GetValue(null) ?? "page");
        var hasBody = (bool)(hasBodyProp?.GetValue(null) ?? true);
        var bodyFormat = (string)(bodyFormatProp?.GetValue(null) ?? "markdown");
        var fields = (IReadOnlyList<ContentField>)(fieldsProp?.GetValue(null) ?? Array.Empty<ContentField>());
        var supportsTags = (bool)(supportsTagsProp?.GetValue(null) ?? true);
        var supportsMedia = (bool)(supportsMediaProp?.GetValue(null) ?? true);
        var maxRevisions = (int?)(maxRevisionsProp?.GetValue(null));

        var descriptor = new ContentTypeDescriptor(
            name,
            description,
            icon,
            hasBody,
            bodyFormat,
            fields,
            supportsTags,
            supportsMedia,
            maxRevisions,
            contentType);

        _types[name] = descriptor;
    }

    public void RegisterFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var types = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IContentType).IsAssignableFrom(t));

        foreach (var type in types)
        {
            Register(type);
        }
    }

    public ContentTypeDescriptor? Get(string name)
    {
        TryGet(name, out var descriptor);
        return descriptor;
    }

    public bool TryGet(string name, out ContentTypeDescriptor? descriptor)
    {
        return _types.TryGetValue(name, out descriptor);
    }

    public IReadOnlyCollection<ContentTypeDescriptor> GetAll()
    {
        return _types.Values.ToList().AsReadOnly();
    }
}
