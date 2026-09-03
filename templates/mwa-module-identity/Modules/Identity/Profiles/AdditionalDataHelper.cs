using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Cysharp.Serialization.Json;
using Kernel.Json;

namespace Identity.Profiles;

/// <summary>
/// Helper for reading and converting values from <see cref="IReadOnlyDictionary{TKey, TValue}"/>
/// additional data dictionaries (handling direct casts, case-insensitivity, JsonElement deserialization,
/// Guid/Ulid parsing, and primitive type conversion).
/// </summary>
internal static class AdditionalDataHelper
{
    public static bool TryGetValue<T>(
        IReadOnlyDictionary<string, object>? additionalData,
        string key,
        IServiceProvider? services,
        [MaybeNullWhen(false)] out T value)
    {
        value = default;
        if (additionalData == null || additionalData.Count == 0 || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        object? rawValue = null;
        if (additionalData.TryGetValue(key, out var exact))
        {
            rawValue = exact;
        }
        else
        {
            foreach (var kvp in additionalData)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    rawValue = kvp.Value;
                    break;
                }
            }
        }

        if (rawValue == null)
        {
            return false;
        }

        if (rawValue is T direct)
        {
            value = direct;
            return true;
        }

        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (rawValue is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Null || jsonElement.ValueKind == JsonValueKind.Undefined)
            {
                return false;
            }

            try
            {
                if (underlyingType == typeof(string))
                {
                    value = jsonElement.ValueKind == JsonValueKind.String
                        ? (T)(object)jsonElement.GetString()!
                        : (T)(object)jsonElement.GetRawText();
                    return true;
                }

                if (underlyingType == typeof(int) && jsonElement.TryGetInt32(out var i))
                {
                    value = (T)(object)i;
                    return true;
                }

                if (underlyingType == typeof(long) && jsonElement.TryGetInt64(out var l))
                {
                    value = (T)(object)l;
                    return true;
                }

                if (underlyingType == typeof(bool) &&
                    (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False))
                {
                    value = (T)(object)jsonElement.GetBoolean();
                    return true;
                }

                if (underlyingType == typeof(decimal) && jsonElement.TryGetDecimal(out var d))
                {
                    value = (T)(object)d;
                    return true;
                }

                if (underlyingType == typeof(double) && jsonElement.TryGetDouble(out var dbl))
                {
                    value = (T)(object)dbl;
                    return true;
                }

                if (underlyingType == typeof(Guid))
                {
                    if (jsonElement.TryGetGuid(out var g))
                    {
                        value = (T)(object)g;
                        return true;
                    }
                    var s = jsonElement.GetString();
                    if (s != null && Guid.TryParse(s, out var pg))
                    {
                        value = (T)(object)pg;
                        return true;
                    }
                }

                if (underlyingType == typeof(Ulid))
                {
                    var s = jsonElement.GetString();
                    if (s != null && Ulid.TryParse(s, out var u))
                    {
                        value = (T)(object)u;
                        return true;
                    }
                }

                if (underlyingType.IsEnum)
                {
                    var s = jsonElement.GetString();
                    if (s != null && Enum.TryParse(underlyingType, s, true, out var ev))
                    {
                        value = (T)ev;
                        return true;
                    }
                }

                // Complex DTO deserialization
                var serializer = services?.GetService(typeof(IJsonSerializer)) as IJsonSerializer;
                if (serializer != null)
                {
                    var deserialized = serializer.Deserialize<T>(jsonElement.GetRawText());
                    if (deserialized != null)
                    {
                        value = deserialized;
                        return true;
                    }
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new UlidJsonConverter());
                var res = System.Text.Json.JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), options);
                if (res != null)
                {
                    value = res;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        if (rawValue is string strVal)
        {
            if (underlyingType == typeof(Ulid) && Ulid.TryParse(strVal, out var ulid))
            {
                value = (T)(object)ulid;
                return true;
            }

            if (underlyingType == typeof(Guid) && Guid.TryParse(strVal, out var guid))
            {
                value = (T)(object)guid;
                return true;
            }

            if (underlyingType.IsEnum && Enum.TryParse(underlyingType, strVal, true, out var enumVal))
            {
                value = (T)enumVal;
                return true;
            }
        }

        try
        {
            value = (T)Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            // Fallback: serialize to JSON and deserialize to target type
            try
            {
                var serializer = services?.GetService(typeof(IJsonSerializer)) as IJsonSerializer;
                if (serializer != null)
                {
                    var json = serializer.Serialize(rawValue);
                    var deserialized = serializer.Deserialize<T>(json);
                    if (deserialized != null)
                    {
                        value = deserialized;
                        return true;
                    }
                }
            }
            catch
            {
                // Ignored
            }

            return false;
        }
    }
}
