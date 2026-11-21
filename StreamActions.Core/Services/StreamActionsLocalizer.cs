/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Globalization;
using System.Text.Json;

namespace StreamActions.Core.Services;

/// <summary>
/// Structured JSON localization engine backed by disk catalogs, workspace database overrides, and <see cref="HybridCache"/>.
/// </summary>
public sealed partial class StreamActionsLocalizer : IStreamActionsLocalizer
{
    /// <summary>
    /// The logical table name used in <see cref="IWorkspaceKeyValueStore"/> for localization overrides.
    /// </summary>
    internal const string _i18nTableName = "i18n";

    /// <summary>
    /// The section name used in <see cref="IWorkspaceKeyValueStore"/> for workspace command trigger overrides.
    /// </summary>
    internal const string _commandsOverrideSection = "commands";

    /// <summary>
    /// The canonical English fallback culture code for command triggers and default localization.
    /// </summary>
    private const string _canonicalFallbackCulture = "en-US";

    /// <summary>
    /// The <see cref="HybridCache"/> instance used for hot-path workspace localization caching.
    /// </summary>
    private readonly HybridCache _cache;

    /// <summary>
    /// The logger instance for catalog loading and override diagnostics.
    /// </summary>
    private readonly ILogger<StreamActionsLocalizer> _logger;

    /// <summary>
    /// The optional workspace store used to resolve <see cref="Workspace.BotCulture"/>.
    /// </summary>
    private readonly IWorkspaceStore? _workspaceStore;

    /// <summary>
    /// The optional key-value store used for workspace-level string and command trigger overrides.
    /// </summary>
    private readonly IWorkspaceKeyValueStore? _keyValueStore;

    /// <summary>
    /// The normalized full path of the root locales directory.
    /// </summary>
    private readonly string _localesDirectoryFullPath;

    /// <summary>
    /// The configured default fallback culture code.
    /// </summary>
    private readonly string _defaultCulture;

    /// <summary>
    /// Lock used to serialize catalog reloads and version increments.
    /// </summary>
    private readonly Lock _reloadLock = new();

    /// <summary>
    /// The monotonically increasing catalog snapshot version used in cache keys.
    /// </summary>
    private long _catalogVersion;

    /// <summary>
    /// The immutable snapshot of loaded per-culture JSON localization catalogs.
    /// </summary>
    private volatile CatalogSnapshot _snapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamActionsLocalizer"/> class.
    /// </summary>
    /// <param name="cache">The <see cref="HybridCache"/> instance used for workspace caching.</param>
    /// <param name="options">The localization configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceStore">The optional workspace store for resolving <see cref="Workspace.BotCulture"/>.</param>
    /// <param name="keyValueStore">The optional workspace key-value store for per-workspace overrides.</param>
    public StreamActionsLocalizer(
        HybridCache cache,
        IOptions<LocalizationOptions> options,
        ILogger<StreamActionsLocalizer> logger,
        IWorkspaceStore? workspaceStore = null,
        IWorkspaceKeyValueStore? keyValueStore = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        LocalizationOptions resolvedOptions = options.Value;
        string configuredDirectory = string.IsNullOrWhiteSpace(resolvedOptions.LocalesDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "locales")
            : resolvedOptions.LocalesDirectory;

        this._cache = cache;
        this._logger = logger;
        this._workspaceStore = workspaceStore;
        this._keyValueStore = keyValueStore;
        this._localesDirectoryFullPath = Path.GetFullPath(configuredDirectory);
        this._defaultCulture = string.IsNullOrWhiteSpace(resolvedOptions.DefaultCulture)
            ? _canonicalFallbackCulture
            : resolvedOptions.DefaultCulture.Trim();

        this._snapshot = this.LoadCatalogSnapshot(1L);
        this._catalogVersion = 1L;
    }

