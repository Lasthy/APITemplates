namespace Kernel.Json;

/// <summary>
///	Interface for centralizing json serialization and deserialization with the same configuration.
/// </summary>
public interface IJsonSerializer
{
	/// <summary>
	///	Serialize an object into a json string.
	/// </summary>
	/// <param name="obj">The object to be serialized</param>
	/// <returns>A string that represents the object in json format.</returns>
	/// <exception cref="JsonSerializationException"></exception>
	string Serialize(object obj);
	
	/// <summary>
	///	Deserializes a string containing a json into a object of type T.
	/// </summary>
	/// <param name="json">The json string to be deserialized.</param>
	/// <param name="T">The type which the json will deserialize into</param>
	/// <returns>A object of type T.</returns>
	/// <exception cref="JsonDeserializationException"></exception>
    T Deserialize<T>(string json);
	
	/// <summary>
	///	Deserializes a string containing a json into a object of type "type".
	/// </summary>
	/// <param name="json">The json string to be deserialized</param>
	/// <param name="type">The type which the json will deserialize into</param>
	/// <returns>A object of type "type""</returns>
	/// <exception cref="JsonDeserializationException"></exception>
    object Deserialize(string json, Type type);
}
