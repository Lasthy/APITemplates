namespace Kernel.Configuration;

public class DynamicSettingsProvider
{
	private Dictionary<string, DynamicSettings> _settings = [];
	
	public DynamicSettings GetSettings<T>() where T : IModule {
		return _settings[T.Name]; // TODO: create module interface with static abstract
	}
	
	public void CommitChanges() {
		foreach(var (_, setting) in _settings) {
			if(setting.SettingsChanged) {
				// TODO: save in the database
			}
		}
	}
}