    /// <inheritdoc/>
    public LocalizedString this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);

            if (string.IsNullOrWhiteSpace(name))
            {
                return new(name, name, resourceNotFound: true);
            }

            string? resolved = this.ResolveUiString(name, CultureInfo.CurrentUICulture);
            return resolved is not null
                ? new(name, resolved, resourceNotFound: false)
                : new(name, name, resourceNotFound: true);
        }
    }

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(arguments);

            LocalizedString localized = this[name];
            string formatted = arguments.Length > 0
                ? string.Format(CultureInfo.CurrentCulture, localized.Value, arguments)
                : localized.Value;

            return new(name, formatted, localized.ResourceNotFound);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        CatalogSnapshot snapshot = this._snapshot;
        List<string> cultureChain = includeParentCultures
            ? this.BuildCultureChain(CultureInfo.CurrentUICulture.Name, includeCanonicalFallback: false)
            : [CultureInfo.CurrentUICulture.Name];

        Dictionary<string, LocalizedString> collected = new(StringComparer.OrdinalIgnoreCase);

        foreach (string cultureName in cultureChain)
        {
            if (string.IsNullOrWhiteSpace(cultureName) ||
                !snapshot.Cultures.TryGetValue(cultureName, out CultureCatalog? cultureCatalog))
            {
                continue;
            }

            foreach (string moduleName in cultureCatalog.OrderedModuleNames)
            {
                ModuleCatalog moduleCatalog = cultureCatalog.Modules[moduleName];
                foreach (KeyValuePair<string, string> entry in moduleCatalog.Strings)
                {
                    string qualifiedKey = $"{moduleName}:{entry.Key}";
                    _ = collected.TryAdd(qualifiedKey, new(qualifiedKey, entry.Value, resourceNotFound: false));
                }
            }
        }

        return collected.Values;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetStringAsync(
        Guid workspaceId,
        string module,
        string key,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateModuleAndKey(module, key, cultureOverride);

        string effectiveCulture = await this.ResolveEffectiveCultureAsync(workspaceId, cultureOverride, cancellationToken).ConfigureAwait(false);
        CatalogSnapshot snapshot = this._snapshot;
        string cacheKey = $"i18n:str:{snapshot.Version}:{workspaceId:N}:{effectiveCulture}:{module}:{key}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        CachedLocalizedValue cached = await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                string? value = await this.ResolveWorkspaceStringUncachedAsync(
                    snapshot,
                    workspaceId,
                    module,
                    key,
                    effectiveCulture,
                    ct).ConfigureAwait(false);

                return new CachedLocalizedValue(value);
            },
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return cached.Value;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetResponseTemplateAsync(
        Guid workspaceId,
        string module,
        string key,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateModuleAndKey(module, key, cultureOverride);

        if (key.StartsWith("responses.", StringComparison.OrdinalIgnoreCase))
        {
            return await this.GetStringAsync(workspaceId, module, key, cultureOverride, cancellationToken).ConfigureAwait(false);
        }

        string effectiveCulture = await this.ResolveEffectiveCultureAsync(workspaceId, cultureOverride, cancellationToken).ConfigureAwait(false);
        CatalogSnapshot snapshot = this._snapshot;
        string cacheKey = $"i18n:resp:{snapshot.Version}:{workspaceId:N}:{effectiveCulture}:{module}:{key}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        CachedLocalizedValue cached = await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                string prefixedKey = $"responses.{key}";
                string? value = null;

                if (this._keyValueStore is not null)
                {
                    value = await this._keyValueStore.GetStringAsync(
                        workspaceId,
                        _i18nTableName,
                        module,
                        prefixedKey,
                        defaultValue: null,
                        ct).ConfigureAwait(false);

                    value ??= await this._keyValueStore.GetStringAsync(
                        workspaceId,
                        _i18nTableName,
                        module,
                        key,
                        defaultValue: null,
                        ct).ConfigureAwait(false);
                }

                value ??= this.ResolveCatalogString(snapshot, module, prefixedKey, effectiveCulture);
                return new CachedLocalizedValue(value);
            },
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return cached.Value;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<LocalizedCommandDefinition>> GetCommandDefinitionsAsync(
        Guid workspaceId,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateOptionalCulture(cultureOverride, nameof(cultureOverride));

        CachedCommandCatalog cached = await this.GetCachedCommandCatalogAsync(workspaceId, cultureOverride, cancellationToken).ConfigureAwait(false);
        return cached.Definitions;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> ResolveCommandIdAsync(
        Guid workspaceId,
        string trigger,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateOptionalCulture(cultureOverride, nameof(cultureOverride));

        if (string.IsNullOrWhiteSpace(trigger))
        {
            return null;
        }

        string normalizedTrigger = trigger.Trim();
        CachedCommandCatalog cached = await this.GetCachedCommandCatalogAsync(workspaceId, cultureOverride, cancellationToken).ConfigureAwait(false);

        foreach (CommandTriggerEntry entry in cached.TriggerEntries)
        {
            if (string.Equals(entry.Trigger, normalizedTrigger, StringComparison.OrdinalIgnoreCase))
            {
                return entry.CommandId;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask SetWorkspaceOverrideAsync(
        Guid workspaceId,
        string module,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        this.ValidateModuleAndKey(module, key, cultureOverride: null);
        ArgumentNullException.ThrowIfNull(value);
        IWorkspaceKeyValueStore keyValueStore = this.RequireKeyValueStore();

        await keyValueStore.SetAsync(workspaceId, _i18nTableName, module, key, value, cancellationToken).ConfigureAwait(false);
        await this._cache.RemoveByTagAsync(GetWorkspaceCacheTag(workspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveWorkspaceOverrideAsync(
        Guid workspaceId,
        string module,
        string key,
        CancellationToken cancellationToken = default)
    {
        this.ValidateModuleAndKey(module, key, cultureOverride: null);
        IWorkspaceKeyValueStore keyValueStore = this.RequireKeyValueStore();

        bool removed = await keyValueStore.DeleteAsync(workspaceId, _i18nTableName, module, key, cancellationToken).ConfigureAwait(false);
        await this._cache.RemoveByTagAsync(GetWorkspaceCacheTag(workspaceId), cancellationToken).ConfigureAwait(false);
        return removed;
    }

    /// <inheritdoc/>
    public async ValueTask SetWorkspaceCommandTriggersAsync(
        Guid workspaceId,
        string commandId,
        IReadOnlyCollection<string> triggers,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(triggers);
        IWorkspaceKeyValueStore keyValueStore = this.RequireKeyValueStore();

        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string trigger in triggers)
        {
            if (!string.IsNullOrWhiteSpace(trigger))
            {
                string trimmed = trigger.Trim();
                if (seen.Add(trimmed))
                {
                    normalized.Add(trimmed);
                }
            }
        }

        string serialized = JsonSerializer.Serialize(normalized);
        await keyValueStore.SetAsync(
            workspaceId,
            _i18nTableName,
            _commandsOverrideSection,
            commandId.Trim(),
            serialized,
            cancellationToken).ConfigureAwait(false);
        await this._cache.RemoveByTagAsync(GetWorkspaceCacheTag(workspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask InvalidateWorkspaceCacheAsync(Guid workspaceId, CancellationToken cancellationToken = default) => await this._cache.RemoveByTagAsync(GetWorkspaceCacheTag(workspaceId), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<string>> GetAvailableCulturesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(this._snapshot.AvailableCultures);
    }

    /// <inheritdoc/>
    public void ReloadCatalogs()
    {
        lock (this._reloadLock)
        {
            long nextVersion = Interlocked.Increment(ref this._catalogVersion);
            this._snapshot = this.LoadCatalogSnapshot(nextVersion);
        }
    }

    /// <summary>
    /// Computes the <see cref="HybridCache"/> invalidation tag for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>The formatted workspace cache tag.</returns>
    private static string GetWorkspaceCacheTag(Guid workspaceId) => $"workspace:{workspaceId}:i18n";

    /// <summary>
    /// Checks whether a candidate full path stays strictly within a root directory path.
    /// </summary>
    /// <param name="rootFullPath">The normalized full path of the root directory.</param>
    /// <param name="candidateFullPath">The normalized full path of the candidate path.</param>
    /// <returns><see langword="true"/> if the candidate path resides inside the root directory; otherwise, <see langword="false"/>.</returns>
    private static bool IsPathWithinDirectory(string rootFullPath, string candidateFullPath)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        string normalizedRoot = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        return candidateFullPath.StartsWith(normalizedRoot, comparison);
    }

    /// <summary>
    /// Determines whether a single path segment (culture or module name) is free of path traversal sequences.
    /// </summary>
    /// <param name="segment">The segment to validate.</param>
    /// <returns><see langword="true"/> if safe; otherwise, <see langword="false"/>.</returns>
    private static bool IsSafePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) ||
            segment is "." ||
            segment.Contains("..", StringComparison.Ordinal) ||
            segment.Contains('/', StringComparison.Ordinal) ||
            segment.Contains('\\', StringComparison.Ordinal) ||
            segment.Contains(':', StringComparison.Ordinal) ||
            segment.Contains('\0', StringComparison.Ordinal) ||
            Path.IsPathRooted(segment))
        {
            return false;
        }

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        return segment.IndexOfAny(invalidFileNameChars) < 0;
    }

    /// <summary>
    /// Validates a path segment and throws <see cref="ArgumentException"/> if it contains traversal or invalid characters.
    /// </summary>
    /// <param name="segment">The segment value to validate.</param>
    /// <param name="paramName">The parameter name for exception reporting.</param>
    private static void ValidatePathSegment(string segment, string paramName)
    {
        if (!IsSafePathSegment(segment))
        {
            throw new ArgumentException($"Invalid localization identifier '{segment}'.", paramName);
        }
    }

    /// <summary>
    /// Recursively flattens a JSON object into dotted key-value string entries, skipping the top-level <c>commands</c> block.
    /// </summary>
    /// <param name="element">The JSON element to flatten.</param>
    /// <param name="prefix">The current dotted key prefix.</param>
    /// <param name="target">The destination dictionary for flattened strings.</param>
    /// <param name="isRoot">Whether <paramref name="element"/> is the catalog root object.</param>
    private static void FlattenJsonObject(
        JsonElement element,
        string prefix,
        Dictionary<string, string> target,
        bool isRoot)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (isRoot && string.Equals(property.Name, _commandsOverrideSection, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string nextKey = string.IsNullOrEmpty(prefix)
                ? property.Name
                : $"{prefix}.{property.Name}";

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                FlattenJsonObject(property.Value, nextKey, target, isRoot: false);
            }
            else if (property.Value.ValueKind == JsonValueKind.String)
            {
                target[nextKey] = property.Value.GetString() ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Parses the <c>commands</c> section of a JSON localization catalog.
    /// </summary>
    /// <param name="root">The root JSON element of the catalog.</param>
    /// <param name="moduleName">The catalog module name.</param>
    /// <returns>A dictionary of parsed command definitions keyed by command identifier.</returns>
    private static Dictionary<string, RawCommandDefinition> ParseCommands(JsonElement root, string moduleName)
    {
        Dictionary<string, RawCommandDefinition> commands = new(StringComparer.OrdinalIgnoreCase);

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(_commandsOverrideSection, out JsonElement commandsElement) ||
            commandsElement.ValueKind != JsonValueKind.Object)
        {
            return commands;
        }

        foreach (JsonProperty commandProperty in commandsElement.EnumerateObject())
        {
            if (commandProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string commandId = commandProperty.Name;
            List<string> triggers = [];
            HashSet<string> seenTriggers = new(StringComparer.OrdinalIgnoreCase);

            if (commandProperty.Value.TryGetProperty("triggers", out JsonElement triggersElement) &&
                triggersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement triggerItem in triggersElement.EnumerateArray())
                {
                    if (triggerItem.ValueKind == JsonValueKind.String)
                    {
                        string? rawTrigger = triggerItem.GetString();
                        if (!string.IsNullOrWhiteSpace(rawTrigger))
                        {
                            string trimmedTrigger = rawTrigger.Trim();
                            if (seenTriggers.Add(trimmedTrigger))
                            {
                                triggers.Add(trimmedTrigger);
                            }
                        }
                    }
                }
            }

            string? description = null;
            if (commandProperty.Value.TryGetProperty("description", out JsonElement descriptionElement) &&
                descriptionElement.ValueKind == JsonValueKind.String)
            {
                description = descriptionElement.GetString();
            }

            commands[commandId] = new(commandId, triggers, description, moduleName);
        }

        return commands;
    }

    /// <summary>
    /// Appends unique non-whitespace triggers to a command's trigger list and priority resolution table.
    /// </summary>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="sourceTriggers">The triggers to add.</param>
    /// <param name="targetTriggers">The destination deduplicated trigger list for the command.</param>
    /// <param name="seenForCommand">The per-command case-insensitive deduplication set.</param>
    /// <param name="triggerEntries">The ordered priority resolution list.</param>
    /// <param name="seenGlobalTriggers">The global trigger deduplication set ensuring higher-priority layers win.</param>
    private static void AppendTriggers(
        string commandId,
        IEnumerable<string> sourceTriggers,
        List<string> targetTriggers,
        HashSet<string> seenForCommand,
        List<CommandTriggerEntry> triggerEntries,
        HashSet<string> seenGlobalTriggers)
    {
        foreach (string trigger in sourceTriggers)
        {
            if (string.IsNullOrWhiteSpace(trigger))
            {
                continue;
            }

            string trimmed = trigger.Trim();
            if (seenForCommand.Add(trimmed))
            {
                targetTriggers.Add(trimmed);
            }

            if (seenGlobalTriggers.Add(trimmed))
            {
                triggerEntries.Add(new(trimmed, commandId));
            }
        }
    }

    /// <summary>
    /// Loads and parses all JSON localization catalogs under the configured root locales directory.
    /// </summary>
    /// <param name="version">The snapshot version number to assign.</param>
    /// <returns>An immutable <see cref="CatalogSnapshot"/> containing all loaded cultures and modules.</returns>
    private CatalogSnapshot LoadCatalogSnapshot(long version)
    {
        Dictionary<string, CultureCatalog> cultures = new(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(this._localesDirectoryFullPath))
        {
            LogLocalesDirectoryMissing(this._logger, this._localesDirectoryFullPath);
            return new(version, cultures, []);
        }

        foreach (string cultureDir in Directory.EnumerateDirectories(this._localesDirectoryFullPath))
        {
            string fullCultureDir = Path.GetFullPath(cultureDir);
            string cultureName = Path.GetFileName(fullCultureDir);

            if (!IsPathWithinDirectory(this._localesDirectoryFullPath, fullCultureDir) ||
                !IsSafePathSegment(cultureName))
            {
                LogUnsafeCatalogPathSkipped(this._logger, fullCultureDir);
                continue;
            }

            Dictionary<string, ModuleCatalog> modules = new(StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in Directory.EnumerateFiles(fullCultureDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                string fullFilePath = Path.GetFullPath(filePath);
                string moduleName = Path.GetFileNameWithoutExtension(fullFilePath);

                if (!IsPathWithinDirectory(fullCultureDir, fullFilePath) ||
                    !IsSafePathSegment(moduleName))
                {
                    LogUnsafeCatalogPathSkipped(this._logger, fullFilePath);
                    continue;
                }

                try
                {
                    using FileStream stream = File.OpenRead(fullFilePath);
                    using JsonDocument document = JsonDocument.Parse(stream);

                    Dictionary<string, string> strings = new(StringComparer.OrdinalIgnoreCase);
                    FlattenJsonObject(document.RootElement, string.Empty, strings, isRoot: true);
                    Dictionary<string, RawCommandDefinition> commands = ParseCommands(document.RootElement, moduleName);

                    modules[moduleName] = new(strings, commands);
                    LogCatalogLoaded(this._logger, cultureName, moduleName, strings.Count, commands.Count);
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    LogCatalogLoadFailed(this._logger, fullFilePath, ex);
                }
            }

            if (modules.Count > 0)
            {
                List<string> orderedModules = [.. modules.Keys.OrderBy(static m => m, StringComparer.OrdinalIgnoreCase)];
                int coreIndex = orderedModules.FindIndex(static m => string.Equals(m, "core", StringComparison.OrdinalIgnoreCase));
                if (coreIndex > 0)
                {
                    string coreName = orderedModules[coreIndex];
                    orderedModules.RemoveAt(coreIndex);
                    orderedModules.Insert(0, coreName);
                }

                cultures[cultureName] = new(modules, orderedModules);
            }
        }

        List<string> availableCultures = [.. cultures.Keys.OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)];
        return new(version, cultures, availableCultures);
    }

    /// <summary>
    /// Validates module, key, and optional culture override parameters against path traversal and blank inputs.
    /// </summary>
    /// <param name="module">The catalog module name.</param>
    /// <param name="key">The localization key.</param>
    /// <param name="cultureOverride">The optional culture override.</param>
    private void ValidateModuleAndKey(string module, string key, string? cultureOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ValidatePathSegment(module, nameof(module));
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        this.ValidateOptionalCulture(cultureOverride, nameof(cultureOverride));

        string candidatePath = Path.GetFullPath(
            Path.Combine(
                this._localesDirectoryFullPath,
                string.IsNullOrWhiteSpace(cultureOverride) ? this._defaultCulture : cultureOverride.Trim(),
                $"{module}.json"));

        if (!IsPathWithinDirectory(this._localesDirectoryFullPath, candidatePath))
        {
            throw new ArgumentException("Localization catalog path escapes the configured locales directory.", nameof(module));
        }
    }

    /// <summary>
    /// Validates an optional culture override parameter against path traversal.
    /// </summary>
    /// <param name="cultureOverride">The optional culture override.</param>
    /// <param name="paramName">The parameter name for exception reporting.</param>
    private void ValidateOptionalCulture(string? cultureOverride, string paramName)
    {
        if (string.IsNullOrWhiteSpace(cultureOverride))
        {
            return;
        }

        ValidatePathSegment(cultureOverride.Trim(), paramName);
        string candidateDir = Path.GetFullPath(Path.Combine(this._localesDirectoryFullPath, cultureOverride.Trim()));
        if (!IsPathWithinDirectory(this._localesDirectoryFullPath, candidateDir))
        {
            throw new ArgumentException($"Culture '{cultureOverride}' escapes the configured locales directory.", paramName);
        }
    }

    /// <summary>
    /// Ensures an <see cref="IWorkspaceKeyValueStore"/> is configured for write operations.
    /// </summary>
    /// <returns>The configured <see cref="IWorkspaceKeyValueStore"/> instance.</returns>
    private IWorkspaceKeyValueStore RequireKeyValueStore() => this._keyValueStore ?? throw new InvalidOperationException("No IWorkspaceKeyValueStore is registered for workspace localization overrides.");

    /// <summary>
    /// Resolves the effective culture for a workspace, caching <see cref="Workspace.BotCulture"/> in <see cref="HybridCache"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cultureOverride">The optional explicit culture override.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The effective BCP-47 culture code.</returns>
    private async ValueTask<string> ResolveEffectiveCultureAsync(
        Guid workspaceId,
        string? cultureOverride,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cultureOverride))
        {
            return cultureOverride.Trim();
        }

        if (this._workspaceStore is null)
        {
            return this._defaultCulture;
        }

        string cacheKey = $"i18n:ws-culture:{workspaceId:N}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                Workspace? workspace = await this._workspaceStore.GetByIdAsync(workspaceId, ct).ConfigureAwait(false);
                string? botCulture = workspace?.BotCulture;

                return string.IsNullOrWhiteSpace(botCulture) || !IsSafePathSegment(botCulture.Trim()) ? this._defaultCulture : botCulture.Trim();
            },
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a workspace string across the 4-layer fallback chain without using the cache.
    /// </summary>
    /// <param name="snapshot">The active catalog snapshot.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="module">The catalog module name.</param>
    /// <param name="key">The dotted localization key.</param>
    /// <param name="effectiveCulture">The resolved workspace culture.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The localized string if found; otherwise, <see langword="null"/>.</returns>
    private async ValueTask<string?> ResolveWorkspaceStringUncachedAsync(
        CatalogSnapshot snapshot,
        Guid workspaceId,
        string module,
        string key,
        string effectiveCulture,
        CancellationToken cancellationToken)
    {
        if (this._keyValueStore is not null)
        {
            string? overrideValue = await this._keyValueStore.GetStringAsync(
                workspaceId,
                _i18nTableName,
                module,
                key,
                defaultValue: null,
                cancellationToken).ConfigureAwait(false);

            if (overrideValue is not null)
            {
                return overrideValue;
            }
        }

        return this.ResolveCatalogString(snapshot, module, key, effectiveCulture);
    }

    /// <summary>
    /// Resolves a localized string from loaded JSON catalogs across the culture fallback chain.
    /// </summary>
    /// <param name="snapshot">The active catalog snapshot.</param>
    /// <param name="module">The catalog module name.</param>
    /// <param name="key">The dotted localization key.</param>
    /// <param name="effectiveCulture">The starting culture code.</param>
    /// <returns>The localized string if found; otherwise, <see langword="null"/>.</returns>
    private string? ResolveCatalogString(
        CatalogSnapshot snapshot,
        string module,
        string key,
        string effectiveCulture)
    {
        List<string> cultureChain = this.BuildCultureChain(effectiveCulture, includeCanonicalFallback: false);

        foreach (string cultureName in cultureChain)
        {
            if (snapshot.Cultures.TryGetValue(cultureName, out CultureCatalog? cultureCatalog) &&
                cultureCatalog.Modules.TryGetValue(module, out ModuleCatalog? moduleCatalog) &&
                moduleCatalog.Strings.TryGetValue(key, out string? value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a synchronous <see cref="IStringLocalizer"/> key using <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    /// <param name="name">The key or <c>module:key</c> string.</param>
    /// <param name="uiCulture">The current UI culture.</param>
    /// <returns>The resolved string if found; otherwise, <see langword="null"/>.</returns>
    private string? ResolveUiString(string name, CultureInfo uiCulture)
    {
        CatalogSnapshot snapshot = this._snapshot;
        List<string> cultureChain = this.BuildCultureChain(uiCulture.Name, includeCanonicalFallback: false);

        int colonIndex = name.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex > 0 && colonIndex < name.Length - 1)
        {
            string module = name[..colonIndex];
            string key = name[(colonIndex + 1)..];

            if (IsSafePathSegment(module))
            {
                foreach (string cultureName in cultureChain)
                {
                    if (snapshot.Cultures.TryGetValue(cultureName, out CultureCatalog? cultureCatalog) &&
                        cultureCatalog.Modules.TryGetValue(module, out ModuleCatalog? moduleCatalog) &&
                        moduleCatalog.Strings.TryGetValue(key, out string? value))
                    {
                        return value;
                    }
                }
            }
        }

        foreach (string cultureName in cultureChain)
        {
            if (!snapshot.Cultures.TryGetValue(cultureName, out CultureCatalog? cultureCatalog))
            {
                continue;
            }

            foreach (string moduleName in cultureCatalog.OrderedModuleNames)
            {
                ModuleCatalog moduleCatalog = cultureCatalog.Modules[moduleName];
                if (moduleCatalog.Strings.TryGetValue(name, out string? value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves or builds the cached command definitions and trigger lookup table for a workspace and culture.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cultureOverride">The optional culture override.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached command definitions and ordered trigger entries.</returns>
    private async ValueTask<CachedCommandCatalog> GetCachedCommandCatalogAsync(
        Guid workspaceId,
        string? cultureOverride,
        CancellationToken cancellationToken)
    {
        string effectiveCulture = await this.ResolveEffectiveCultureAsync(workspaceId, cultureOverride, cancellationToken).ConfigureAwait(false);
        CatalogSnapshot snapshot = this._snapshot;
        string cacheKey = $"i18n:cmds:{snapshot.Version}:{workspaceId:N}:{effectiveCulture}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct => await this.BuildCommandCatalogUncachedAsync(snapshot, workspaceId, effectiveCulture, ct).ConfigureAwait(false),
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds merged command definitions and trigger resolution mappings across workspace overrides and the culture fallback chain.
    /// </summary>
    /// <param name="snapshot">The active catalog snapshot.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="effectiveCulture">The effective workspace culture.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The built <see cref="CachedCommandCatalog"/>.</returns>
    private async ValueTask<CachedCommandCatalog> BuildCommandCatalogUncachedAsync(
        CatalogSnapshot snapshot,
        Guid workspaceId,
        string effectiveCulture,
        CancellationToken cancellationToken)
    {
        List<string> cultureChain = this.BuildCultureChain(effectiveCulture, includeCanonicalFallback: true);
        Dictionary<string, IReadOnlyList<string>> workspaceOverrides = await this.LoadWorkspaceCommandOverridesAsync(workspaceId, cancellationToken).ConfigureAwait(false);

        HashSet<string> commandIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (string commandId in workspaceOverrides.Keys)
        {
            _ = commandIds.Add(commandId);
        }

        foreach (string cultureName in cultureChain)
        {
            if (!snapshot.Cultures.TryGetValue(cultureName, out CultureCatalog? cultureCatalog))
            {
                continue;
            }

            foreach (string moduleName in cultureCatalog.OrderedModuleNames)
            {
                foreach (string commandId in cultureCatalog.Modules[moduleName].Commands.Keys)
                {
                    _ = commandIds.Add(commandId);
                }
            }
        }

        List<string> orderedCommandIds = [.. commandIds.OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)];
        Dictionary<string, List<string>> perCommandTriggers = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> perCommandSeen = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> perCommandDescriptions = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> perCommandModules = new(StringComparer.OrdinalIgnoreCase);

        foreach (string commandId in orderedCommandIds)
        {
            perCommandTriggers[commandId] = [];
            perCommandSeen[commandId] = new(StringComparer.OrdinalIgnoreCase);
            perCommandDescriptions[commandId] = null;
            perCommandModules[commandId] = null;
        }

        List<CommandTriggerEntry> triggerEntries = [];
        HashSet<string> seenGlobalTriggers = new(StringComparer.OrdinalIgnoreCase);

        foreach (string commandId in orderedCommandIds)
        {
            if (workspaceOverrides.TryGetValue(commandId, out IReadOnlyList<string>? overrideTriggers))
            {
                AppendTriggers(
                    commandId,
                    overrideTriggers,
                    perCommandTriggers[commandId],
                    perCommandSeen[commandId],
                    triggerEntries,
                    seenGlobalTriggers);
            }
        }

        foreach (string cultureName in cultureChain)
        {
            if (!snapshot.Cultures.TryGetValue(cultureName, out CultureCatalog? cultureCatalog))
            {
                continue;
            }

            foreach (string moduleName in cultureCatalog.OrderedModuleNames)
            {
                ModuleCatalog moduleCatalog = cultureCatalog.Modules[moduleName];
                foreach (string commandId in orderedCommandIds)
                {
                    if (!moduleCatalog.Commands.TryGetValue(commandId, out RawCommandDefinition? rawCommand))
                    {
                        continue;
                    }

                    perCommandModules[commandId] ??= rawCommand.Module;
                    if (perCommandDescriptions[commandId] is null && !string.IsNullOrWhiteSpace(rawCommand.Description))
                    {
                        perCommandDescriptions[commandId] = rawCommand.Description;
                    }

                    AppendTriggers(
                        commandId,
                        rawCommand.Triggers,
                        perCommandTriggers[commandId],
                        perCommandSeen[commandId],
                        triggerEntries,
                        seenGlobalTriggers);
                }
            }
        }

        List<LocalizedCommandDefinition> definitions = new(orderedCommandIds.Count);
        foreach (string commandId in orderedCommandIds)
        {
            definitions.Add(new(
                commandId,
                perCommandTriggers[commandId],
                perCommandDescriptions[commandId],
                perCommandModules[commandId]));
        }

        return new(definitions, triggerEntries);
    }

    /// <summary>
    /// Loads workspace command trigger overrides from <see cref="IWorkspaceKeyValueStore"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary mapping command identifiers to override trigger lists.</returns>
    private async ValueTask<Dictionary<string, IReadOnlyList<string>>> LoadWorkspaceCommandOverridesAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        Dictionary<string, IReadOnlyList<string>> overrides = new(StringComparer.OrdinalIgnoreCase);

        if (this._keyValueStore is null)
        {
            return overrides;
        }

        IReadOnlyList<string> commandIds = await this._keyValueStore.GetKeysAsync(
            workspaceId,
            _i18nTableName,
            _commandsOverrideSection,
            cancellationToken).ConfigureAwait(false);

        foreach (string commandId in commandIds)
        {
            string? rawJson = await this._keyValueStore.GetStringAsync(
                workspaceId,
                _i18nTableName,
                _commandsOverrideSection,
                commandId,
                defaultValue: null,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                continue;
            }

            try
            {
                string[]? parsed = JsonSerializer.Deserialize<string[]>(rawJson);
                if (parsed is not null)
                {
                    overrides[commandId] = parsed;
                }
            }
            catch (JsonException ex)
            {
                LogInvalidWorkspaceTriggerOverride(this._logger, workspaceId, commandId, ex);
            }
        }

        return overrides;
    }

    /// <summary>
    /// Builds a deduplicated culture fallback chain from exact culture to parent neutral cultures to the default culture.
    /// </summary>
    /// <param name="cultureName">The starting BCP-47 culture code.</param>
    /// <param name="includeCanonicalFallback">Whether to also append <c>en-US</c> if the configured default differs.</param>
    /// <returns>The ordered list of distinct culture codes.</returns>
    private List<string> BuildCultureChain(string cultureName, bool includeCanonicalFallback)
    {
        List<string> chain = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            string trimmed = cultureName.Trim();
            AddCultureWithParents(trimmed, chain, seen);
        }

        if (seen.Add(this._defaultCulture))
        {
            chain.Add(this._defaultCulture);
        }

        if (includeCanonicalFallback && seen.Add(_canonicalFallbackCulture))
        {
            chain.Add(_canonicalFallbackCulture);
        }

        return chain;
    }

    /// <summary>
    /// Adds a culture and its parent neutral cultures to the fallback chain.
    /// </summary>
    /// <param name="cultureName">The starting culture code.</param>
    /// <param name="chain">The destination culture chain.</param>
    /// <param name="seen">The deduplication set.</param>
    private static void AddCultureWithParents(string cultureName, List<string> chain, HashSet<string> seen)
    {
        try
        {
            CultureInfo current = CultureInfo.GetCultureInfo(cultureName);
            while (!string.IsNullOrEmpty(current.Name))
            {
                if (seen.Add(current.Name))
                {
                    chain.Add(current.Name);
                }

                if (Equals(current.Parent, CultureInfo.InvariantCulture) ||
                    string.Equals(current.Parent.Name, current.Name, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = current.Parent;
            }
        }
        catch (CultureNotFoundException)
        {
            string current = cultureName;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (seen.Add(current))
                {
                    chain.Add(current);
                }

                int hyphenIndex = current.LastIndexOf('-');
                if (hyphenIndex <= 0)
                {
                    break;
                }

                current = current[..hyphenIndex];
            }
        }
    }

    /// <summary>
    /// Logs a warning when the configured locales directory does not exist.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="directoryPath">The missing directory path.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Localization directory '{DirectoryPath}' does not exist; no disk catalogs loaded.")]
    private static partial void LogLocalesDirectoryMissing(ILogger logger, string directoryPath);

    /// <summary>
    /// Logs debug details when a localization catalog file is loaded.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="module">The module name.</param>
    /// <param name="stringCount">The number of flattened string keys loaded.</param>
    /// <param name="commandCount">The number of command definitions loaded.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded localization catalog '{Culture}/{Module}' ({StringCount} strings, {CommandCount} commands).")]
    private static partial void LogCatalogLoaded(ILogger logger, string culture, string module, int stringCount, int commandCount);

    /// <summary>
    /// Logs an error when a JSON localization catalog file cannot be read or parsed.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="filePath">The catalog file path.</param>
    /// <param name="exception">The exception encountered.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load localization catalog '{FilePath}'.")]
    private static partial void LogCatalogLoadFailed(ILogger logger, string filePath, Exception exception);

    /// <summary>
    /// Logs a warning when a catalog path outside the configured locales root is encountered and skipped.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="path">The unsafe path that was skipped.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped unsafe localization catalog path '{Path}'.")]
    private static partial void LogUnsafeCatalogPathSkipped(ILogger logger, string path);

    /// <summary>
    /// Logs a warning when a workspace command trigger override entry in the key-value store contains invalid JSON.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="exception">The JSON deserialization exception.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring invalid workspace command trigger override JSON for workspace {WorkspaceId} command '{CommandId}'.")]
    private static partial void LogInvalidWorkspaceTriggerOverride(ILogger logger, Guid workspaceId, string commandId, Exception exception);

    /// <summary>
    /// Represents an immutable snapshot of all loaded localization catalogs.
    /// </summary>
    /// <param name="Version">The snapshot version number.</param>
    /// <param name="Cultures">Loaded culture catalogs keyed by BCP-47 culture code.</param>
    /// <param name="AvailableCultures">Sorted list of available culture codes.</param>
    private sealed record CatalogSnapshot(
        long Version,
        Dictionary<string, CultureCatalog> Cultures,
        IReadOnlyList<string> AvailableCultures);

    /// <summary>
    /// Represents all loaded module catalogs for a single culture.
    /// </summary>
    /// <param name="Modules">Module catalogs keyed by module name.</param>
    /// <param name="OrderedModuleNames">Deterministic ordered list of module names.</param>
    private sealed record CultureCatalog(
        Dictionary<string, ModuleCatalog> Modules,
        IReadOnlyList<string> OrderedModuleNames);

    /// <summary>
    /// Represents the flattened strings and command definitions loaded from a single module JSON catalog.
    /// </summary>
    /// <param name="Strings">Flattened dotted string entries.</param>
    /// <param name="Commands">Parsed command definitions keyed by command identifier.</param>
    private sealed record ModuleCatalog(
        Dictionary<string, string> Strings,
        Dictionary<string, RawCommandDefinition> Commands);

    /// <summary>
    /// Represents a raw command entry parsed from a JSON localization catalog.
    /// </summary>
    /// <param name="CommandId">The canonical command identifier.</param>
    /// <param name="Triggers">The list of trigger phrases declared in the catalog.</param>
    /// <param name="Description">The optional command description.</param>
    /// <param name="Module">The owning module name.</param>
    private sealed record RawCommandDefinition(
        string CommandId,
        IReadOnlyList<string> Triggers,
        string? Description,
        string Module);

    /// <summary>
    /// Wrapper record for caching nullable localized strings inside <see cref="HybridCache"/>.
    /// </summary>
    /// <param name="Value">The resolved localized string, or <see langword="null"/> if missing.</param>
    internal sealed record CachedLocalizedValue(string? Value);

    /// <summary>
    /// Cached command definition list and ordered trigger lookup entries for a workspace and culture.
    /// </summary>
    /// <param name="Definitions">The resolved command definitions.</param>
    /// <param name="TriggerEntries">Ordered trigger-to-command mappings.</param>
    internal sealed record CachedCommandCatalog(
        IReadOnlyList<LocalizedCommandDefinition> Definitions,
        IReadOnlyList<CommandTriggerEntry> TriggerEntries);

    /// <summary>
    /// Represents a trigger phrase mapped to its canonical command identifier.
    /// </summary>
    /// <param name="Trigger">The trigger phrase.</param>
    /// <param name="CommandId">The canonical command identifier.</param>
    internal sealed record CommandTriggerEntry(string Trigger, string CommandId);
}
