using System.Text.Json;

namespace Kernel.Json;

public class JsonSerializationException : JsonException
{
	public JsonSerializationException(string? message = "An error ocurred serializing an object into json.", Exception? innerException = null) : base(message, innerException) 
	{
		
	}
}

