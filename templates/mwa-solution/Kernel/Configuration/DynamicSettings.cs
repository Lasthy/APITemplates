using Kernel.Json;
using Kernel.Modules;
using System.Xml.Linq;

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

	public T GetSection<T>() where T : DynamicSettingsSection
	{
		return (T)_settings[nameof(T)];
	}

	public DynamicSettingsSection CreateSection(string source, string name)
	{
		var section = new DynamicSettingsSection(source, _jsonSerializer);
        _settings[name] = section;

		return section;
	}

	public T AddSection<T>(T instance) where T : DynamicSettingsSection
	{
        _settings[typeof(T).Name] = instance;

        return instance;
    }
	
	public void CommitChanges() {
		foreach(var (_, setting) in _settings) {
			if(setting.SettingsChanged) {
				File.WriteAllText(setting.Source, setting.GetAsJson());
			}
		}
	}
}
