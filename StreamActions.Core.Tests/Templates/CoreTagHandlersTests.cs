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
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Templates;
using StreamActions.Core.Templates.Handlers;
using System.Globalization;
using Xunit;

namespace StreamActions.Core.Tests.Templates;

/// <summary>
/// Unit tests for all universal Core template tag handlers.
/// </summary>
public sealed class CoreTagHandlersTests
{
    #region ArgumentsAndIdentity

    [Fact]
    [Trait("Member", "ArgumentsAndIdentity")]
    public async Task PositionalAndIdentityTagsResolveExpectedValues()
    {
        ChatCommand cmd = TemplateTestHarness.CreateChatCommand(
            trigger: "greet",
            arguments: ["@Bob", "two", "three"],
            rawArguments: "@Bob two three",
            senderUsername: "alice_login",
            senderDisplayName: "AliceDisplay",
            channelName: "streamer_chan");
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(cmd);

        _ = (await EvaluateAsync("{1}", ctx)).Value.Should().Be("@Bob");
        _ = (await EvaluateAsync("{2}", ctx)).Value.Should().Be("two");
        _ = (await EvaluateAsync("{9}", ctx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{args}", ctx)).Value.Should().Be("@Bob two three");
        _ = (await EvaluateAsync("{args 2}", ctx)).Value.Should().Be("two three");
        _ = (await EvaluateAsync("{args 5}", ctx)).IsComplete.Should().BeFalse();

        _ = (await EvaluateAsync("{sender}", ctx)).Value.Should().Be("AliceDisplay");
        _ = (await EvaluateAsync("{@sender}", ctx)).Value.Should().Be("@AliceDisplay");
        _ = (await EvaluateAsync("{bare_sender}", ctx)).Value.Should().Be("alice_login");
        _ = (await EvaluateAsync("{sender_id}", ctx)).Value.Should().Be("user-42");
        _ = (await EvaluateAsync("{channel}", ctx)).Value.Should().Be("streamer_chan");
        _ = (await EvaluateAsync("{target}", ctx)).Value.Should().Be("Bob");
        _ = (await EvaluateAsync("{clean_user \"  @Carol \"}", ctx)).Value.Should().Be("Carol");
        _ = (await EvaluateAsync("{point_to_user}", ctx)).Value.Should().Be("@Bob");

        TagEvaluationContext noArgCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(arguments: [], senderDisplayName: "AliceDisplay"));
        _ = (await EvaluateAsync("{target}", noArgCtx)).Value.Should().Be("AliceDisplay");
        _ = (await EvaluateAsync("{point_to_user}", noArgCtx)).Value.Should().Be("@AliceDisplay");
    }

    #endregion

    #region MathAndStrings

    [Theory]
    [InlineData("{math add 2 3 4}", "9")]
    [InlineData("{math sub 10 3}", "7")]
    [InlineData("{math mul 3 4}", "12")]
    [InlineData("{math div 9 2}", "4.5")]
    [InlineData("{math mod 10 3}", "1")]
    [InlineData("{math pow 2 5}", "32")]
    [InlineData("{math abs -4.5}", "4.5")]
    [InlineData("{math ceil 4.1}", "5")]
    [InlineData("{math floor 4.9}", "4")]
    [InlineData("{math round 3.14159 2}", "3.14")]
    [InlineData("{math min 8 3 5}", "3")]
    [InlineData("{math max 8 3 5}", "8")]
    [Trait("Member", "MathAndStrings")]
    public async Task MathTagValidOperationsEvaluateWithInvariantCulture(string template, string expected)
    {
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext();
        TagResult result = await EvaluateAsync(template, ctx);

        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().Be(expected);
    }

    [Fact]
    [Trait("Member", "MathAndStrings")]
    public async Task MathAndStringTagsEdgeCasesAndFormattingWorkCorrectly()
    {
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext();

        _ = (await EvaluateAsync("{math div 5 0}", ctx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{upper \"hello\"}", ctx)).Value.Should().Be("HELLO");
        _ = (await EvaluateAsync("{lower \"HeLLo\"}", ctx)).Value.Should().Be("hello");
        _ = (await EvaluateAsync("{replace \"a-b-a\" \"-\" \"_\"}", ctx)).Value.Should().Be("a_b_a");
        _ = (await EvaluateAsync("{url_encode \"a b&c=1\"}", ctx)).Value.Should().Be("a%20b%26c%3D1");
        _ = (await EvaluateAsync("{repeat 3 \"ha\"}", ctx)).Value.Should().Be("hahaha");
        _ = (await EvaluateAsync("{repeat 500 \"x\"}", ctx)).Value.Should().HaveLength(RepeatTagHandler.MaxRepeatCount);
        _ = (await EvaluateAsync("{nl}", ctx)).Value.Should().Be("\n");
    }

    #endregion

    #region RandomAndTime

    [Fact]
    [Trait("Member", "RandomAndTime")]
    public async Task RandomTagsGenerateValidOutputs()
    {
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext();

        TagResult choice = await EvaluateAsync("{random \"red\" \"blue\"}", ctx);
        _ = choice.Value.Should().BeOneOf("red", "blue");

        TagResult choiceAlias = await EvaluateAsync("{random_choice \"yes\" \"no\"}", ctx);
        _ = choiceAlias.Value.Should().BeOneOf("yes", "no");

        TagResult randInt = await EvaluateAsync("{random_int 10 20}", ctx);
        int parsed = int.Parse(randInt.Value, CultureInfo.InvariantCulture);
        _ = parsed.Should().BeInRange(10, 20);

        TagResult randCode = await EvaluateAsync("{random_code 12}", ctx);
        _ = randCode.Value.Should().HaveLength(12);
        _ = randCode.Value.Should().MatchRegex("^[A-Za-z0-9]{12}$");
    }

    [Theory]
    [InlineData("UTC", "2026-07-15 14:30:00")]
    [InlineData("America/New_York", "2026-07-15 10:30:00")]
    [InlineData("Europe/London", "2026-07-15 15:30:00")]
    [InlineData("Asia/Tokyo", "2026-07-15 23:30:00")]
    [Trait("Member", "RandomAndTime")]
    public async Task TimeTagsAcrossTimezonesWithFakeTimeProviderAreDeterministic(string timeZoneId, string expectedTime)
    {
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 7, 15, 14, 30, 0, TimeSpan.Zero));
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(timeProvider: fakeTime);

        TagResult timeRes = await EvaluateAsync($"{{time \"{timeZoneId}\"}}", ctx);
        TagResult countdownRes = await EvaluateAsync("{countdown \"2026-07-16T15:31:05Z\"}", ctx);
        TagResult countupRes = await EvaluateAsync("{countup \"2026-07-15T12:25:00Z\"}", ctx);

        _ = timeRes.IsComplete.Should().BeTrue();
        _ = timeRes.Value.Should().Be(expectedTime);
        _ = countdownRes.Value.Should().Be("1d 1h 1m 5s");
        _ = countupRes.Value.Should().Be("2h 5m 0s");
    }

