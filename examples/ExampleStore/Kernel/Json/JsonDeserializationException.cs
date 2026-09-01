using System.Text.Json;

namespace Kernel.Json;

public class JsonDeserializationException : JsonException
{
	public JsonDeserializationException(string? message = "An error ocurred deserializing a json into a object.", Exception? innerException = null) : base(message, innerException) 
	{
		
	}
}
