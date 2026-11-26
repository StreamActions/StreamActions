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

using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using System.Globalization;
using Xunit;
using CoreLocalizationOptions = StreamActions.Core.Configuration.LocalizationOptions;

namespace StreamActions.Core.Tests.Localization;

/// <summary>
/// Unit tests for <see cref="StreamActionsLocalizer"/> and its DI registration.
/// </summary>
public sealed class StreamActionsLocalizerTests : IDisposable
{
    /// <summary>
    /// Temporary root folder created per test instance for isolated JSON localization catalogs.
    /// </summary>
    private readonly string _tempRoot;

    /// <summary>
    /// Root locales folder inside <see cref="_tempRoot"/>.
    /// </summary>
    private readonly string _localesDir;

    /// <summary>
    /// Disposable service providers created during tests.
    /// </summary>
    private readonly List<ServiceProvider> _providers = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamActionsLocalizerTests"/> class.
    /// </summary>
    public StreamActionsLocalizerTests()
    {
        this._tempRoot = Path.Combine(Path.GetTempPath(), $"sa-i18n-tests-{Guid.NewGuid():N}");
        this._localesDir = Path.Combine(this._tempRoot, "locales");
        _ = Directory.CreateDirectory(this._localesDir);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (ServiceProvider provider in this._providers)
        {
            provider.Dispose();
        }

        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    #region GetStringAsync

    /// <summary>
    /// Verifies the 4-layer fallback hierarchy: Workspace DB Override -> Exact Culture -> Parent Neutral Culture -> Default Culture.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStringAsync")]
    public async Task GetStringAsyncFourLayerFallbackResolvesInPriorityOrder()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, FakeWorkspaceKeyValueStore kvStore) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "fr-CA");
        await kvStore.SetAsync(workspaceId, "i18n", "moderation", "ui.overrideOnly", "Custom Workspace Title", TestContext.Current.CancellationToken);

        string? fromOverride = await localizer.GetStringAsync(workspaceId, "moderation", "ui.overrideOnly", cancellationToken: TestContext.Current.CancellationToken);
        string? fromExact = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        string? fromParent = await localizer.GetStringAsync(workspaceId, "moderation", "ui.parentOnly", cancellationToken: TestContext.Current.CancellationToken);
        string? fromDefault = await localizer.GetStringAsync(workspaceId, "moderation", "ui.defaultOnly", cancellationToken: TestContext.Current.CancellationToken);
        string? nestedKey = await localizer.GetStringAsync(workspaceId, "moderation", "ui.nested.header", cancellationToken: TestContext.Current.CancellationToken);

        _ = fromOverride.Should().Be("Custom Workspace Title");
        _ = fromExact.Should().Be("Titre Quebecois");
        _ = fromParent.Should().Be("Titre Francais");
        _ = fromDefault.Should().Be("Default English Only");
        _ = nestedKey.Should().Be("Parametres Moderation");
    }

    /// <summary>
    /// Verifies that an explicit culture override takes precedence over <see cref="Workspace.BotCulture"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStringAsync")]
    public async Task GetStringAsyncWithCultureOverrideOverridesWorkspaceBotCulture()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, _) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "en-US");

        string? result = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cultureOverride: "fr-CA", cancellationToken: TestContext.Current.CancellationToken);

        _ = result.Should().Be("Titre Quebecois");
    }

    /// <summary>
    /// Verifies that missing localization keys return <see langword="null"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStringAsync")]
    public async Task GetStringAsyncWhenKeyMissingReturnsNull()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, _) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "fr-CA");

        string? missingKey = await localizer.GetStringAsync(workspaceId, "moderation", "ui.doesNotExist", cancellationToken: TestContext.Current.CancellationToken);
        string? missingModule = await localizer.GetStringAsync(workspaceId, "unknown", "ui.title", cancellationToken: TestContext.Current.CancellationToken);

        _ = missingKey.Should().BeNull();
        _ = missingModule.Should().BeNull();
    }

    /// <summary>
    /// Verifies that malformed path-traversal inputs in module or culture override are rejected.
    /// </summary>
    /// <param name="malformedInput">A malformed module or culture input attempting path traversal.</param>
    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("foo/bar")]
    [InlineData("foo\\bar")]
    [InlineData(".")]
    [InlineData("/etc/passwd")]
    [Trait("Member", "GetStringAsync")]
    public async Task GetStringAsyncPathTraversalInputThrowsArgumentException(string malformedInput)
    {
        this.SeedFallbackCatalogs();
        string outsideFile = Path.Combine(this._tempRoot, "secret.json");
        await File.WriteAllTextAsync(outsideFile, """{"ui":{"title":"Leaked Secret"}}""", TestContext.Current.CancellationToken);

        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, _, _) = this.CreateSut();

        Func<Task> badModuleAct = async () => _ = await localizer.GetStringAsync(workspaceId, malformedInput, "ui.title").ConfigureAwait(false);
        Func<Task> badCultureAct = async () => _ = await localizer.GetStringAsync(workspaceId, "moderation", "ui.title", cultureOverride: malformedInput).ConfigureAwait(false);

        _ = await badModuleAct.Should().ThrowAsync<ArgumentException>();
        _ = await badCultureAct.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetResponseTemplateAsync

    /// <summary>
    /// Verifies that <see cref="StreamActionsLocalizer.GetResponseTemplateAsync"/> returns raw unformatted templates with placeholders intact.
    /// </summary>
    [Fact]
    [Trait("Member", "GetResponseTemplateAsync")]
    public async Task GetResponseTemplateAsyncReturnsRawTagTemplateWithoutFormatting()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, _) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "fr-CA");

        string? withoutPrefix = await localizer.GetResponseTemplateAsync(workspaceId, "moderation", "permitGranted", cancellationToken: TestContext.Current.CancellationToken);
        string? withPrefix = await localizer.GetResponseTemplateAsync(workspaceId, "moderation", "responses.permitGranted", cancellationToken: TestContext.Current.CancellationToken);
        string? missing = await localizer.GetResponseTemplateAsync(workspaceId, "moderation", "missingTemplate", cancellationToken: TestContext.Current.CancellationToken);

        _ = withoutPrefix.Should().Be("{sender}, {1} a la permission pour les liens.");
        _ = withPrefix.Should().Be("{sender}, {1} a la permission pour les liens.");
        _ = missing.Should().BeNull();
    }

    /// <summary>
    /// Verifies that workspace database overrides work whether stored with or without the <c>responses.</c> prefix.
    /// </summary>
    [Fact]
    [Trait("Member", "GetResponseTemplateAsync")]
    public async Task GetResponseTemplateAsyncWorkspaceOverridesRespectPrefixAndDirectKey()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, FakeWorkspaceKeyValueStore kvStore) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "fr");

        await kvStore.SetAsync(workspaceId, "i18n", "moderation", "responses.permitGranted", "Prefixed {sender} -> {1}", TestContext.Current.CancellationToken);
        await kvStore.SetAsync(workspaceId, "i18n", "moderation", "directOnly", "Direct {sender}", TestContext.Current.CancellationToken);

        string? prefixedResult = await localizer.GetResponseTemplateAsync(workspaceId, "moderation", "permitGranted", cancellationToken: TestContext.Current.CancellationToken);
        string? directResult = await localizer.GetResponseTemplateAsync(workspaceId, "moderation", "directOnly", cancellationToken: TestContext.Current.CancellationToken);

        _ = prefixedResult.Should().Be("Prefixed {sender} -> {1}");
        _ = directResult.Should().Be("Direct {sender}");
    }

    #endregion

    #region GetCommandDefinitionsAsync

    /// <summary>
    /// Verifies that localized, parent, canonical <c>en-US</c>, and workspace-override triggers are merged and deduplicated case-insensitively.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCommandDefinitionsAsync")]
    public async Task GetCommandDefinitionsAsyncCombinesWorkspaceLocalizedParentAndEnglishTriggers()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, _) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "fr-CA");
        await localizer.SetWorkspaceCommandTriggersAsync(workspaceId, "bot.permit", ["allowme", "AUTORISER", "AllowMe"], TestContext.Current.CancellationToken);

        IReadOnlyList<LocalizedCommandDefinition> definitions = await localizer.GetCommandDefinitionsAsync(workspaceId, cancellationToken: TestContext.Current.CancellationToken);
        LocalizedCommandDefinition? permit = definitions.FirstOrDefault(static d => d.CommandId == "bot.permit");

        _ = permit.Should().NotBeNull();
        _ = permit.Module.Should().Be("moderation");
        _ = permit.Description.Should().Be("Permet temporairement les liens (fr).");
        _ = permit.Triggers.Should().ContainInOrder("allowme", "AUTORISER", "permettre", "permit");
        _ = permit.Triggers.Should().HaveCount(4);
    }

    #endregion

    #region ResolveCommandIdAsync

    /// <summary>
    /// Verifies that localized triggers, parent triggers, workspace custom triggers, and English fallbacks all resolve to the canonical command ID.
    /// </summary>
    /// <param name="trigger">The trigger phrase entered in chat.</param>
    /// <param name="expectedCommandId">The expected canonical command identifier.</param>
    [Theory]
    [InlineData("custompermit", "bot.permit")]
    [InlineData("autoriser", "bot.permit")]
    [InlineData("PERMETTRE", "bot.permit")]
    [InlineData("permit", "bot.permit")]
    [InlineData("verrouillage", "bot.lockdown")]
    [InlineData("lockdown", "bot.lockdown")]
    [InlineData("nonexistent", null)]
    [Trait("Member", "ResolveCommandIdAsync")]
    public async Task ResolveCommandIdAsyncResolvesLocalizedAndEnglishTriggers(string trigger, string? expectedCommandId)
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, _) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "fr-CA");
        await localizer.SetWorkspaceCommandTriggersAsync(workspaceId, "bot.permit", ["custompermit"], TestContext.Current.CancellationToken);

        string? resolved = await localizer.ResolveCommandIdAsync(workspaceId, trigger, cancellationToken: TestContext.Current.CancellationToken);

        _ = resolved.Should().Be(expectedCommandId);
    }

    #endregion

    #region HybridCacheAndInvalidation

    /// <summary>
    /// Verifies that <see cref="HybridCache"/> serves repeated lookups with zero database reads and evicts immediately on mutation or invalidation.
    /// </summary>
    [Fact]
    [Trait("Member", "InvalidateWorkspaceCacheAsync")]
    public async Task HybridCacheCachesHotLookupsAndEvictsOnMutationOrInvalidation()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        Guid otherWorkspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, FakeWorkspaceKeyValueStore kvStore) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "fr-CA");
        workspaceStore.Workspaces[otherWorkspaceId] = CreateWorkspace(otherWorkspaceId, "en-US");

        string? first = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        _ = await localizer.GetStringAsync(otherWorkspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);

        int workspaceReadsAfterWarmup = workspaceStore.GetByIdCallCount;
        int kvReadsAfterWarmup = kvStore.GetStringCallCount;

        string? second = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        string? missingFirst = await localizer.GetStringAsync(workspaceId, "moderation", "ui.missingKey", cancellationToken: TestContext.Current.CancellationToken);
        int kvReadsAfterFirstMissing = kvStore.GetStringCallCount;
        string? missingSecond = await localizer.GetStringAsync(workspaceId, "moderation", "ui.missingKey", cancellationToken: TestContext.Current.CancellationToken);

        _ = first.Should().Be("Titre Quebecois");
        _ = second.Should().Be("Titre Quebecois");
        _ = missingFirst.Should().BeNull();
        _ = missingSecond.Should().BeNull();
        _ = workspaceStore.GetByIdCallCount.Should().Be(workspaceReadsAfterWarmup);
        _ = kvStore.GetStringCallCount.Should().Be(kvReadsAfterFirstMissing);
        _ = kvReadsAfterFirstMissing.Should().Be(kvReadsAfterWarmup + 1);

        await localizer.SetWorkspaceOverrideAsync(workspaceId, "moderation", "ui.exactOnly", "Override V1", TestContext.Current.CancellationToken);
        string? afterSet = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        _ = afterSet.Should().Be("Override V1");

        bool removed = await localizer.RemoveWorkspaceOverrideAsync(workspaceId, "moderation", "ui.exactOnly", TestContext.Current.CancellationToken);
        _ = removed.Should().BeTrue();
        string? afterRemove = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        _ = afterRemove.Should().Be("Titre Quebecois");

        workspaceStore.Workspaces[workspaceId].BotCulture = "en-US";
        int otherReadsBefore = kvStore.GetStringCallCount;
        _ = await localizer.GetStringAsync(otherWorkspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        _ = kvStore.GetStringCallCount.Should().Be(otherReadsBefore);

        await localizer.InvalidateWorkspaceCacheAsync(workspaceId, TestContext.Current.CancellationToken);
        string? afterWorkspaceInvalidate = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        _ = afterWorkspaceInvalidate.Should().Be("English Exact Title");
    }

    #endregion

    #region IStringLocalizer

    /// <summary>
    /// Verifies that <see cref="IStringLocalizer"/> uses <see cref="CultureInfo.CurrentUICulture"/> independently of <see cref="Workspace.BotCulture"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "IStringLocalizer")]
    public async Task StringLocalizerUsesCurrentUICultureIndependentlyFromWorkspaceBotCulture()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, _) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "en-US");

        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");

            LocalizedString moduleQualified = localizer["moderation:ui.exactOnly"];
            LocalizedString unscopedParent = localizer["ui.parentOnly"];
            LocalizedString formatted = localizer["moderation:ui.formatted", "Alice"];
            LocalizedString missing = localizer["moderation:ui.notFound"];
            List<LocalizedString> allStrings = [.. localizer.GetAllStrings(includeParentCultures: true)];

            string? botString = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);

            _ = moduleQualified.ResourceNotFound.Should().BeFalse();
            _ = moduleQualified.Value.Should().Be("Titre Quebecois");
            _ = unscopedParent.ResourceNotFound.Should().BeFalse();
            _ = unscopedParent.Value.Should().Be("Titre Francais");
            _ = formatted.ResourceNotFound.Should().BeFalse();
            _ = formatted.Value.Should().Be("Bonjour Alice");
            _ = missing.ResourceNotFound.Should().BeTrue();
            _ = missing.Value.Should().Be("moderation:ui.notFound");
            _ = allStrings.Should().Contain(static item => item.Name == "moderation:ui.exactOnly" && item.Value == "Titre Quebecois");
            _ = allStrings.Should().Contain(static item => item.Name == "moderation:ui.defaultOnly" && item.Value == "Default English Only");
            _ = botString.Should().Be("English Exact Title");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    #endregion

    #region CatalogManagementAndDI

    /// <summary>
    /// Verifies that default <c>en-US</c> catalogs ship in the build output and load cleanly.
    /// </summary>
    [Fact]
    [Trait("Member", "ReloadCatalogs")]
    public async Task DefaultCatalogsLoadBuiltInEnglishCoreAndModerationCatalogs()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        this._providers.Add(provider);

        IOptions<CoreLocalizationOptions> options = Options.Create(new CoreLocalizationOptions());
        StreamActionsLocalizer localizer = new(
            provider.GetRequiredService<HybridCache>(),
            options,
            NullLogger<StreamActionsLocalizer>.Instance);

        IReadOnlyList<string> cultures = await localizer.GetAvailableCulturesAsync(TestContext.Current.CancellationToken);
        string? coreTitle = await localizer.GetStringAsync(Guid.NewGuid(), "core", "ui.title", cancellationToken: TestContext.Current.CancellationToken);
        string? permitTemplate = await localizer.GetResponseTemplateAsync(Guid.NewGuid(), "moderation", "permitGranted", cancellationToken: TestContext.Current.CancellationToken);
        string? commandId = await localizer.ResolveCommandIdAsync(Guid.NewGuid(), "lockdown", cancellationToken: TestContext.Current.CancellationToken);

        _ = cultures.Should().Contain("en-US");
        _ = coreTitle.Should().Be("StreamActions");
        _ = permitTemplate.Should().Contain("{sender}");
        _ = commandId.Should().Be("bot.lockdown");
    }

    /// <summary>
    /// Verifies that <see cref="StreamActionsLocalizer.ReloadCatalogs"/> loads newly added disk catalogs and refreshes cached lookups.
    /// </summary>
    [Fact]
    [Trait("Member", "ReloadCatalogs")]
    public async Task ReloadCatalogsPicksUpNewCatalogAndInvalidatesCachedValues()
    {
        this.SeedFallbackCatalogs();
        Guid workspaceId = Guid.NewGuid();
        (StreamActionsLocalizer localizer, FakeWorkspaceStore workspaceStore, _) = this.CreateSut();
        workspaceStore.Workspaces[workspaceId] = CreateWorkspace(workspaceId, "es-ES");

        string? beforeReload = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);
        _ = beforeReload.Should().Be("English Exact Title");

        string esDir = Path.Combine(this._localesDir, "es-ES");
        _ = Directory.CreateDirectory(esDir);
        await File.WriteAllTextAsync(Path.Combine(esDir, "moderation.json"), """{"ui":{"exactOnly":"Titulo Espanol"}}""", TestContext.Current.CancellationToken);

        localizer.ReloadCatalogs();

        IReadOnlyList<string> cultures = await localizer.GetAvailableCulturesAsync(TestContext.Current.CancellationToken);
        string? afterReload = await localizer.GetStringAsync(workspaceId, "moderation", "ui.exactOnly", cancellationToken: TestContext.Current.CancellationToken);

        _ = cultures.Should().Contain("es-ES");
        _ = afterReload.Should().Be("Titulo Espanol");
    }

    /// <summary>
    /// Verifies that <see cref="ServiceCollectionExtensions.AddStreamActionsCore"/> registers localization options and forwards <see cref="IStringLocalizer"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "AddStreamActionsCore")]
    public void AddStreamActionsCoreRegistersLocalizerAndForwardsIStringLocalizer()
    {
        Dictionary<string, string?> configValues = new()
        {
            ["Localization:LocalesDirectory"] = this._localesDir,
            ["Localization:DefaultCulture"] = "en-US",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddStreamActionsCore(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        this._providers.Add(provider);

        IStreamActionsLocalizer streamLocalizer = provider.GetRequiredService<IStreamActionsLocalizer>();
        IStringLocalizer stringLocalizer = provider.GetRequiredService<IStringLocalizer>();
        IOptions<CoreLocalizationOptions> options = provider.GetRequiredService<IOptions<CoreLocalizationOptions>>();

        _ = stringLocalizer.Should().BeSameAs(streamLocalizer);
        _ = options.Value.LocalesDirectory.Should().Be(this._localesDir);
        _ = options.Value.DefaultCulture.Should().Be("en-US");
    }

    #endregion

    /// <summary>
    /// Creates a configured <see cref="StreamActionsLocalizer"/> and its in-memory store fakes.
    /// </summary>
    /// <returns>The system under test paired with its fake stores.</returns>
    private (StreamActionsLocalizer Localizer, FakeWorkspaceStore WorkspaceStore, FakeWorkspaceKeyValueStore KeyValueStore) CreateSut()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        this._providers.Add(provider);

        FakeWorkspaceStore workspaceStore = new();
        FakeWorkspaceKeyValueStore keyValueStore = new();
        IOptions<CoreLocalizationOptions> options = Options.Create(new CoreLocalizationOptions
        {
            LocalesDirectory = this._localesDir,
            DefaultCulture = "en-US",
        });

        StreamActionsLocalizer localizer = new(
            provider.GetRequiredService<HybridCache>(),
            options,
            NullLogger<StreamActionsLocalizer>.Instance,
            workspaceStore,
            keyValueStore);

        return (localizer, workspaceStore, keyValueStore);
    }

    /// <summary>
    /// Writes test <c>en-US</c>, <c>fr</c>, and <c>fr-CA</c> catalogs into <see cref="_localesDir"/>.
    /// </summary>
    private void SeedFallbackCatalogs()
    {
        string enUsDir = Path.Combine(this._localesDir, "en-US");
        string frDir = Path.Combine(this._localesDir, "fr");
        string frCaDir = Path.Combine(this._localesDir, "fr-CA");
        _ = Directory.CreateDirectory(enUsDir);
        _ = Directory.CreateDirectory(frDir);
        _ = Directory.CreateDirectory(frCaDir);

        File.WriteAllText(
            Path.Combine(enUsDir, "core.json"),
            """
            {
              "ui": {
                "title": "StreamActions Core"
              }
            }
            """);

        File.WriteAllText(
            Path.Combine(enUsDir, "moderation.json"),
            """
            {
              "ui": {
                "overrideOnly": "Default Override Key",
                "exactOnly": "English Exact Title",
                "parentOnly": "English Parent Title",
                "defaultOnly": "Default English Only",
                "formatted": "Hello {0}"
              },
              "responses": {
                "permitGranted": "{sender}, {1} is permitted."
              },
              "commands": {
                "bot.permit": {
                  "triggers": ["permit"],
                  "description": "Temporarily permits links."
                },
                "bot.lockdown": {
                  "triggers": ["lockdown"],
                  "description": "Toggles lockdown."
                }
              }
            }
            """);

        File.WriteAllText(
            Path.Combine(frDir, "moderation.json"),
            """
            {
              "ui": {
                "overrideOnly": "Parent Override Key",
                "exactOnly": "Titre Francais Exact",
                "parentOnly": "Titre Francais",
                "formatted": "Bonjour {0}"
              },
              "commands": {
                "bot.permit": {
                  "triggers": ["permettre"],
                  "description": "Permet temporairement les liens (fr)."
                },
                "bot.lockdown": {
                  "triggers": ["verrouillage"],
                  "description": "Active le verrouillage."
                }
              }
            }
            """);

        File.WriteAllText(
            Path.Combine(frCaDir, "moderation.json"),
            """
            {
              "ui": {
                "overrideOnly": "Exact Override Key",
                "exactOnly": "Titre Quebecois",
                "nested": {
                  "header": "Parametres Moderation"
                }
              },
              "responses": {
                "permitGranted": "{sender}, {1} a la permission pour les liens."
              },
              "commands": {
                "bot.permit": {
                  "triggers": ["autoriser"]
                }
              }
            }
            """);
    }

    /// <summary>
    /// Creates a minimal <see cref="Workspace"/> entity with the specified bot culture.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="botCulture">The workspace BCP-47 bot culture code.</param>
    /// <returns>The populated <see cref="Workspace"/> instance.</returns>
    private static Workspace CreateWorkspace(Guid workspaceId, string botCulture) => new()
    {
        Id = workspaceId,
        OwnerUserId = Guid.NewGuid(),
        Name = "Test Workspace",
        Slug = $"ws-{workspaceId:N}",
        PrimaryPlatformId = "twitch",
        BotCulture = botCulture,
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// In-memory <see cref="IWorkspaceStore"/> fake that tracks <see cref="GetByIdAsync"/> invocations.
    /// </summary>
    private sealed class FakeWorkspaceStore : IWorkspaceStore
    {
        /// <summary>
        /// Gets the stored workspaces keyed by workspace identifier.
        /// </summary>
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];

        /// <summary>
        /// Gets the number of times <see cref="GetByIdAsync"/> has been called.
        /// </summary>
        public int GetByIdCallCount { get; private set; }

        /// <inheritdoc/>
        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            this.GetByIdCallCount++;
            return ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));
        }

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, bool clearClientSecret = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// In-memory <see cref="IWorkspaceKeyValueStore"/> fake that tracks <see cref="GetStringAsync"/> invocations.
    /// </summary>
    private sealed class FakeWorkspaceKeyValueStore : IWorkspaceKeyValueStore
    {
        /// <summary>
        /// In-memory key-value dictionary keyed by workspace, table, section, and variable.
        /// </summary>
        private readonly Dictionary<(Guid WorkspaceId, string Table, string Section, string Variable), string> _entries = [];

        /// <summary>
        /// Gets the number of times <see cref="GetStringAsync"/> has been called.
        /// </summary>
        public int GetStringCallCount { get; private set; }

        /// <inheritdoc/>
        public ValueTask<string?> GetStringAsync(Guid workspaceId, string table, string section, string variable, string? defaultValue = null, CancellationToken ct = default)
        {
            this.GetStringCallCount++;
            return ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? value) ? value : defaultValue);
        }

        /// <inheritdoc/>
        public ValueTask<bool> GetBoolAsync(Guid workspaceId, string table, string section, string variable, bool defaultValue = false, CancellationToken ct = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<int> GetIntAsync(Guid workspaceId, string table, string section, string variable, int defaultValue = 0, CancellationToken ct = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<long> GetLongAsync(Guid workspaceId, string table, string section, string variable, long defaultValue = 0L, CancellationToken ct = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<float> GetFloatAsync(Guid workspaceId, string table, string section, string variable, float defaultValue = 0f, CancellationToken ct = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<double> GetDoubleAsync(Guid workspaceId, string table, string section, string variable, double defaultValue = 0d, CancellationToken ct = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask SetAsync<T>(Guid workspaceId, string table, string section, string variable, T value, CancellationToken ct = default)
        {
            string formatted = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            this._entries[(workspaceId, table, section, variable)] = formatted;
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask<long> IncrementLongAsync(Guid workspaceId, string table, string section, string variable, long delta = 1L, CancellationToken ct = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<bool> DeleteAsync(Guid workspaceId, string table, string section, string variable, CancellationToken ct = default) => ValueTask.FromResult(this._entries.Remove((workspaceId, table, section, variable)));

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<string>> GetKeysAsync(Guid workspaceId, string table, string section, CancellationToken ct = default)
        {
            List<string> keys = [.. this._entries.Keys
                .Where(k => k.WorkspaceId == workspaceId &&
                            string.Equals(k.Table, table, StringComparison.Ordinal) &&
                            string.Equals(k.Section, section, StringComparison.Ordinal))
                .Select(static k => k.Variable)];
            return ValueTask.FromResult<IReadOnlyList<string>>(keys);
        }
    }
}
