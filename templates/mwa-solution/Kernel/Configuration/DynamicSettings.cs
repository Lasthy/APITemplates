namespace Kernel.Configuration;

public class DynamicSettings
{
	internal bool SettingsChanged = false;
	private Dictionary<string, object> _options = [];
	
	public T GetValue<T>(string key) => (T)_options[key];
	
	public void SetValue(string key, object value) {
		_options[key] = value;
		
		SettingsChanged = true;
	}
}
  