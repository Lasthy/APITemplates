using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kernel.Encryption;

/// <summary>
/// Entity Framework Core <see cref="ValueConverter{TModel,TProvider}"/> for transparent column-level encryption of string properties.
/// </summary>
public class EncryptedValueConverter : ValueConverter<string, string>
{
    /// <summary>
    /// Initializes a new instance of <see cref="EncryptedValueConverter"/> using the specified <see cref="IEncryptionService"/>.
    /// </summary>
    public EncryptedValueConverter(IEncryptionService encryptionService, ConverterMappingHints? mappingHints = null)
        : base(
            v => v == null ? null! : encryptionService.Encrypt(v),
            v => v == null ? null! : encryptionService.Decrypt(v),
            mappingHints)
    {
        ArgumentNullException.ThrowIfNull(encryptionService);
    }
}

/// <summary>
/// Entity Framework Core <see cref="ValueConverter{TModel,TProvider}"/> for transparent column-level encryption of binary (byte[]) properties.
/// </summary>
public class EncryptedBinaryValueConverter : ValueConverter<byte[], byte[]>
{
    /// <summary>
    /// Initializes a new instance of <see cref="EncryptedBinaryValueConverter"/> using the specified <see cref="IEncryptionService"/>.
    /// </summary>
    public EncryptedBinaryValueConverter(IEncryptionService encryptionService, ConverterMappingHints? mappingHints = null)
        : base(
            v => v == null ? null! : encryptionService.Encrypt(v),
            v => v == null ? null! : encryptionService.Decrypt(v),
            mappingHints)
    {
        ArgumentNullException.ThrowIfNull(encryptionService);
    }
}
