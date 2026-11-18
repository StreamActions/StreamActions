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

using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Daemon;
using StreamActions.Web.Components.Pages;
using StreamActions.Web.Endpoints;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using TestContext = Xunit.TestContext;

namespace StreamActions.Web.Tests;

public sealed partial class SetupTests
{
    #region SetupComponent
    [Fact]
    [Trait("Member", "Setup")]
    public void Setup_WhenNoRootUserExistsAndTokenActive_RendersInitialSetupFormWithoutLeakingToken()
    {
        const string secretBootstrapToken = "deadbeef0123456789abcdef01234567";
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IBootstrapTokenService>(
            new StubBootstrapTokenService(new BootstrapTokenRequestResult(true, secretBootstrapToken, null)));
        _ = context.Services.AddSingleton<IPanelUserStore>(new StubPanelUserStore(hasRootUser: false));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<Setup> component = context.Render<Setup>();

        _ = component.Markup.Should().Contain("Initial Setup");
        _ = component.Markup.Should().Contain("action=\"/setup/verify\"");
        _ = component.Markup.Should().Contain("name=\"__RequestVerificationToken\"");
        _ = component.Markup.Should().Contain("name=\"bootstrapToken\"");
        _ = component.Markup.Should().Contain("name=\"platformId\"");
        _ = component.Markup.Should().Contain("value=\"twitch\"");
        _ = component.Markup.Should().Contain("name=\"clientId\"");
        _ = component.Markup.Should().Contain("name=\"clientSecret\"");
        _ = component.Markup.Should().NotContain(secretBootstrapToken);
    }

    [Fact]
    [Trait("Member", "Setup")]
    public void Setup_WhenRootUserExists_RendersRootRecoveryMode()
    {
        const string secretBootstrapToken = "cafebabe0123456789abcdef01234567";
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IBootstrapTokenService>(
            new StubBootstrapTokenService(new BootstrapTokenRequestResult(true, secretBootstrapToken, null)));
        _ = context.Services.AddSingleton<IPanelUserStore>(new StubPanelUserStore(hasRootUser: true));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<Setup> component = context.Render<Setup>();

        _ = component.Markup.Should().Contain("Root Recovery");
        _ = component.Markup.Should().NotContain("Initial Setup");
        _ = component.Markup.Should().NotContain(secretBootstrapToken);
    }

    [Fact]
    [Trait("Member", "Setup")]
    public void Setup_WhenTokenIsInCooldown_DisplaysCooldownMessageAndHidesForm()
    {
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IBootstrapTokenService>(
            new StubBootstrapTokenService(new BootstrapTokenRequestResult(false, null, TimeSpan.FromSeconds(42))));
        _ = context.Services.AddSingleton<IPanelUserStore>(new StubPanelUserStore(hasRootUser: false));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<Setup> component = context.Render<Setup>();

        _ = component.Markup.Should().Contain("cooldown");
        _ = component.Markup.Should().Contain("42");
        _ = component.Markup.Should().NotContain("action=\"/setup/verify\"");
    }

    [Fact]
    [Trait("Member", "Setup")]
    public void Setup_WhenRootRecoveryWithMultiplePlatformsAndStoredOAuth_RendersDynamicPlatformsOptionalLabelsResetCheckboxAndTicketLink()
    {
        EphemeralDataProtectionProvider dataProtection = new();
        DefaultHttpContext httpContext = new()
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IDataProtectionProvider>(dataProtection)
                .BuildServiceProvider(),
        };

