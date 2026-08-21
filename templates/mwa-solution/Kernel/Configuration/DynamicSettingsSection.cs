using Kernel.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kernel.Configuration;

/// <summary>
/// Abstract base class for dynamic settings sections.
/// Subclasses declare a <see cref="SectionName"/> and expose strongly-typed properties
/// using <see cref="GetValue{T}"/> / <see cref="SetValue"/>.
/// <para>
/// Files follow the naming convention:
/// <c>settings.{SectionName}.json</c> (base) and
/// <c>settings.{SectionName}.{Environment}.json</c> (optional environment overlay).
/// Environment values overlay base values — base values are always persisted on write.
/// </para>
/// </summary>
public abstract class DynamicSettingsSection
{
	/// <summary>
	/// The section name, used as the key in <see cref="DynamicSettings"/> and in the file name convention.
	/// </summary>
	public abstract string SectionName { get; }

	/// <summary>
	/// When <c>true</c>, <see cref="SetValue"/> calls are silently ignored.
	/// </summary>
	public bool IsReadonly { get; set; }

	/// <summary>
	/// <c>true</c> when at least one value has been changed via <see cref="SetValue"/>
	/// since the last <see cref="Save"/> (or since construction).
	/// </summary>
	[JsonIgnore]
	public bool HasPendingChanges { get; private set; }

	/// <summary>
	/// The absolute or relative path to the base settings file.
	/// </summary>
	[JsonIgnore]
	public string Source { get; private set; }

	/// <summary>
	/// The absolute or relative path to the environment-specific overlay file, if any.
	/// </summary>
	[JsonIgnore]
	public string? EnvironmentSource { get; private set; }

	/// <summary>
	/// Raised whenever a setting value is changed via <see cref="SetValue"/>.
	/// Parameters: the section instance and the key that changed.
	/// </summary>
	public event Action<DynamicSettingsSection, string>? OnSettingsChanged;

	private Dictionary<string, JsonElement> _baseOptions = [];
	private Dictionary<string, JsonElement> _envOptions = [];
	private Dictionary<string, JsonElement> _merged = [];

	[JsonIgnore]
	protected readonly IJsonSerializer JsonSerializer;

	/// <summary>
	/// Constructs the section by loading the base file and an optional environment overlay.
	/// </summary>
	/// <param name="source">Path to the base settings file (<c>settings.{SectionName}.json</c>).</param>
	/// <param name="jsonSerializer">The centralized JSON serializer.</param>
	/// <param name="environment">
	/// Optional environment name (e.g. <c>"Development"</c>).
	/// When provided, an overlay file <c>settings.{SectionName}.{environment}.json</c>
	/// in the same directory is merged on top of the base values.
	/// </param>
	protected DynamicSettingsSection(string source, IJsonSerializer jsonSerializer, string? environment = null)
	{
		Source = source;
		JsonSerializer = jsonSerializer;

		_baseOptions = LoadFile(source);

		// Load environment overlay if it exists
		if (!string.IsNullOrEmpty(environment))
		{
			var dir = Path.GetDirectoryName(source);
			var envFileName = $"settings.{SectionName}.{environment}.json";
			var envFilePath = string.IsNullOrEmpty(dir) ? envFileName : Path.Combine(dir, envFileName);

			if (File.Exists(envFilePath))
			{
				EnvironmentSource = envFilePath;
				_envOptions = LoadFile(envFilePath);
			}
		}

		RebuildMerged();
	}

	/// <summary>
	/// Gets a strongly-typed value by key from the merged (base + env) settings.
	/// </summary>
	/// <exception cref="KeyNotFoundException">When the key does not exist.</exception>
	public T GetValue<T>(string key) => _merged[key].Deserialize<T>()!;

	/// <summary>
	/// Tries to get a strongly-typed value by key. Returns <c>false</c> if the key is missing.
	/// </summary>
	public bool TryGetValue<T>(string key, out T? value)
	{
		if (_merged.TryGetValue(key, out var element))
		{
			value = element.Deserialize<T>();
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Sets a value in the <b>base</b> settings (not the environment overlay).
	/// Marks the section as dirty, raises <see cref="OnSettingsChanged"/>, and auto-persists.
	/// </summary>
	public void SetValue(string key, object value)
	{
		if (IsReadonly) return;

		_baseOptions[key] = JsonSerializer.SerializeToElement(value);
		RebuildMerged();

		HasPendingChanges = true;

		// Auto-persist to the base file
		Save();

		OnSettingsChanged?.Invoke(this, key);
	}

	/// <summary>
	/// Persists any pending changes back to the <see cref="Source"/> file.
	/// </summary>
	public void Save()
	{
		if (!HasPendingChanges) return;

		File.WriteAllText(Source, JsonSerializer.Serialize(_baseOptions));
		HasPendingChanges = false;
	}

	/// <summary>
	/// Returns the merged settings as a JSON string.
	/// </summary>
	public string GetAsJson() => JsonSerializer.Serialize(_merged);

	// ── Private helpers ───────────────────────────────────────────────────

	private Dictionary<string, JsonElement> LoadFile(string path)
	{
		if (!File.Exists(path))
			return [];

		var content = File.ReadAllText(path);
		if (string.IsNullOrWhiteSpace(content))
			return [];

		return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
	}

	/// <summary>
	/// Merges base + environment options. Environment values win on conflict.
	/// </summary>
	private void RebuildMerged()
	{
		_merged = new Dictionary<string, JsonElement>(_baseOptions);

		foreach (var (key, value) in _envOptions)
		{
			_merged[key] = value;
		}
	}
}