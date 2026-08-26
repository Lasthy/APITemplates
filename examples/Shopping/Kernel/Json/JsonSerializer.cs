using System.Text.Json;
using Cysharp.Serialization.Json;

namespace Kernel.Json;

/// <summary>
///	The default implementation to Kernel.Json.IJsonSerializer.
/// </summary>
/// <remarks>
///	Uses System.Text.Json.JsonSerializer underneath.
///	</remarks>
public class JsonSerializer : IJsonSerializer
{
	/// <summary>
	///	The JsonSerializerOptions that will be used in serialization and deserialization.
	/// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

	/// <inheritdoc/>
    public string Serialize(object obj)
    {
		try 
		{
			var json = System.Text.Json.JsonSerializer.Serialize<object>(obj, SerializerOptions);
			
			return json;
		}
		catch(Exception e)
		{
			throw new JsonSerializationException(null, e);	
		}
    }

	/// <inheritdoc/>
    public T Deserialize<T>(string json)
    {
		try 
		{
			var @object = System.Text.Json.JsonSerializer.Deserialize<T>(json, SerializerOptions);
			
			if(@object is null) {
				throw new NullReferenceException("The json string deserializes into null.");
			}
			
			return @object;
		}
		catch(Exception e)
		{
			throw new JsonDeserializationException(null, e);	
		}
    }

	/// <inheritdoc/>
    public object Deserialize(string json, Type type)
    {
		try 
		{
			var @object = System.Text.Json.JsonSerializer.Deserialize(json, type, SerializerOptions);
			
			if(@object is null) {
				throw new NullReferenceException("The json string deserializes into null.");
			}
			
			return @object;
		}
		catch(Exception e)
		{
			throw new JsonDeserializationException(null, e);	
		}
    }

	/// <summary>
	///	Creates the JsonSerializerOptions that will be used in serialization and deserialization.
	/// </summary>
	/// <returns>A JsonSerializerOptions.</returns>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions();
		
		options.Converters.Add(new UlidJsonConverter());
        options.WriteIndented = true;

        return options;
    }

    public JsonElement SerializeToElement(object obj)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.SerializeToElement(obj, SerializerOptions);

            return json;
        }
        catch (Exception e)
        {
            throw new JsonSerializationException(null, e);
        }
    }
}