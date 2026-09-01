using System.Reflection;
using Kernel.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Encryption.Extensions;

/// <summary>
/// Extension methods for registering encryption services and configuring transparent property encryption in EF Core.
/// </summary>
public static class EncryptionExtensions
{
    /// <summary>
    /// Registers the <see cref="AesEncryptionService"/> implementation of <see cref="IEncryptionService"/> as a singleton.
    /// </summary>
    public static IServiceCollection AddEncryption(this IServiceCollection services)
    {
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        return services;
    }

    /// <summary>
    /// Scans the EF Core model for entity properties decorated with <see cref="EncryptedAttribute"/>
    /// and configures them with value converters for transparent encryption/decryption.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="encryptionService">The encryption service used for value conversion.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder UseEncryption(this ModelBuilder modelBuilder, IEncryptionService encryptionService)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(encryptionService);

        var stringConverter = new EncryptedValueConverter(encryptionService);
        var binaryConverter = new EncryptedBinaryValueConverter(encryptionService);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var memberInfo = (MemberInfo?)property.PropertyInfo ?? property.FieldInfo;
                if (memberInfo == null) continue;

                if (Attribute.IsDefined(memberInfo, typeof(EncryptedAttribute), inherit: true))
                {
                    if (property.ClrType == typeof(string))
                    {
                        property.SetValueConverter(stringConverter);
                    }
                    else if (property.ClrType == typeof(byte[]))
                    {
                        property.SetValueConverter(binaryConverter);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Property '{property.Name}' on entity '{entityType.ClrType.Name}' is decorated with [{nameof(EncryptedAttribute)}] " +
                            $"but has unsupported CLR type '{property.ClrType.Name}'. Only 'string' and 'byte[]' properties can be encrypted.");
                    }
                }
            }
        }

        return modelBuilder;
    }

    /// <summary>
    /// Scans the EF Core model for entity properties decorated with <see cref="EncryptedAttribute"/>
    /// and configures them with value converters resolved from the <see cref="DbContext"/>'s service provider.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="context">The database context instance.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder UseEncryption(this ModelBuilder modelBuilder, DbContext context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        var serviceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        var encryptionService = serviceProvider.GetService<IEncryptionService>();

        if (encryptionService != null)
        {
            modelBuilder.UseEncryption(encryptionService);
        }

        return modelBuilder;
    }
}
