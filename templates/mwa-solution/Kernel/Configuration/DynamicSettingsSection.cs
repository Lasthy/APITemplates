using Kernel.Json;
using System.Text.Json;

namespace Kernel.Configuration;

public class DynamicSettingsSection
{
	public bool SettingsChanged { get; private set; }
	public string Source { get; private set; }

	private Dictionary<string, JsonElement> _options { get; set; } = [];
	private readonly IJsonSerializer _jsonSerializer;

	public DynamicSettingsSection(string source, IJsonSerializer jsonSerializer)
	{
		Source = source;
		_jsonSerializer = jsonSerializer;
		_options = _jsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(Source));
	}
	
	public T GetValue<T>(string key) => _options[key].Deserialize<T>()!;
	
	public void SetValue(string key, object value) {
		_options[key] = _jsonSerializer.SerializeToElement(value);
		
		SettingsChanged = true;
	}
	public string GetAsJson()
	{
		return _jsonSerializer.Serialize(_options);
	}
}
  