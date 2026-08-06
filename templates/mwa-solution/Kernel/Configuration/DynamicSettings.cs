using Kernel.Json;
using Kernel.Modules;

namespace Kernel.Configuration;

public class DynamicSettings
{
	private Dictionary<string, DynamicSettingsSection> _settings = [];
	private readonly IJsonSerializer _jsonSerializer;

    public DynamicSettings(IJsonSerializer jsonSerializer)
    {
        _jsonSerializer = jsonSerializer;
    }

    public DynamicSettingsSection GetSection(string name) {
		return _settings[name];
	}

	public void CreateSection(string source, string name)
	{
		_settings[name] = new DynamicSettingsSection(source, _jsonSerializer);
	}
	
	public void CommitChanges() {
		foreach(var (_, setting) in _settings) {
			if(setting.SettingsChanged) {
				File.WriteAllText(setting.Source, setting.GetAsJson());
			}
		}
	}
}