    #endregion

    #region VarCancelAndKv

    [Fact]
    [Trait("Member", "VarCancelAndKv")]
    public async Task VarCancelAndKvTagsManageStateAndShortCircuit()
    {
        TemplateTestHarness.InMemoryCounterStore counterStore = new();
        TemplateTestHarness.InMemoryKeyValueStore kvStore = new();
        TagEvaluationDependencies deps = new()
        {
            CounterStore = counterStore,
            KeyValueStore = kvStore,
        };
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(dependencies: deps);

        TagResult varFlow = await EvaluateAsync("{var greeting \"Hi\"}Say: {var greeting} {sender}", ctx);
        _ = varFlow.Value.Should().Be("Say: Hi Alice");

        TagResult kvSetAndGet = await EvaluateAsync("{kv_set quotes all q1 \"Stay hydrated\"}{kv_get quotes all q1} | {kv_get quotes all missing \"none\"} | {kv_rand quotes all}", ctx);
        _ = kvSetAndGet.Value.Should().Be("Stay hydrated | none | Stay hydrated");

        TagResult canceled = await EvaluateAsync("{cancel \"not allowed\"}{count \"+1\" \"deaths\"}", ctx);
        _ = canceled.IsComplete.Should().BeFalse();
        _ = ctx.IsCanceled.Should().BeTrue();
        _ = ctx.CancelReason.Should().Be("not allowed");
        _ = counterStore.MutationCount.Should().Be(0);
    }

