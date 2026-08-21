using Kernel.Events;
using Kernel.Json;

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
	private readonly Dictionary<string, DynamicSettingsSection> _settings = [];
	private readonly IJsonSerializer _jsonSerializer;
	private readonly string? _environment;
	private IEventBus? _eventBus;

	public DynamicSettings(IJsonSerializer jsonSerializer, string? environment = null)
	{
		_jsonSerializer = jsonSerializer;
		_environment = environment;
	}

	/// <summary>
	/// Returns all registered section names.
	/// </summary>
	public IEnumerable<string> GetSectionNames() => _settings.Keys;

	/// <summary>
	/// Gets a section by its <see cref="DynamicSettingsSection.SectionName"/>.
	/// </summary>
	/// <exception cref="KeyNotFoundException">When the section is not registered.</exception>
	public DynamicSettingsSection GetSection(string name) => _settings[name];

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
		_settings[instance.SectionName] = instance;
		WireChangeEvent(instance);
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

	// ── Private helpers ───────────────────────────────────────────────────

	private void WireChangeEvent(DynamicSettingsSection section)
	{
		section.OnSettingsChanged += (changedSection, key) =>
		{
			if (_eventBus is not null)
			{
				// Fire-and-forget publish. Handlers run synchronously in-process.
				_ = _eventBus.PublishAsync(new SettingsChangedEvent(changedSection.SectionName, key));
			}
		};
	}
}
