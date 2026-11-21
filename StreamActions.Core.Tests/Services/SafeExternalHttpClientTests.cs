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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Http;
using StreamActions.Core.Models;
using StreamActions.Core.Services;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class SafeExternalHttpClientTests
{
    #region SafeExternalIpAddressValidator

    [Theory]
    [Trait("Member", "IsAllowed")]
    [InlineData("127.0.0.1", false)]
    [InlineData("127.255.255.254", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("0.1.2.3", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.254", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("100.127.255.254", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("255.255.255.255", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("192.0.0.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("198.51.100.1", false)]
    [InlineData("203.0.113.1", false)]
    [InlineData("::1", false)]
    [InlineData("::", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("::ffff:169.254.169.254", false)]
    [InlineData("::127.0.0.1", false)]
    [InlineData("::93.184.216.34", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fd12:3456::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("2001::1", false)]
    [InlineData("2002:7f00:1::1", false)]
    [InlineData("64:ff9b::127.0.0.1", false)]
    [InlineData("64:ff9b:1::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("93.184.216.34", true)]
    [InlineData("8.8.8.8", true)]
    [InlineData("172.32.0.1", true)]
    [InlineData("192.169.0.1", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("2001:4860:4860::8888", true)]
    public void SafeExternalIpAddressValidatorIsAllowedClassifiesAddressesCorrectly(string ipString, bool expectedAllowed)
    {
        IPAddress address = IPAddress.Parse(ipString);

        bool actual = SafeExternalIpAddressValidator.IsAllowed(address);

        _ = actual.Should().Be(expectedAllowed);
    }

    #endregion

    #region SendAsync

    [Theory]
    [Trait("Member", "SendAsync")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Test verifies raw URL scheme validation.")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/resource")]
    [InlineData("ws://example.com/socket")]
    [InlineData("wss://example.com/socket")]
    [InlineData("gopher://example.com/1")]
    public async Task SendAsyncDisallowedSchemeThrowsDisallowedScheme(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SafeExternalHttpClient client = CreateTestClient(kestrelPort: 1);

        Func<Task> act = async () => await client.SendAsync(new(url), ct).ConfigureAwait(false);

        SafeExternalHttpException ex = (await act.Should().ThrowAsync<SafeExternalHttpException>()).Which;
        _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.DisallowedScheme);
    }

    [Theory]
    [Trait("Member", "SendAsync")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Test verifies raw URL SSRF blocking.")]
    [InlineData("http://127.0.0.1:8080/test")]
    [InlineData("http://localhost:8080/test")]
    [InlineData("http://sub.localhost:8080/test")]
    [InlineData("http://[::1]:8080/test")]
    [InlineData("http://[::ffff:127.0.0.1]:8080/test")]
    [InlineData("http://10.0.0.1/test")]
    [InlineData("http://172.16.0.1/test")]
    [InlineData("http://192.168.1.1/test")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    public async Task SendAsyncBlockedLiteralOrLocalhostTargetThrowsBlockedIpAddress(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SafeExternalHttpClient client = CreateTestClient(kestrelPort: 1);

        Func<Task> act = async () => await client.SendAsync(new(url), ct).ConfigureAwait(false);

        SafeExternalHttpException ex = (await act.Should().ThrowAsync<SafeExternalHttpException>()).Which;
        _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.BlockedIpAddress);
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncDnsRebindingToPrivateIpThrowsBlockedIpAddressBeforeConnecting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int requestCount = 0;

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app => _ = app.MapGet("/rebind", () =>
            {
                _ = Interlocked.Increment(ref requestCount);
                return Results.Text("ok");
            }),
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            int dnsCalls = 0;
            using SafeExternalHttpClient client = CreateTestClient(
                server.Port,
                resolveHostAddressesAsync: (_, _) =>
                {
                    int call = Interlocked.Increment(ref dnsCalls);
                    return ValueTask.FromResult(call == 1
                        ? new[] { IPAddress.Parse("93.184.216.34") }
                        : [IPAddress.Parse("127.0.0.1")]);
                });

            SafeExternalHttpResponse first = await client.SendAsync(
                new("http://rebind-ok.example.com/rebind"),
                ct);
            _ = first.StatusCode.Should().Be(200);
            _ = requestCount.Should().Be(1);

            Func<Task> secondAct = async () => await client.SendAsync(
                new("http://rebind-evil.example.com/rebind"),
                ct).ConfigureAwait(false);

            SafeExternalHttpException ex = (await secondAct.Should().ThrowAsync<SafeExternalHttpException>()).Which;
            _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.BlockedIpAddress);
            _ = requestCount.Should().Be(1);
        }
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncAllowedMockExternalHostReachesEphemeralKestrelCleanly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app => _ = app.MapGet("/hello", (HttpContext ctx) =>
            {
                ctx.Response.Headers["X-Custom"] = "ok";
                return Results.Text("hello external world");
            }),
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            using SafeExternalHttpClient client = CreateTestClient(server.Port);
            SafeExternalHttpResponse response = await client.SendAsync(
                new("http://api.example.com/hello"),
                ct);

            _ = response.StatusCode.Should().Be(200);
            _ = response.IsSuccessStatusCode.Should().BeTrue();
            _ = response.Body.Should().Be("hello external world");
            _ = response.Headers.Should().ContainKey("X-Custom");
            _ = response.Headers["X-Custom"].Should().Contain("ok");
            _ = response.RedirectCount.Should().Be(0);
        }
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncRedirectsUpToThreeHopsSucceedFourthHopThrowsTooManyRedirects()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app =>
            {
                _ = app.MapGet("/hop-0", () => Results.Redirect("/hop-1"));
                _ = app.MapGet("/hop-1", () => Results.Redirect("/hop-2"));
                _ = app.MapGet("/hop-2", () => Results.Redirect("/hop-3"));
                _ = app.MapGet("/hop-3", () => Results.Text("reached-3"));
                _ = app.MapGet("/too-many-0", () => Results.Redirect("/hop-0"));
            },
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            using SafeExternalHttpClient client = CreateTestClient(server.Port);

            SafeExternalHttpResponse threeHops = await client.SendAsync(
                new("http://api.example.com/hop-0"),
                ct);
            _ = threeHops.StatusCode.Should().Be(200);
            _ = threeHops.Body.Should().Be("reached-3");
            _ = threeHops.RedirectCount.Should().Be(3);

            Func<Task> fourHopsAct = async () => await client.SendAsync(
                new("http://api.example.com/too-many-0"),
                ct).ConfigureAwait(false);

            SafeExternalHttpException ex = (await fourHopsAct.Should().ThrowAsync<SafeExternalHttpException>()).Which;
            _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.TooManyRedirects);
        }
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncRedirectMethodRewritesAndDisallowedTargetsEnforcedCorrectly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string? observed303Method = null;
        string? observed303Body = "uninitialized";
        string? observed307Method = null;
        string? observed307Body = null;

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app =>
            {
                _ = app.MapPost("/post-303", () => Results.Extensions.RedirectWithStatus("/after-303", 303));
                _ = app.MapMethods("/after-303", ["GET", "POST"], async (HttpContext ctx) =>
                {
                    observed303Method = ctx.Request.Method;
                    using StreamReader reader = new(ctx.Request.Body);
                    observed303Body = await reader.ReadToEndAsync(ctx.RequestAborted).ConfigureAwait(false);
                    return Results.Text("done-303");
                });

                _ = app.MapPost("/post-307", () => Results.Extensions.RedirectWithStatus("/after-307", 307));
                _ = app.MapMethods("/after-307", ["GET", "POST"], async (HttpContext ctx) =>
                {
                    observed307Method = ctx.Request.Method;
                    using StreamReader reader = new(ctx.Request.Body);
                    observed307Body = await reader.ReadToEndAsync(ctx.RequestAborted).ConfigureAwait(false);
                    return Results.Text("done-307");
                });

                _ = app.MapGet("/redirect-private", () => Results.Redirect("http://169.254.169.254/latest/meta-data/"));
                _ = app.MapGet("/redirect-file", () => Results.Redirect("file:///etc/passwd"));
            },
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            using SafeExternalHttpClient client = CreateTestClient(server.Port);

            SafeExternalHttpResponse res303 = await client.SendAsync(
                new("http://api.example.com/post-303", HttpMethod.Post, Body: "payload", ContentType: "text/plain"),
                ct);
            _ = res303.StatusCode.Should().Be(200);
            _ = observed303Method.Should().Be("GET");
            _ = observed303Body.Should().BeEmpty();

            SafeExternalHttpResponse res307 = await client.SendAsync(
                new("http://api.example.com/post-307", HttpMethod.Post, Body: "payload", ContentType: "text/plain"),
                ct);
            _ = res307.StatusCode.Should().Be(200);
            _ = observed307Method.Should().Be("POST");
            _ = observed307Body.Should().Be("payload");

            Func<Task> privateRedirect = async () => await client.SendAsync(new("http://api.example.com/redirect-private"), ct).ConfigureAwait(false);
            _ = (await privateRedirect.Should().ThrowAsync<SafeExternalHttpException>()).Which.Reason.Should().Be(SafeExternalHttpFailureReason.BlockedIpAddress);

            Func<Task> fileRedirect = async () => await client.SendAsync(new("http://api.example.com/redirect-file"), ct).ConfigureAwait(false);
            _ = (await fileRedirect.Should().ThrowAsync<SafeExternalHttpException>()).Which.Reason.Should().Be(SafeExternalHttpFailureReason.DisallowedScheme);
        }
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncCrossOriginRedirectWithSecretStripsCredentialsAndValidatesAllowedHostPatterns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.CreateVersion7();
        const string secretPlaintext = "sec+ret/token=42";
        string? observedAuth = "not-set";
        string? observedProxyAuth = "not-set";
        string? observedCookie = "not-set";
        string? observedCustomSecretHeader = "not-set";
        string? observedTraceHeader = null;
        string? observedQuery = null;

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app =>
            {
                _ = app.MapGet("/start", (HttpContext ctx) =>
                {
                    string escaped = Uri.EscapeDataString(secretPlaintext);
                    return Results.Redirect($"http://cdn.weather.com/target?keep=1&leaked={escaped}&token=$secret.api_key");
                });

                _ = app.MapGet("/target", (HttpContext ctx) =>
                {
                    observedAuth = ctx.Request.Headers.Authorization.FirstOrDefault();
                    observedProxyAuth = ctx.Request.Headers.ProxyAuthorization.FirstOrDefault();
                    observedCookie = ctx.Request.Headers.Cookie.FirstOrDefault();
                    observedCustomSecretHeader = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
                    observedTraceHeader = ctx.Request.Headers["X-Trace"].FirstOrDefault();
                    observedQuery = ctx.Request.QueryString.Value;
                    return Results.Text("redirected-ok");
                });

                _ = app.MapGet("/to-evil", () => Results.Redirect("http://evil.com/steal"));
            },
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            InMemoryStubSecretVault vault = new();
            await vault.SetSecretAsync(workspaceId, "api_key", secretPlaintext, ["*.weather.com"], cancellationToken: ct);
            SecretRedactor redactor = new();

            using SafeExternalHttpClient client = CreateTestClient(server.Port, vault, redactor);

            SafeExternalHttpResponse response = await client.SendAsync(
                new SafeExternalHttpRequest(
                    "http://api.weather.com/start?auth=$secret.api_key",
                    Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Authorization"] = "Bearer raw-auth-value",
                        ["Proxy-Authorization"] = "Basic abc",
                        ["Cookie"] = "session=xyz",
                        ["X-Api-Key"] = "$secret.api_key",
                        ["X-Trace"] = "trace-123",
                    },
                    WorkspaceId: workspaceId),
                ct);

            _ = response.StatusCode.Should().Be(200);
            _ = observedAuth.Should().BeNull();
            _ = observedProxyAuth.Should().BeNull();
            _ = observedCookie.Should().BeNull();
            _ = observedCustomSecretHeader.Should().BeNull();
            _ = observedTraceHeader.Should().Be("trace-123");
            _ = observedQuery.Should().Be("?keep=1");

            Func<Task> disallowedRedirect = async () => await client.SendAsync(
                new SafeExternalHttpRequest(
                    "http://api.weather.com/to-evil?k=$secret.api_key",
                    WorkspaceId: workspaceId),
                ct).ConfigureAwait(false);

            SafeExternalHttpException ex = (await disallowedRedirect.Should().ThrowAsync<SafeExternalHttpException>()).Which;
            _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.SecretHostNotAllowed);
        }
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncDecompressionBombAndOversizedPayloadAbortedAt64KiBLimit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] exact64KiB = new byte[65_536];
        Array.Fill(exact64KiB, (byte)'A');
        byte[] over64KiB = new byte[65_537];
        Array.Fill(over64KiB, (byte)'B');

        byte[] gzipBomb;
        using (MemoryStream compressed = new())
        {
            GZipStream gz = new(compressed, CompressionLevel.Optimal, leaveOpen: true);
            await using (gz.ConfigureAwait(true))
            {
                byte[] bombPayload = new byte[512 * 1024];
                Array.Fill(bombPayload, (byte)'Z');
                await gz.WriteAsync(bombPayload, ct).ConfigureAwait(true);
            }

            gzipBomb = compressed.ToArray();
        }

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app =>
            {
                _ = app.MapGet("/exact", () => Results.Bytes(exact64KiB, "text/plain"));
                _ = app.MapGet("/too-big-chunked", async ctx =>
                {
                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.Body.WriteAsync(over64KiB, ctx.RequestAborted).ConfigureAwait(false);
                });
                _ = app.MapGet("/gzip-bomb", (HttpContext ctx) =>
                {
                    ctx.Response.Headers.ContentEncoding = "gzip";
                    return Results.Bytes(gzipBomb, "text/plain");
                });
            },
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            using SafeExternalHttpClient client = CreateTestClient(server.Port);

            SafeExternalHttpResponse exactResponse = await client.SendAsync(new("http://api.example.com/exact"), ct);
            _ = exactResponse.Body.Length.Should().Be(65_536);

            Func<Task> chunkedAct = async () => await client.SendAsync(new("http://api.example.com/too-big-chunked"), ct).ConfigureAwait(false);
            _ = (await chunkedAct.Should().ThrowAsync<SafeExternalHttpException>()).Which.Reason.Should().Be(SafeExternalHttpFailureReason.ResponseTooLarge);

            Func<Task> bombAct = async () => await client.SendAsync(new("http://api.example.com/gzip-bomb"), ct).ConfigureAwait(false);
            _ = (await bombAct.Should().ThrowAsync<SafeExternalHttpException>()).Which.Reason.Should().Be(SafeExternalHttpFailureReason.ResponseTooLarge);
        }
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncExceedingTimeoutCancelsDeterministicallyWithFakeTimeProvider()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource serverEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app => _ = app.MapGet("/slow", async (HttpContext ctx) =>
            {
                _ = serverEntered.TrySetResult();
                TaskCompletionSource aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
                using CancellationTokenRegistration reg = ctx.RequestAborted.Register(() => _ = aborted.TrySetResult());
                await aborted.Task.ConfigureAwait(false);
                return Results.Text("never");
            }),
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
            using SafeExternalHttpClient client = CreateTestClient(server.Port, timeProvider: fakeTime);

            Task<SafeExternalHttpResponse> sendTask = client.SendAsync(
                new("http://api.example.com/slow"),
                ct).AsTask();

            await serverEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            fakeTime.Advance(TimeSpan.FromSeconds(5));

            Func<Task> act = async () => await sendTask.ConfigureAwait(false);
            SafeExternalHttpException ex = (await act.Should().ThrowAsync<SafeExternalHttpException>()).Which;
            _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.Timeout);
        }
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncSecretSubstitutionAndRedactionEnforcesAllInjectionAndScrubbingRules()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.CreateVersion7();
        const string secretValue = "sec+ret/val=99";
        string escapedSecret = Uri.EscapeDataString(secretValue);

        string? observedItemToken = null;
        string? observedPath = null;
        string? observedRawQuery = null;
        string? observedHeader = null;
        string? observedBody = null;

        EphemeralKestrelServer server = await EphemeralKestrelServer.StartAsync(
            app => _ = app.MapPost("/items/{itemToken}", async (string itemToken, HttpContext ctx) =>
            {
                observedItemToken = itemToken;
                observedPath = ctx.Request.Path.Value;
                observedRawQuery = ctx.Request.QueryString.Value;
                observedHeader = ctx.Request.Headers["X-Secret-Header"].FirstOrDefault();
                using StreamReader reader = new(ctx.Request.Body);
                observedBody = await reader.ReadToEndAsync(ctx.RequestAborted).ConfigureAwait(false);

                ctx.Response.Headers["X-Echo-Secret"] = $"hdr={secretValue}";
                return Results.Text($"body={secretValue}&escaped={escapedSecret}");
            }),
            ct).ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            InMemoryStubSecretVault vault = new();
            await vault.SetSecretAsync(workspaceId, "api_key", secretValue, ["api.example.com"], cancellationToken: ct);
            SecretRedactor redactor = new();

            using SafeExternalHttpClient client = CreateTestClient(server.Port, vault, redactor);

            SafeExternalHttpResponse response = await client.SendAsync(
                new SafeExternalHttpRequest(
                    "http://api.example.com/items/$secret.API_KEY?key=$secret.api_key",
                    HttpMethod.Post,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Secret-Header"] = "Bearer $secret.API_KEY",
                    },
                    Body: "{\"literal\":\"$secret.API_KEY\"}",
                    ContentType: "application/json",
                    WorkspaceId: workspaceId),
                ct);

            _ = Uri.UnescapeDataString(observedItemToken!).Should().Be(secretValue);
            _ = observedPath.Should().Be("/items/sec+ret%2Fval=99");
            _ = observedRawQuery.Should().Be($"?key={escapedSecret}");
            _ = observedHeader.Should().Be($"Bearer {secretValue}");
            _ = observedBody.Should().Be("{\"literal\":\"$secret.API_KEY\"}");

            _ = response.Body.Should().NotContain(secretValue);
            _ = response.Body.Should().NotContain(escapedSecret);
            _ = response.Body.Should().Contain("[REDACTED]");
            _ = response.Headers["X-Echo-Secret"].Should().Contain("hdr=[REDACTED]");
            _ = response.FinalUri.AbsoluteUri.Should().NotContain(secretValue);
            _ = response.FinalUri.AbsoluteUri.Should().NotContain(escapedSecret);
        }
    }

    [Theory]
    [Trait("Member", "SendAsync")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Test verifies invalid authority syntax rejection.")]
    [InlineData("https://$secret.api_key@api.example.com/v1")]
    [InlineData("https://user:$secret.api_key@api.example.com/v1")]
    [InlineData("https://$secret.api_key.example.com/v1")]
    [InlineData("https://api.example.com:$secret.port/v1")]
    [InlineData("https://%24secret.api_key@api.example.com/v1")]
    public async Task SendAsyncSecretInAuthorityThrowsSecretInAuthority(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SafeExternalHttpClient client = CreateTestClient(kestrelPort: 1);

        Func<Task> act = async () => await client.SendAsync(new(url, WorkspaceId: Guid.CreateVersion7()), ct).ConfigureAwait(false);

        SafeExternalHttpException ex = (await act.Should().ThrowAsync<SafeExternalHttpException>()).Which;
        _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.SecretInAuthority);
    }

    [Fact]
    [Trait("Member", "SendAsync")]
    public async Task SendAsyncTransportHttpRequestExceptionRedactsSecretFromExceptionMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.CreateVersion7();
        const string secretValue = "super_transport_secret_12345";
        string escapedSecret = Uri.EscapeDataString(secretValue);

        using TcpListener rawListener = new(IPAddress.Loopback, 0);
        rawListener.Start();
        int port = ((IPEndPoint)rawListener.LocalEndpoint).Port;

        Task acceptTask = Task.Run(async () =>
        {
            using TcpClient tcpClient = await rawListener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            NetworkStream stream = tcpClient.GetStream();
            await using (stream.ConfigureAwait(false))
            {
                byte[] malformed = Encoding.ASCII.GetBytes("NOT_VALID_HTTP\r\n\r\n");
                await stream.WriteAsync(malformed, ct).ConfigureAwait(false);
            }
        }, ct);

        InMemoryStubSecretVault vault = new();
        await vault.SetSecretAsync(workspaceId, "api_key", secretValue, ["api.example.com"], cancellationToken: ct);
        SecretRedactor redactor = new();

        using SafeExternalHttpClient client = CreateTestClient(port, vault, redactor);

        Func<Task> act = async () => await client.SendAsync(
            new SafeExternalHttpRequest(
                "http://api.example.com/endpoint?key=$secret.api_key",
                WorkspaceId: workspaceId),
            ct).ConfigureAwait(false);

        SafeExternalHttpException ex = (await act.Should().ThrowAsync<SafeExternalHttpException>()).Which;
        _ = ex.Reason.Should().Be(SafeExternalHttpFailureReason.RequestFailed);
        _ = ex.Message.Should().NotContain(secretValue);
        _ = ex.Message.Should().NotContain(escapedSecret);
        _ = ex.ToString().Should().NotContain(secretValue);
        _ = ex.ToString().Should().NotContain(escapedSecret);

        await acceptTask.ConfigureAwait(true);
    }

    #endregion

    #region LocalizerAndDi

    [Fact]
    [Trait("Member", "GetStringAsync")]
    public async Task LocalizerDoesNotExpandSecretTokensKeepsLiteralTokenText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string tempDir = Path.Combine(Path.GetTempPath(), $"sa-i18n-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path.Combine(tempDir, "en-US"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "en-US", "core.json"),
                """{"literalSecret":"Use $secret.API_KEY in HTTP client only"}""",
                ct);

            ServiceCollection services = new();
            _ = services.AddLogging();
            _ = services.AddHybridCache();
            _ = services.Configure<LocalizationOptions>(opts => opts.LocalesDirectory = tempDir);
            _ = services.AddSingleton<IStreamActionsLocalizer, StreamActionsLocalizer>();

            using ServiceProvider provider = services.BuildServiceProvider();
            IStreamActionsLocalizer localizer = provider.GetRequiredService<IStreamActionsLocalizer>();

            string? resolved = await localizer.GetStringAsync(Guid.CreateVersion7(), "core", "literalSecret", cancellationToken: ct);
            _ = resolved.Should().Be("Use $secret.API_KEY in HTTP client only");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Member", "AddStreamActionsCore")]
    public void AddStreamActionsCoreRegistersISafeExternalHttpClientAsSingleton()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddStreamActionsCore(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        ISafeExternalHttpClient first = provider.GetRequiredService<ISafeExternalHttpClient>();
        ISafeExternalHttpClient second = provider.GetRequiredService<ISafeExternalHttpClient>();

        _ = first.Should().BeOfType<SafeExternalHttpClient>();
        _ = second.Should().BeSameAs(first);
    }

    #endregion

    /// <summary>
    /// Creates a test <see cref="SafeExternalHttpClient"/> whose mocked DNS returns <c>93.184.216.34</c> and rewrites the validated endpoint to Kestrel.
    /// </summary>
    /// <param name="kestrelPort">The ephemeral Kestrel port.</param>
    /// <param name="secretVault">Optional secret vault instance.</param>
    /// <param name="secretRedactor">Optional secret redactor instance.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    /// <param name="resolveHostAddressesAsync">Optional custom DNS override.</param>
    /// <returns>A configured <see cref="SafeExternalHttpClient"/>.</returns>
    private static SafeExternalHttpClient CreateTestClient(
        int kestrelPort,
        ISecretVault? secretVault = null,
        ISecretRedactor? secretRedactor = null,
        TimeProvider? timeProvider = null,
        Func<string, CancellationToken, ValueTask<IPAddress[]>>? resolveHostAddressesAsync = null) =>
        new(
            secretRedactor ?? new SecretRedactor(),
            timeProvider ?? TimeProvider.System,
            NullLogger<SafeExternalHttpClient>.Instance,
            secretVault,
            resolveHostAddressesAsync ?? ((_, _) => ValueTask.FromResult(new[] { IPAddress.Parse("93.184.216.34") })),
            _ => new IPEndPoint(IPAddress.Loopback, kestrelPort));

    /// <summary>
    /// Minimal in-memory <see cref="ISecretVault"/> stub for Core unit tests.
    /// </summary>
    private sealed class InMemoryStubSecretVault : ISecretVault
    {
        /// <summary>
        /// Stored stub secret entries keyed by workspace and normalized secret name.
        /// </summary>
        private readonly ConcurrentDictionary<string, (string Plaintext, IReadOnlyList<string> Patterns)> _secrets = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Formats a case-insensitive composite key for stub secret lookup.
        /// </summary>
        /// <param name="workspaceId">The workspace identifier.</param>
        /// <param name="secretName">The secret name.</param>
        /// <returns>The composite lookup key.</returns>
        private static string FormatKey(Guid workspaceId, string secretName) => $"{workspaceId:N}:{secretName.Trim()}";

        /// <inheritdoc/>
        public ValueTask<string?> GetSecretValueAsync(Guid workspaceId, string secretName, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._secrets.TryGetValue(FormatKey(workspaceId, secretName), out var entry) ? entry.Plaintext : null);

        /// <inheritdoc/>
        public ValueTask SetSecretAsync(
            Guid workspaceId,
            string secretName,
            string plaintextValue,
            IReadOnlyCollection<string> allowedHostPatterns,
            DateTimeOffset? expiresAtUtc = null,
            CancellationToken cancellationToken = default)
        {
            List<string> normalized = [.. allowedHostPatterns.Select(HostPatternMatcher.NormalizeAndValidatePattern)];
            this._secrets[FormatKey(workspaceId, secretName)] = (plaintextValue, normalized);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask<bool> DeleteSecretAsync(Guid workspaceId, string secretName, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._secrets.TryRemove(FormatKey(workspaceId, secretName), out _));

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<WorkspaceSecretMetadata>> GetSecretsMetadataAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceSecretMetadata>>([]);

        /// <inheritdoc/>
        public ValueTask UpdateSecretPolicyAsync(
            Guid workspaceId,
            string secretName,
            IReadOnlyCollection<string> allowedHostPatterns,
            DateTimeOffset? expiresAtUtc = null,
            string? newPlaintextValue = null,
            bool broadcasterConfirmed = false,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask<SecretResolutionResult> ResolveForHostAsync(
            Guid workspaceId,
            string secretName,
            Uri targetUri,
            CancellationToken cancellationToken = default) => !this._secrets.TryGetValue(FormatKey(workspaceId, secretName), out var entry)
                ? ValueTask.FromResult(new SecretResolutionResult(SecretResolutionStatus.NotFound))
                : !HostPatternMatcher.IsHostAllowed(entry.Patterns, targetUri.IdnHost)
                ? ValueTask.FromResult(new SecretResolutionResult(SecretResolutionStatus.HostNotAllowed, AllowedHostPatterns: entry.Patterns))
                : ValueTask.FromResult(new SecretResolutionResult(SecretResolutionStatus.Resolved, entry.Plaintext, entry.Patterns));
    }

    /// <summary>
    /// Self-hosted ephemeral Kestrel server bound to <c>http://127.0.0.1:0</c> for offline HTTP tests.
    /// </summary>
    private sealed class EphemeralKestrelServer : IAsyncDisposable
    {
        /// <summary>
        /// The running Kestrel web application.
        /// </summary>
        private readonly WebApplication _app;

        /// <summary>
        /// Initializes a new instance of the <see cref="EphemeralKestrelServer"/> class.
        /// </summary>
        /// <param name="app">The running web application.</param>
        /// <param name="port">The bound TCP port.</param>
        private EphemeralKestrelServer(WebApplication app, int port)
        {
            this._app = app;
            this.Port = port;
        }

        /// <summary>
        /// Gets the dynamically assigned loopback port.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Configures and starts an ephemeral Kestrel test server.
        /// </summary>
        /// <param name="configureRoutes">Delegate registering test routes.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A running <see cref="EphemeralKestrelServer"/>.</returns>
        public static async Task<EphemeralKestrelServer> StartAsync(
            Action<WebApplication> configureRoutes,
            CancellationToken cancellationToken)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
            WebApplication app = builder.Build();
            configureRoutes(app);

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            IServerAddressesFeature addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            Uri boundUri = new(addresses.Addresses.Single(), UriKind.Absolute);
            return new(app, boundUri.Port);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await this._app.StopAsync().ConfigureAwait(false);
            await this._app.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Minimal <see cref="IResult"/> helper for emitting explicit 303/307 redirect responses in Kestrel tests.
/// </summary>
static file class TestRedirectResultExtensions
{
    /// <summary>
    /// Creates an HTTP redirect result with a custom 3xx status code.
    /// </summary>
    /// <param name="_">The results extensions marker.</param>
    /// <param name="location">The target location URI string.</param>
    /// <param name="statusCode">The 3xx status code.</param>
    /// <returns>An <see cref="IResult"/> writing the redirect status and <c>Location</c> header.</returns>
    public static IResult RedirectWithStatus(this IResultExtensions _, string location, int statusCode) =>
        new ExplicitRedirectResult(location, statusCode);

    /// <summary>
    /// Writes a redirect response with the specified status code and <c>Location</c> header.
    /// </summary>
    /// <param name="location">The redirect target location.</param>
    /// <param name="statusCode">The 3xx status code.</param>
    private sealed class ExplicitRedirectResult(string location, int statusCode) : IResult
    {
        /// <inheritdoc/>
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.Headers.Location = location;
            return Task.CompletedTask;
        }
    }
}