    #endregion

    #region ApiTag

    [Fact]
    [Trait("Member", "ApiTag")]
    public async Task ApiTagGetPostJsonExtractionStatusMappingAndMemoizationWorkAsExpected()
    {
        TemplateTestHarness.StubSafeExternalHttpClient httpClient = new(req =>
            req.Method == HttpMethod.Get && req.Url.Contains("stats", StringComparison.Ordinal)
                ? CreateJsonResponse(200, "{\"user\":{\"name\":\"Alice\",\"scores\":[10,25]}}", req.Url)
                : req.Method == HttpMethod.Post
                ? CreateJsonResponse(201, "created", req.Url)
                : req.Url.Contains("missing", StringComparison.Ordinal)
                ? CreateJsonResponse(404, "not found", req.Url)
                : req.Url.Contains("boom", StringComparison.Ordinal)
                ? CreateJsonResponse(503, "unavailable", req.Url)
                : throw new SafeExternalHttpException(SafeExternalHttpFailureReason.BlockedIpAddress, "blocked"));

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { SafeExternalHttpClient = httpClient });

        // Two GET requests to identical URL/headers should memoize into 1 HTTP call
        TagResult memoizedGet = await EvaluateAsync(
            "{api https://example.com/stats json=\"user.name\"}:{api GET https://example.com/stats json=\"user.scores[1]\"}",
            ctx);
        _ = memoizedGet.IsComplete.Should().BeTrue();
        _ = memoizedGet.Value.Should().Be("Alice:25");
        _ = httpClient.Requests.Should().HaveCount(1);

        // POST with custom headers and JSON body is not memoized
        TagResult postResult = await EvaluateAsync(
            "{api POST https://example.com/submit \"Authorization: Bearer $secret.TOKEN\" body=\"\\{\\\"ok\\\":true\\}\" 201->Created}",
            ctx);
        _ = postResult.Value.Should().Be("Created");
        _ = httpClient.Requests.Should().HaveCount(2);
        _ = httpClient.Requests[1].Method.Should().Be(HttpMethod.Post);
        _ = httpClient.Requests[1].Headers.Should().ContainKey("Authorization");
        _ = httpClient.Requests[1].Body.Should().Be("{\"ok\":true}");
        _ = httpClient.Requests[1].ContentType.Should().Be("application/json");

