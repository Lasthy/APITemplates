using Kernel.Json;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kernel.Configuration;

/// <summary>
/// Abstract base class for dynamic settings sections.
/// Subclasses declare a <see cref="SectionName"/> and expose strongly-typed properties
/// using <see cref="GetValue{T}"/> / <see cref="SetValue"/>.
/// <para>
/// Files follow the naming convention:
/// <c>appsettings.{SectionName}.json</c> (base) or <c>appsettings.json</c> for root host settings, and
/// <c>appsettings.{SectionName}.{Environment}.json</c> (or <c>appsettings.{Environment}.json</c>) for environment overlays.
/// Environment values and environment variables overlay base values — base values are always persisted on write.
/// </para>
/// </summary>
public abstract class DynamicSettingsSection
{
	private readonly string? _explicitSectionName;

	/// <summary>
	/// The section name, used as the key in <see cref="DynamicSettings"/> and in the file name convention.
	/// An empty string indicates root/host settings (<c>appsettings.json</c>).
	/// </summary>
	public virtual string SectionName => _explicitSectionName ?? string.Empty;

	/// <summary>
	/// When <c>true</c>, <see cref="SetValue"/> calls are silently ignored.
	/// </summary>
	public bool IsReadonly { get; set; }

	/// <summary>
	/// Controls whether <see cref="SetValue"/> automatically persists changes to the file on disk.
	/// Defaults to <c>true</c>. Set to <c>false</c> in in-memory test environments to avoid file contention.
	/// </summary>
	[JsonIgnore]
	public bool AutoSave { get; set; } = true;

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
	/// The environment name (e.g. <c>"Development"</c>).
	/// </summary>
	[JsonIgnore]
	public string? EnvironmentName { get; private set; }

	/// <summary>
	/// Raised whenever a setting value is changed via <see cref="SetValue"/>.
	/// Parameters: the section instance and the key that changed.
	/// </summary>
	public event Action<DynamicSettingsSection, string>? OnSettingsChanged;

	private Dictionary<string, JsonElement> _baseOptions = new(StringComparer.OrdinalIgnoreCase);
	private Dictionary<string, JsonElement> _envOptions = new(StringComparer.OrdinalIgnoreCase);
	private Dictionary<string, JsonElement> _envVarOptions = new(StringComparer.OrdinalIgnoreCase);
	private Dictionary<string, JsonElement> _merged = new(StringComparer.OrdinalIgnoreCase);

	[JsonIgnore]
	protected readonly IJsonSerializer JsonSerializer;

	/// <summary>
	/// Returns the merged key-value dictionary (base + environment overlay + environment variables).
	/// </summary>
	[JsonIgnore]
	public IReadOnlyDictionary<string, JsonElement> MergedValues => _merged;

	/// <summary>
	/// Constructs the section by loading the base file, optional environment overlay, and environment variables.
	/// </summary>
	/// <param name="source">Path to the base settings file.</param>
	/// <param name="jsonSerializer">The centralized JSON serializer.</param>
	/// <param name="environment">
	/// Optional environment name (e.g. <c>"Development"</c>).
	/// When provided, an overlay file in the same directory is merged on top of base values.
	/// </param>
	/// <param name="sectionName">Optional explicit section name for dynamic or root sections.</param>
	protected DynamicSettingsSection(
		string source,
		IJsonSerializer jsonSerializer,
		string? environment = null,
		string? sectionName = null)
	{
		Source = source;
		JsonSerializer = jsonSerializer;
		EnvironmentName = environment;
		_explicitSectionName = sectionName;

		Initialize();
	}

	/// <summary>
	/// Re-initializes and reloads base options, environment overlays, and environment variables.
	/// </summary>
	public void Initialize()
	{
		var effectiveSection = SectionName;
		Source = ResolveFilePath(Source, effectiveSection);
		_baseOptions = LoadFile(Source);
		_envOptions = [];
		_envVarOptions = [];

		// 1. Load environment JSON overlay if specified
		if (!string.IsNullOrEmpty(EnvironmentName))
		{
			var dir = Path.GetDirectoryName(Source);
			var envFileName = string.IsNullOrEmpty(effectiveSection)
				? $"appsettings.{EnvironmentName}.json"
				: $"appsettings.{effectiveSection}.{EnvironmentName}.json";

			var envFilePath = string.IsNullOrEmpty(dir) ? envFileName : Path.Combine(dir, envFileName);
			envFilePath = ResolveFilePath(envFilePath, effectiveSection);

			if (File.Exists(envFilePath))
			{
				EnvironmentSource = envFilePath;
				_envOptions = LoadFile(envFilePath);
			}
		}

		// 2. Load environment variables
		LoadEnvironmentVariables(effectiveSection);

		// 3. Merge: Base -> Environment JSON -> Environment Variables
		RebuildMerged();
	}

