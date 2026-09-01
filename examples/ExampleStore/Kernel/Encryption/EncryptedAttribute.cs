namespace Kernel.Encryption;

/// <summary>
/// Specifies that an entity property should be transparently encrypted when stored in the database
/// and decrypted when read from the database using <see cref="IEncryptionService"/>.
/// Supported on <see cref="string"/> and <c>byte[]</c> properties.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class EncryptedAttribute : Attribute
{
}
