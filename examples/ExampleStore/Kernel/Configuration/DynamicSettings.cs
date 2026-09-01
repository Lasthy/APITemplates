using Kernel.Events;
using Kernel.Json;
using System.Diagnostics.CodeAnalysis;

namespace Kernel.Configuration;

/// <summary>
/// Central registry of all <see cref="DynamicSettingsSection"/> instances.
/// Sections are keyed by their <see cref="DynamicSettingsSection.SectionName"/>.
/// <para>
/// When an <see cref="IEventBus"/> is provided, every value change on any section
/// automatically publishes a <see cref="SettingsChangedEvent"/>.
/// </para>
/// </summary>
public class DynamicSettings
{
	private readonly Dictionary<string, DynamicSettingsSection> _settings = new(StringComparer.OrdinalIgnoreCase);
	private readonly IJsonSerializer _jsonSerializer;
	private string? _environment;
	private IEventBus? _eventBus;

	/// <summary>
	/// Raised whenever a new section is registered.
	/// </summary>
	public event Action<DynamicSettingsSection>? OnSectionAdded;

	/// <summary>
	/// Raised whenever any setting value changes in any registered section.
	/// </summary>
	public event Action<DynamicSettingsSection, string>? OnSettingChanged;

	/// <summary>
	/// Raised whenever the active environment is updated.
	/// </summary>
	public event Action<string?>? OnEnvironmentChanged;

	public DynamicSettings(IJsonSerializer jsonSerializer, string? environment = null)
	{
		_jsonSerializer = jsonSerializer;
		_environment = environment ?? 
			System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? 
			System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? 
			"Production";
	}

	/// <summary>
	/// Returns all registered section names.
	/// </summary>
	public IEnumerable<string> GetSectionNames() => _settings.Keys;

	/// <summary>
	/// Returns all registered sections.
	/// </summary>
	public IEnumerable<DynamicSettingsSection> GetSections() => _settings.Values;

	/// <summary>
	/// Gets a section by its <see cref="DynamicSettingsSection.SectionName"/>.
	/// </summary>
	/// <exception cref="KeyNotFoundException">When the section is not registered.</exception>
	public DynamicSettingsSection GetSection(string name) => _settings[name];

	/// <summary>
	/// Tries to get a section by its <see cref="DynamicSettingsSection.SectionName"/>.
	/// </summary>
	public bool TryGetSection(string name, [NotNullWhen(true)] out DynamicSettingsSection? section) =>
		_settings.TryGetValue(name, out section);

	/// <summary>
	/// Gets a strongly-typed section. Looks up by the instance's <see cref="DynamicSettingsSection.SectionName"/>.
	/// </summary>
	public T GetSection<T>() where T : DynamicSettingsSection
	{
		// Find the first section of the exact type
		foreach (var section in _settings.Values)
		{
			if (section is T typed)
				return typed;
		}

		throw new KeyNotFoundException(
			$"No DynamicSettingsSection of type '{typeof(T).Name}' is registered.");
	}

	/// <summary>
	/// Registers a section instance, keyed by its <see cref="DynamicSettingsSection.SectionName"/>.
	/// Wires up the <see cref="DynamicSettingsSection.OnSettingsChanged"/> event to publish
	/// <see cref="SettingsChangedEvent"/> via <see cref="IEventBus"/> (if available).
	/// </summary>
	public T AddSection<T>(T instance) where T : DynamicSettingsSection
	{
		if (!string.IsNullOrEmpty(_environment) &&
		    !string.Equals(instance.EnvironmentName, _environment, StringComparison.OrdinalIgnoreCase))
		{
			instance.SetEnvironment(_environment);
		}

		_settings[instance.SectionName] = instance;
		WireChangeEvent(instance);
		OnSectionAdded?.Invoke(instance);
		return instance;
	}

	/// <summary>
	/// Persists all sections that have pending changes back to their source files.
	/// </summary>
	public void CommitChanges()
	{
		foreach (var (_, section) in _settings)
		{
			section.Save();
		}
	}

	/// <summary>
	/// Sets the <see cref="IEventBus"/> used to publish <see cref="SettingsChangedEvent"/> instances.
	/// Called after the DI container is built (since IEventBus is resolved from DI).
	/// Re-wires all existing sections.
	/// </summary>
	public void SetEventBus(IEventBus eventBus)
	{
		_eventBus = eventBus;
	}

	/// <summary>
	/// The environment name passed during construction (e.g. <c>"Development"</c>).
	/// Used by section constructors that need to discover overlay files.
	/// </summary>
	public string? Environment => _environment;

	/// <summary>
	/// Updates the active environment for DynamicSettings and all registered sections.
	/// Causes all sections to reload their environment-specific overlays.
	/// </summary>
	public void SetEnvironment(string? environment)
	{
		if (string.Equals(_environment, environment, StringComparison.OrdinalIgnoreCase))
			return;

		_environment = environment;
		foreach (var section in _settings.Values)
		{
			section.SetEnvironment(environment);
		}

		OnEnvironmentChanged?.Invoke(environment);
	}

	// ── Private helpers ───────────────────────────────────────────────────

	private void WireChangeEvent(DynamicSettingsSection section)
	{
		section.OnSettingsChanged += (changedSection, key) =>
		{
			if (string.Equals(changedSection.SectionName, "Application", StringComparison.OrdinalIgnoreCase) &&
			    string.Equals(key, "Environment", StringComparison.OrdinalIgnoreCase))
			{
				if (changedSection.TryGetValue<string>("Environment", out var newEnv) && !string.IsNullOrWhiteSpace(newEnv))
				{
					SetEnvironment(newEnv);
				}
			}

			OnSettingChanged?.Invoke(changedSection, key);

			if (_eventBus is not null)
			{
				// Fire-and-forget publish. Handlers run synchronously in-process.
				_ = _eventBus.PublishAsync(new SettingsChangedEvent(changedSection.SectionName, key));
			}
		};
	}
}