	/// <summary>
	/// Gets a strongly-typed value by key from the merged (base + env + env-vars) settings.
	/// </summary>
	/// <exception cref="KeyNotFoundException">When the key does not exist.</exception>
	public T GetValue<T>(string key)
	{
		if (_merged.TryGetValue(key, out var element))
		{
			return element.Deserialize<T>()!;
		}

		throw new KeyNotFoundException($"Key '{key}' was not found in settings section '{SectionName}'.");
	}

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
		if (AutoSave)
		{
			Save();
		}

		OnSettingsChanged?.Invoke(this, key);
	}

	/// <summary>
	/// Persists any pending changes back to the <see cref="Source"/> file.
	/// </summary>
	public void Save()
	{
		if (!HasPendingChanges) return;

		var targetPath = ResolveFilePath(Source, SectionName);
		File.WriteAllText(targetPath, JsonSerializer.Serialize(_baseOptions));
		HasPendingChanges = false;
	}

	/// <summary>
	/// Returns the merged settings as a JSON string.
	/// </summary>
	public string GetAsJson() => JsonSerializer.Serialize(_merged);

	/// <summary>
	/// Resolves a file path across the current working directory, AppContext.BaseDirectory, and module directories.
	/// </summary>
	public static string ResolveFilePath(string path, string? sectionName = null)
	{
		if (string.IsNullOrWhiteSpace(path))
			return path;

		if (Path.IsPathRooted(path) && File.Exists(path))
			return path;

		// 1. Check relative to current working directory
		var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), path);
		if (File.Exists(cwdPath))
			return cwdPath;

		// 2. Check relative to AppContext.BaseDirectory (build output / bin folder)
		var baseDirPath = Path.Combine(AppContext.BaseDirectory, path);
		if (File.Exists(baseDirPath))
			return baseDirPath;

		// 3. If a section name is available, check relative to module source folders
		if (!string.IsNullOrEmpty(sectionName))
		{
			var modulePath1 = Path.Combine(Directory.GetCurrentDirectory(), "Modules", sectionName, path);
			if (File.Exists(modulePath1))
				return modulePath1;

			var modulePath2 = Path.Combine(Directory.GetCurrentDirectory(), "..", "Modules", sectionName, path);
			if (File.Exists(modulePath2))
				return modulePath2;
		}

		return path;
	}

	// ── Private helpers ───────────────────────────────────────────────────

	private Dictionary<string, JsonElement> LoadFile(string path)
	{
		var resolvedPath = ResolveFilePath(path, SectionName);
		if (!File.Exists(resolvedPath))
			return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

		var content = File.ReadAllText(resolvedPath);
		if (string.IsNullOrWhiteSpace(content))
			return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

		var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
		return new Dictionary<string, JsonElement>(dict ?? [], StringComparer.OrdinalIgnoreCase);
	}

	private void LoadEnvironmentVariables(string effectiveSection)
	{
		var envVars = System.Environment.GetEnvironmentVariables();
		var sectionPrefix = string.IsNullOrEmpty(effectiveSection) ? string.Empty : $"{effectiveSection}__";
		var appsettingsPrefix = string.IsNullOrEmpty(effectiveSection) ? "APPSETTINGS__" : $"APPSETTINGS__{effectiveSection}__";
		var settingsPrefix = string.IsNullOrEmpty(effectiveSection) ? "SETTINGS__" : $"SETTINGS__{effectiveSection}__";

		foreach (DictionaryEntry entry in envVars)
		{
			if (entry.Key is not string key || entry.Value is not string strValue)
				continue;

			string? settingKey = null;

			if (key.StartsWith(appsettingsPrefix, StringComparison.OrdinalIgnoreCase))
			{
				settingKey = key[appsettingsPrefix.Length..];
			}
			else if (key.StartsWith(settingsPrefix, StringComparison.OrdinalIgnoreCase))
			{
				settingKey = key[settingsPrefix.Length..];
			}
			else if (!string.IsNullOrEmpty(sectionPrefix) && key.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase))
			{
				settingKey = key[sectionPrefix.Length..];
			}

			if (string.IsNullOrWhiteSpace(settingKey))
				continue;

			// Handle nested keys e.g. "Partition__Start" => "Partition:Start" or nested json
			try
			{
				using var doc = JsonDocument.Parse(strValue);
				_envVarOptions[settingKey] = doc.RootElement.Clone();
			}
			catch
			{
				_envVarOptions[settingKey] = JsonSerializer.SerializeToElement(strValue);
			}
		}
	}

	/// <summary>
	/// Merges base + environment file options + environment variables. Precedence: EnvVars > EnvFile > Base.
	/// </summary>
	private void RebuildMerged()
	{
		_merged = new Dictionary<string, JsonElement>(_baseOptions, StringComparer.OrdinalIgnoreCase);

		foreach (var (key, value) in _envOptions)
		{
			_merged[key] = value;
		}

		foreach (var (key, value) in _envVarOptions)
		{
			_merged[key] = value;
		}
	}
}