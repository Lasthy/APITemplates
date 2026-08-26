// Copyright (c) 2019 Cysharp, Inc.
// Licensed under the MIT license. See ThirdParty/Licenses/Cysharp.Ulid/LICENSE.txt

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cysharp.EntityFrameworkCore;

public class UlidToBytesConverter : ValueConverter<Ulid, byte[]>
{
    private static readonly ConverterMappingHints DefaultHints = new ConverterMappingHints(size: 16);

    public UlidToBytesConverter() : this(null)
    {
    }
    
    public UlidToBytesConverter(ConverterMappingHints? mappingHints)
        : base(
                convertToProviderExpression: x => x.ToByteArray(),
                convertFromProviderExpression: x => new Ulid(x),
                mappingHints: DefaultHints.With(mappingHints))
    {
    }
}

public class UlidToStringConverter : ValueConverter<Ulid, string>
{
    private static readonly ConverterMappingHints DefaultHints = new ConverterMappingHints(size: 26);

    public UlidToStringConverter() : this(null)
    {
    }

    public UlidToStringConverter(ConverterMappingHints? mappingHints)
        : base(
            convertToProviderExpression: x => x.ToString(),
            convertFromProviderExpression: x => Ulid.Parse(x),
            mappingHints: DefaultHints.With(mappingHints))
    {
    }
}

public class UlidToNullableStringConverter : ValueConverter<Ulid?, string?>
{
    private static readonly ConverterMappingHints DefaultHints = new ConverterMappingHints(size: 26);

    public UlidToNullableStringConverter() : this(null)
    {
    }

    public UlidToNullableStringConverter(ConverterMappingHints? mappingHints)
        : base(
            convertToProviderExpression: x => x.HasValue ? x.Value.ToString() : null,
            convertFromProviderExpression: x => string.IsNullOrEmpty(x) ? (Ulid?)null : Ulid.Parse(x),
            mappingHints: DefaultHints.With(mappingHints))
    {
    }
}