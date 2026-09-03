namespace Kernel.Configuration;

/// <summary>
/// Marks a dynamic setting property as sensitive.
/// Sensitive settings are redacted (masked with asterisks) when retrieved via the settings API
/// and are protected from accidental exposure or unauthorized mutation.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class SensitiveAttribute : Attribute
{
    /// <summary>
    /// When <c>false</c> (default), mutation of this key via HTTP / management API endpoints is prohibited.
    /// </summary>
    public bool AllowApiMutation { get; set; }

    public SensitiveAttribute(bool allowApiMutation = false)
    {
        AllowApiMutation = allowApiMutation;
    }
}