        // Status mappings: 404->, 5xx->, !->
        _ = (await EvaluateAsync("{api https://example.com/missing 200->OK 404->Missing 5xx->Down !->Err}", ctx)).Value.Should().Be("Missing");
        _ = (await EvaluateAsync("{api https://example.com/boom 200->OK 404->Missing 5xx->Down !->Err}", ctx)).Value.Should().Be("Down");
        _ = (await EvaluateAsync("{api https://127.0.0.1/blocked !->Fallback}", ctx)).Value.Should().Be("Fallback");
    }

    [Fact]
    [Trait("Member", "ApiTag")]
    public async Task ApiTagBareArrowMultiPathJsonInterpolationAndMultipleJsonArgsWorkAsExpected()
    {
        TemplateTestHarness.StubSafeExternalHttpClient httpClient = new(req =>
            req.Url.Contains("weather", StringComparison.Ordinal)
                ? CreateJsonResponse(200, "{\"location\":{\"name\":\"Seattle\"},\"current\":{\"temp_f\":72,\"condition\":\"Sunny\"}}", req.Url)
                : req.Url.Contains("created", StringComparison.Ordinal)
                ? CreateJsonResponse(201, "{\"item\":{\"id\":42,\"ok\":true}}", req.Url)
                : req.Url.Contains("missing", StringComparison.Ordinal)
                ? CreateJsonResponse(404, "not found", req.Url)
                : req.Url.Contains("error", StringComparison.Ordinal)
                ? CreateJsonResponse(500, "server error", req.Url)
                : throw new SafeExternalHttpException(SafeExternalHttpFailureReason.RequestFailed, "network down"));

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { SafeExternalHttpClient = httpClient });

        // PhantomBot-style bare -> multi-path interpolation + 404-> + !->
        const string weatherTemplate = "{api https://example.com/weather \"->Weather in \\{location.name\\}: \\{current.temp_f\\} F\" 404->\"City not found\" !->\"Service error\"}";
        TagResult weatherOk = await EvaluateAsync(weatherTemplate, ctx);
        _ = weatherOk.IsComplete.Should().BeTrue();
        _ = weatherOk.Value.Should().Be("Weather in Seattle: 72 F");

        TagResult weather404 = await EvaluateAsync(
            "{api https://example.com/missing \"->Weather in \\{location.name\\}: \\{current.temp_f\\} F\" 404->\"City not found\" !->\"Service error\"}",
            ctx);
        _ = weather404.IsComplete.Should().BeTrue();
        _ = weather404.Value.Should().Be("City not found");

        TagResult weather500 = await EvaluateAsync(
            "{api https://example.com/error \"->Weather in \\{location.name\\}: \\{current.temp_f\\} F\" 404->\"City not found\" !->\"Service error\"}",
            ctx);
        _ = weather500.IsComplete.Should().BeTrue();
        _ = weather500.Value.Should().Be("Service error");

        TagResult weatherNetErr = await EvaluateAsync(
            "{api https://example.com/down \"->Weather in \\{location.name\\}: \\{current.temp_f\\} F\" 404->\"City not found\" !->\"Service error\"}",
            ctx);
        _ = weatherNetErr.IsComplete.Should().BeTrue();
        _ = weatherNetErr.Value.Should().Be("Service error");

        // 2xx JSON placeholder interpolation
        TagResult created2xx = await EvaluateAsync(
            "{api https://example.com/created \"2xx->Created #\\{item.id\\} (ok=\\{item.ok\\})\"}",
            ctx);
        _ = created2xx.IsComplete.Should().BeTrue();
        _ = created2xx.Value.Should().Be("Created #42 (ok=true)");

        // Missing JSON path fallback to !-> and Incomplete when no !-> is provided
        TagResult missingWithFallback = await EvaluateAsync(
            "{api https://example.com/weather \"->Temp: \\{current.humidity\\}\" !->\"Missing field\"}",
            ctx);
        _ = missingWithFallback.IsComplete.Should().BeTrue();
        _ = missingWithFallback.Value.Should().Be("Missing field");

        TagResult missingNoFallback = await EvaluateAsync(
            "{api https://example.com/weather \"->Temp: \\{current.humidity\\}\"}",
            ctx);
        _ = missingNoFallback.IsComplete.Should().BeFalse();

        // Multiple json= args space-joined without branch
        TagResult multiJsonJoined = await EvaluateAsync(
            "{api https://example.com/weather json=\"location.name\" json=\"current.temp_f\"}",
            ctx);
        _ = multiJsonJoined.IsComplete.Should().BeTrue();
        _ = multiJsonJoined.Value.Should().Be("Seattle 72");

        // Multiple json= args indexed {0} / {1} inside bare ->
        TagResult multiJsonIndexed = await EvaluateAsync(
            "{api https://example.com/weather json=\"location.name\" json=\"current.temp_f\" \"->\\{0\\}: \\{1\\}F (\\{current.condition\\})\"}",
            ctx);
        _ = multiJsonIndexed.IsComplete.Should().BeTrue();
        _ = multiJsonIndexed.Value.Should().Be("Seattle: 72F (Sunny)");

        // Multiple json= args with a missing path falls back to !-> or Incomplete
        TagResult multiJsonMissing = await EvaluateAsync(
            "{api https://example.com/weather json=\"location.name\" json=\"current.missing\" !->\"Bad path\"}",
            ctx);
        _ = multiJsonMissing.IsComplete.Should().BeTrue();
        _ = multiJsonMissing.Value.Should().Be("Bad path");
        _ = (await EvaluateAsync("{api https://example.com/weather json=\"location.name\" json=\"current.missing\"}", ctx)).IsComplete.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ApiTag")]
    public async Task ApiTagEvaluatesBranchesLazilyAndPreventsSecondOrderJsonInjection()
    {
        TemplateTestHarness.StubSafeExternalHttpClient httpClient = new(req =>
            req.Url.Contains("injection", StringComparison.Ordinal)
                ? CreateJsonResponse(200, "{\"name\":\"Alice\",\"payload\":\"{secret.key}\",\"secret\":{\"key\":\"LEAKED\"}}", req.Url)
                : req.Url.Contains("missing", StringComparison.Ordinal)
                ? CreateJsonResponse(404, "not found", req.Url)
                : CreateJsonResponse(200, "{\"location\":{\"name\":\"Seattle\"}}", req.Url));

        TemplateTestHarness.InMemoryCounterStore counterStore = new();
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies
            {
                SafeExternalHttpClient = httpClient,
                CounterStore = counterStore,
            });

        // Side-effect tag and missing {1} in unchosen 404-> / !-> branches must not execute or break the chosen 200 branch
        TagResult okBranch = await EvaluateAsync(
            "{api https://example.com/ok json=\"location.name\" 200->\"Found \\{0\\}\" 404->\"{count +1 errors} \\{1\\}\" !->\"{count +1 fallbacks} {nonexistent_tag}\"}",
            ctx);
        _ = okBranch.IsComplete.Should().BeTrue();
        _ = okBranch.Value.Should().Be("Found Seattle");
        _ = counterStore.MutationCount.Should().Be(0);

        // Now trigger 404: only 404-> branch runs (not !->)
        TagResult notFoundBranch = await EvaluateAsync(
            "{api https://example.com/missing 200->\"{count +1 ok_hits}\" 404->\"{count +1 errors}Missing\" !->\"{count +1 fallbacks}\"}",
            ctx);
        _ = notFoundBranch.IsComplete.Should().BeTrue();
        _ = notFoundBranch.Value.Should().Be("[Increased] 1Missing");
        _ = counterStore.MutationCount.Should().Be(1);

        // Second-order injection immunity:
        // 1) {1} resolves from JSON to "{secret.key}" and must remain literal "{secret.key}"
        // 2) A nested TagNode ({var injected}) returning "{secret.key}" must remain literal "{secret.key}"
        TagResult injectionSafe = await EvaluateAsync(
            "{var injected \"\\{secret.key\\}\"}{api https://example.com/injection json=\"name\" json=\"payload\" \"->\\{0\\} | \\{1\\} | {var injected}\"}",
            ctx);
        _ = injectionSafe.IsComplete.Should().BeTrue();
        _ = injectionSafe.Value.Should().Be("Alice | {secret.key} | {secret.key}");
    }

    #endregion

    #region JsonGetTag

    [Fact]
    [Trait("Member", "JsonGetTag")]
    public async Task ApiIntoVariableAndJsonGetTagSupportVariablesDirectJsonDefaultsAndFailures()
    {
        TemplateTestHarness.StubSafeExternalHttpClient httpClient = new(req =>
            req.Url.Contains("weather", StringComparison.Ordinal)
                ? CreateJsonResponse(200, "{\"location\":{\"name\":\"Seattle\"},\"current\":{\"temp_f\":72,\"tags\":[\"clear\",\"breezy\"]}}", req.Url)
                : req.Url.Contains("missing", StringComparison.Ordinal)
                ? CreateJsonResponse(404, "{\"error\":\"not found\"}", req.Url)
                : throw new SafeExternalHttpException(SafeExternalHttpFailureReason.RequestFailed, "network down"));

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { SafeExternalHttpClient = httpClient });

        // into="w" captures JSON quietly (returns empty string) and exposes it via {var w} and bare variable name w
        TagResult combined = await EvaluateAsync(
            "{api https://example.com/weather into=\"w\" 404->Missing !->Err}City={json_get {var w} location.name} Temp={json_get w current.temp_f} Tag={json_get w current.tags[1]} Fallback={json_get w current.unknown \"N/A\"}",
            ctx);
        _ = combined.IsComplete.Should().BeTrue();
        _ = combined.Value.Should().Be("City=Seattle Temp=72 Tag=breezy Fallback=N/A");
        _ = ctx.TryGetVariable("w", out string? storedJson).Should().BeTrue();
        _ = storedJson.Should().Contain("Seattle");

        // Direct JSON payload argument (object and array)
        TagResult directObj = await EvaluateAsync("{json_get \"\\{\\\"user\\\":\\{\\\"id\\\":99\\}\\}\" \"user.id\"}", ctx);
        _ = directObj.IsComplete.Should().BeTrue();
        _ = directObj.Value.Should().Be("99");

        TagResult directArr = await EvaluateAsync("{json_get \"[10,20,30]\" \"[1]\"}", ctx);
        _ = directArr.IsComplete.Should().BeTrue();
        _ = directArr.Value.Should().Be("20");

        // Missing path without default returns Incomplete
        TagResult missingNoDefault = await EvaluateAsync("{json_get w \"current.unknown\"}", ctx);
        _ = missingNoDefault.IsComplete.Should().BeFalse();

        // into="errVar" must NOT set errVar on 404 or network failure while returning 404-> / !->
        TagEvaluationContext errCtx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { SafeExternalHttpClient = httpClient });

        TagResult notFoundResult = await EvaluateAsync(
            "{api https://example.com/missing into=\"errVar\" 404->Missing !->Err}",
            errCtx);
        _ = notFoundResult.IsComplete.Should().BeTrue();
        _ = notFoundResult.Value.Should().Be("Missing");
        _ = errCtx.TryGetVariable("errVar", out _).Should().BeFalse();

        TagResult netErrResult = await EvaluateAsync(
            "{api https://example.com/down into=\"errVar\" 404->Missing !->Err}",
            errCtx);
        _ = netErrResult.IsComplete.Should().BeTrue();
        _ = netErrResult.Value.Should().Be("Err");
        _ = errCtx.TryGetVariable("errVar", out _).Should().BeFalse();

        // into="w2" combined with -> still sets variable AND outputs formatted branch
        TagResult intoWithBranch = await EvaluateAsync(
            "{api https://example.com/weather into=\"w2\" \"->Hi \\{location.name\\}\"}",
            errCtx);
        _ = intoWithBranch.IsComplete.Should().BeTrue();
        _ = intoWithBranch.Value.Should().Be("Hi Seattle");
        _ = errCtx.TryGetVariable("w2", out string? w2Value).Should().BeTrue();
        _ = w2Value.Should().Contain("Seattle");
    }

    #endregion

    /// <summary>
    /// Creates a stub <see cref="SafeExternalHttpResponse"/> for unit tests.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body.</param>
    /// <param name="url">The requested URL string.</param>
    /// <returns>A populated <see cref="SafeExternalHttpResponse"/>.</returns>
    private static SafeExternalHttpResponse CreateJsonResponse(int statusCode, string body, string url) =>
        new(
            statusCode,
            body,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            new Uri(url));

    /// <summary>
    /// Helper to parse and evaluate a template string against <paramref name="context"/>.
    /// </summary>
    /// <param name="template">The raw template string.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    private static ValueTask<TagResult> EvaluateAsync(string template, TagEvaluationContext context)
    {
        TemplateParseResult parsed = TemplateParser.Parse(template, context.TagRegistry, context.PlatformId);
        return TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: TestContext.Current.CancellationToken);
    }
}