        string ticketJson = JsonSerializer.Serialize(new SetupTicketPayload
        {
            Nonce = "abc123",
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15),
            IsRootRecovery = true,
            PlatformId = "kick",
            ResetRootMfa = false,
        });
        string protectedTicket = dataProtection
            .CreateProtector(SetupEndpoints.SetupTicketProtectorPurpose)
            .ToTimeLimitedDataProtector()
            .Protect(ticketJson, SetupEndpoints.SetupTicketLifetime);
        httpContext.Request.Headers.Cookie = $"{SetupEndpoints.SetupTicketCookieName}={Uri.EscapeDataString(protectedTicket)}";

        using BunitContext context = new();
        _ = context.Services.AddSingleton<IBootstrapTokenService>(
            new StubBootstrapTokenService(new BootstrapTokenRequestResult(true, "token123", null)));
        _ = context.Services.AddSingleton<IPanelUserStore>(new StubPanelUserStore(hasRootUser: true));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("kick"));
        _ = context.Services.AddSingleton<IInstanceOAuthConfigService>(new StubInstanceOAuthConfigService(
            new Dictionary<string, PlatformOAuthAppCredentials>
            {
                ["twitch"] = new("existing-cid", "existing-secret"),
            }));

        IRenderedComponent<Setup> component = context.Render<Setup>(parameters =>
            parameters.AddCascadingValue(httpContext));

        _ = component.Markup.Should().Contain("value=\"kick\"");
        _ = component.Markup.Should().Contain("value=\"twitch\"");
        _ = component.Markup.Should().Contain("Continue with Kick");
        _ = component.Markup.Should().Contain("/auth/kick/login?flow=setup");
        _ = component.Markup.Should().Contain("OAuth Client ID (optional");
        _ = component.Markup.Should().Contain("OAuth Client Secret (optional");
        _ = component.Markup.Should().Contain("name=\"resetMfaSubmitted\" value=\"true\"");
        _ = component.Markup.Should().Contain("id=\"reset-mfa\"");
    }
    #endregion

    #region MapSetupEndpoints
    [Fact]
    [Trait("Member", "MapSetupEndpoints")]
    public async Task GetSetup_AndGetSetupRecovery_ReturnOkAndNeverLeakBootstrapToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using WebTestApplicationFactory factory = new(dirs);
        using HttpClient client = factory.CreateClient();

        IBootstrapTokenService tokenService = factory.Services.GetRequiredService<IBootstrapTokenService>();
        BootstrapTokenRequestResult tokenResult = tokenService.RequestOrReuseActiveToken();
        _ = tokenResult.Succeeded.Should().BeTrue();
        _ = tokenResult.Token.Should().NotBeNullOrWhiteSpace();

        HttpResponseMessage setupResponse = await client.GetAsync(new Uri("/setup", UriKind.Relative), ct);
        string setupHtml = await setupResponse.Content.ReadAsStringAsync(ct);

        HttpResponseMessage recoveryResponse = await client.GetAsync(new Uri("/setup-recovery", UriKind.Relative), ct);
        string recoveryHtml = await recoveryResponse.Content.ReadAsStringAsync(ct);

        _ = setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = recoveryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = setupHtml.Should().Contain("action=\"/setup/verify\"");
        _ = setupHtml.Should().Contain("name=\"__RequestVerificationToken\"");
        _ = setupHtml.Should().NotContain(tokenResult.Token!);
        _ = recoveryHtml.Should().NotContain(tokenResult.Token!);
    }

    [Fact]
    [Trait("Member", "MapSetupEndpoints")]
    public async Task PostSetupVerify_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using WebTestApplicationFactory factory = new(dirs);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/setup/verify",
            new { bootstrapToken = "invalid-token-0000" },
            ct);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _ = response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "MapSetupEndpoints")]
    public async Task PostSetupVerify_WhenRateLimitExceeded_ReturnsTooManyRequests()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using WebTestApplicationFactory factory = new(dirs);
        using HttpClient client = factory.CreateClient();

        for (int attempt = 0; attempt < 5; attempt++)
        {
            HttpResponseMessage attemptResponse = await client.PostAsJsonAsync(
                "/setup/verify",
                new { bootstrapToken = "wrong-token" },
                ct);
            _ = attemptResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        HttpResponseMessage rateLimitedResponse = await client.PostAsJsonAsync(
            "/setup/verify",
            new { bootstrapToken = "wrong-token" },
            ct);

        _ = rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    [Trait("Member", "MapSetupEndpoints")]
    public async Task PostSetupVerify_WhenTokenIsValid_SavesCredentialsBurnsTokenAndSetsHttpOnlySetupTicketCookie()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using WebTestApplicationFactory factory = new(dirs);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        HttpResponseMessage getSetupResponse = await client.GetAsync(new Uri("/setup", UriKind.Relative), ct);
        _ = getSetupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        string setupHtml = await getSetupResponse.Content.ReadAsStringAsync(ct);

        Match antiforgeryMatch = AntiforgeryInputRegex().Match(setupHtml);
        _ = antiforgeryMatch.Success.Should().BeTrue();
        string antiforgeryToken = antiforgeryMatch.Groups["value"].Value;

        IBootstrapTokenService tokenService = factory.Services.GetRequiredService<IBootstrapTokenService>();
        BootstrapTokenRequestResult tokenResult = tokenService.RequestOrReuseActiveToken();
        _ = tokenResult.Succeeded.Should().BeTrue();
        string activeToken = tokenResult.Token!;

        using FormUrlEncodedContent formContent = new(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
            new KeyValuePair<string, string>("bootstrapToken", activeToken),
            new KeyValuePair<string, string>("platformId", "twitch"),
            new KeyValuePair<string, string>("clientId", "test-twitch-client-id"),
            new KeyValuePair<string, string>("clientSecret", "super-secret-client-secret"),
        ]);

        HttpResponseMessage verifyResponse = await client.PostAsync(new Uri("/setup/verify", UriKind.Relative), formContent, ct);
        _ = verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        IReadOnlyList<string> setCookies = [.. verifyResponse.Headers.GetValues("Set-Cookie")];
        string? setupCookieHeader = setCookies.FirstOrDefault(static c =>
            c.StartsWith($"{SetupEndpoints.SetupTicketCookieName}=", StringComparison.Ordinal));
        _ = setupCookieHeader.Should().NotBeNull();
        _ = setupCookieHeader!.Should().ContainEquivalentOf("httponly");

        string encodedCookieValue = setupCookieHeader
            .Split(';', 2)[0]
            [(SetupEndpoints.SetupTicketCookieName.Length + 1)..];
        string protectedTicket = Uri.UnescapeDataString(encodedCookieValue);

        IDataProtectionProvider dataProtectionProvider = factory.Services.GetRequiredService<IDataProtectionProvider>();
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(SetupEndpoints.SetupTicketProtectorPurpose)
            .ToTimeLimitedDataProtector();
        string unprotectedJson = protector.Unprotect(protectedTicket);
        SetupTicketPayload? payload = JsonSerializer.Deserialize<SetupTicketPayload>(unprotectedJson);
        _ = payload.Should().NotBeNull();
        _ = payload!.Nonce.Should().NotBeNullOrWhiteSpace();
        _ = payload.IsRootRecovery.Should().BeFalse();

        IInstanceOAuthConfigService oauthService = factory.Services.GetRequiredService<IInstanceOAuthConfigService>();
        PlatformOAuthAppCredentials? savedCredentials = await oauthService.GetAsync("twitch", ct);
        _ = savedCredentials.Should().NotBeNull();
        _ = savedCredentials!.ClientId.Should().Be("test-twitch-client-id");
        _ = savedCredentials.ClientSecret.Should().Be("super-secret-client-secret");

        HttpResponseMessage reuseResponse = await client.PostAsJsonAsync(
            "/setup/verify",
            new { bootstrapToken = activeToken },
            ct);
        _ = reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        HttpResponseMessage revisitResponse = await client.GetAsync(new Uri("/setup", UriKind.Relative), ct);
        string revisitHtml = await revisitResponse.Content.ReadAsStringAsync(ct);
        _ = revisitHtml.Should().NotContain("super-secret-client-secret");
    }
    #endregion

    /// <summary>
    /// Matches the hidden antiforgery token input rendered inside <see cref="Setup"/>.
    /// </summary>
    /// <returns>The compiled regular expression.</returns>
    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="(?<value>[^"]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryInputRegex();

    /// <summary>
    /// Stub implementation of <see cref="IBootstrapTokenService"/> for isolated bUnit component tests.
    /// </summary>
    /// <param name="result">The fixed result returned by <see cref="RequestOrReuseActiveToken"/>.</param>
    private sealed class StubBootstrapTokenService(BootstrapTokenRequestResult result) : IBootstrapTokenService
    {
        /// <summary>
        /// Configured token request result.
        /// </summary>
        private readonly BootstrapTokenRequestResult _result = result;

        /// <inheritdoc/>
        public BootstrapTokenRequestResult RequestOrReuseActiveToken() => this._result;

        /// <inheritdoc/>
        public string GetOrCreateToken() => this._result.Token ?? throw new InvalidOperationException("No token.");

        /// <inheritdoc/>
        public bool ValidateToken(string candidate) => false;

        /// <inheritdoc/>
        public void Invalidate()
        {
        }
    }

    /// <summary>
    /// Stub implementation of <see cref="IPanelUserStore"/> for isolated bUnit component tests.
    /// </summary>
    /// <param name="hasRootUser">The value returned by <see cref="HasRootUserAsync"/>.</param>
    private sealed class StubPanelUserStore(bool hasRootUser) : IPanelUserStore
    {
        /// <summary>
        /// Whether a root panel user exists.
        /// </summary>
        private readonly bool _hasRootUser = hasRootUser;

        /// <inheritdoc/>
        public ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this._hasRootUser);

        /// <inheritdoc/>
        public ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this._hasRootUser);

        /// <inheritdoc/>
        public ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => ValueTask.FromResult<PanelUser?>(null);

        /// <inheritdoc/>
        public ValueTask<PanelUser?> GetByLoginIdentityAsync(string providerId, string providerUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<PanelUser?>(null);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PanelUser>>([]);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(Guid panelUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<UserLoginIdentity>>([]);

        /// <inheritdoc/>
        public ValueTask<PanelUser> CreateUserAsync(PanelUser user, UserLoginIdentity? initialIdentity = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(user);

        /// <inheritdoc/>
        public ValueTask<PanelUser> UpdateUserAsync(PanelUser user, CancellationToken cancellationToken = default) => ValueTask.FromResult(user);

        /// <inheritdoc/>
        public ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(UserLoginIdentity identity, CancellationToken cancellationToken = default) => ValueTask.FromResult(identity);

        /// <inheritdoc/>
        public ValueTask<bool> UnlinkLoginIdentityAsync(Guid panelUserId, string providerId, string providerUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    /// <summary>
    /// Stub implementation of <see cref="AntiforgeryStateProvider"/> for bUnit component tests.
    /// </summary>
    private sealed class StubAntiforgeryStateProvider : AntiforgeryStateProvider
    {
        /// <inheritdoc/>
        public override AntiforgeryRequestToken? GetAntiforgeryToken() =>
            new("test-antiforgery-token", "__RequestVerificationToken");
    }

    /// <summary>
    /// Minimal <see cref="IPlatformOAuthProvider"/> stub for multi-platform setup dropdown tests.
    /// </summary>
    /// <param name="platformId">The platform identifier.</param>
    private sealed class StubPlatformOAuthProvider(string platformId) : IPlatformOAuthProvider
    {
        /// <inheritdoc/>
        public string PlatformId { get; } = platformId;

        /// <inheritdoc/>
        public IReadOnlyList<string> DefaultLoginScopes => ["openid"];

        /// <inheritdoc/>
        public IReadOnlyList<string> DefaultBroadcasterScopes => ["openid"];

        /// <inheritdoc/>
        public IReadOnlyList<string> DefaultBotScopes => ["openid"];

        /// <inheritdoc/>
        public Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IReadOnlyCollection<string>? scopes, string state, bool forceVerify = false, string? nonce = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, Uri redirectUri, string? expectedNonce = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask RevokeTokenAsync(string clientId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// Minimal <see cref="IInstanceOAuthConfigService"/> stub for root recovery optional-label tests.
    /// </summary>
    /// <param name="stored">Pre-configured OAuth credentials.</param>
    private sealed class StubInstanceOAuthConfigService(
        IReadOnlyDictionary<string, PlatformOAuthAppCredentials> stored) : IInstanceOAuthConfigService
    {
        /// <inheritdoc/>
        public ValueTask<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(stored);

        /// <inheritdoc/>
        public ValueTask<PlatformOAuthAppCredentials?> GetAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(stored.GetValueOrDefault(platformId));

        /// <inheritdoc/>
        public ValueTask SetAsync(string platformId, PlatformOAuthAppCredentials credentials, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    /// <summary>
    /// Manages isolated temporary host directories for each <see cref="WebTestApplicationFactory"/> test run.
    /// </summary>
    private sealed class TempDaemonDirectories : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TempDaemonDirectories"/> class.
        /// </summary>
        public TempDaemonDirectories()
        {
            this.Root = Path.Combine(Path.GetTempPath(), $"sa-web-test-{Guid.NewGuid():N}");
            this.ConfigDir = Path.Combine(this.Root, "config");
            this.DataDir = Path.Combine(this.Root, "data");
            this.LogDir = Path.Combine(this.Root, "logs");
            this.PluginsDir = Path.Combine(this.Root, "plugins");
            this.BackupDir = Path.Combine(this.Root, "backups");
        }

        /// <summary>
        /// Gets the root temporary directory path.
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// Gets the configuration directory path.
        /// </summary>
        public string ConfigDir { get; }

        /// <summary>
        /// Gets the data directory path.
        /// </summary>
        public string DataDir { get; }

        /// <summary>
        /// Gets the log directory path.
        /// </summary>
        public string LogDir { get; }

        /// <summary>
        /// Gets the plugins directory path.
        /// </summary>
        public string PluginsDir { get; }

        /// <summary>
        /// Gets the backup directory path.
        /// </summary>
        public string BackupDir { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Directory.Exists(this.Root))
            {
                try
                {
                    Directory.Delete(this.Root, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    /// <summary>
    /// Configures <see cref="WebApplicationFactory{TEntryPoint}"/> with isolated temporary storage and web UI enabled.
    /// </summary>
    /// <param name="dirs">The isolated temporary directories for the test host.</param>
    private sealed class WebTestApplicationFactory(TempDaemonDirectories dirs) : WebApplicationFactory<Program>
    {
        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _ = builder.UseEnvironment("Testing");
            _ = builder.UseSetting("NoWeb", "false");
            _ = builder.UseSetting("Bootstrap:Paths:ConfigDir", dirs.ConfigDir);
            _ = builder.UseSetting("Bootstrap:Paths:DataDir", dirs.DataDir);
            _ = builder.UseSetting("Bootstrap:Paths:LogDir", dirs.LogDir);
            _ = builder.UseSetting("Bootstrap:Paths:PluginsDir", dirs.PluginsDir);
            _ = builder.UseSetting("Bootstrap:Paths:BackupDir", dirs.BackupDir);
            _ = builder.UseSetting("Bootstrap:Node:InstanceId", $"web-test-{Guid.NewGuid():N}");
            _ = builder.UseSetting("Bootstrap:Sentry:Enabled", "false");
        }
    }
}
