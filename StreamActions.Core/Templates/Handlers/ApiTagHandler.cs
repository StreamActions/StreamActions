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

using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{api ...}</c> tag backed by <see cref="ISafeExternalHttpClient"/>, supporting HTTP methods, headers, request bodies, JSON path extraction, status mappings, and per-message GET memoization.
/// </summary>
public sealed class ApiTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "api";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        ISafeExternalHttpClient? httpClient = context.GetService<ISafeExternalHttpClient>();
        if (httpClient is null)
        {
            return TagResult.Incomplete;
        }

        List<string> evaluatedArgs = new(unevaluatedArgs.Count);
        Dictionary<int, TemplateNode> exactStatusMap = [];
        Dictionary<int, TemplateNode> classStatusMap = [];
        TemplateNode? successMapping = null;
        TemplateNode? catchAllMapping = null;

        foreach (TemplateNode arg in unevaluatedArgs)
        {
            if (TryExtractStatusMappingBranch(arg, out StatusMappingRule rule))
            {
                ApplyStatusMappingRule(rule, exactStatusMap, classStatusMap, ref successMapping, ref catchAllMapping);
                continue;
            }

            TagResult argResult = await arg.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!argResult.IsComplete || context.IsCanceled)
            {
                return TagResult.Incomplete;
            }

            evaluatedArgs.Add(argResult.Value);
        }

        if (!TryParseArguments(
            evaluatedArgs,
            exactStatusMap,
            classStatusMap,
            ref successMapping,
            ref catchAllMapping,
            out ApiRequestSpec spec))
        {
            return TagResult.Incomplete;
        }

        string? contentType = null;
        if (spec.Headers.TryGetValue("Content-Type", out string? explicitContentType))
        {
            contentType = explicitContentType;
        }
        else if (spec.Body is not null)
        {
            string trimmedBody = spec.Body.TrimStart();
            contentType = trimmedBody.StartsWith('{') || trimmedBody.StartsWith('[')
                ? "application/json"
                : "text/plain";
        }

        SafeExternalHttpRequest request = new(
            Url: spec.Url,
            Method: spec.Method,
            Headers: spec.Headers.Count > 0 ? spec.Headers : null,
            Body: spec.Body,
            ContentType: contentType,
            WorkspaceId: context.WorkspaceId);

        SafeExternalHttpResponse response;
        try
        {
            if (spec.Method == HttpMethod.Get)
            {
                string memoKey = BuildGetMemoKey(spec.Url, spec.Headers);
                response = await context.GetOrComputeMemoizedAsync(
                    memoKey,
                    ct => httpClient.SendAsync(request, ct),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (SafeExternalHttpException)
        {
            return await EvaluateCatchAllOrIncompleteAsync(spec.CatchAllMapping, context, cancellationToken).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            return TryMatchStatusMapping(spec, response.StatusCode, out TemplateNode? errorBranch)
                ? await errorBranch!.EvaluateAsync(context, cancellationToken).ConfigureAwait(false)
                : await EvaluateCatchAllOrIncompleteAsync(spec.CatchAllMapping, context, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(spec.IntoVariable))
        {
            context.SetVariable(spec.IntoVariable, response.Body);
        }

        if (TryMatchSuccessBranch(spec, response.StatusCode, out TemplateNode? successBranch))
        {
            using JsonInterpolationState jsonState = new(response.Body, spec.JsonPaths);
            (bool placeholderFailed, TagResult branchResult) = await EvaluateBranchWithJsonAsync(
                successBranch!,
                context,
                jsonState,
                cancellationToken).ConfigureAwait(false);

            return context.IsCanceled
                ? TagResult.Incomplete
                : placeholderFailed
                ? await EvaluateCatchAllOrIncompleteAsync(spec.CatchAllMapping, context, cancellationToken).ConfigureAwait(false)
                : branchResult;
        }

        if (spec.JsonPaths.Count > 0)
        {
            bool extractedOk = spec.JsonPaths.Count == 1
                ? TryExtractJsonPath(response.Body, spec.JsonPaths[0], out string? extracted)
                : TryExtractMultipleJsonPaths(response.Body, spec.JsonPaths, out extracted);

            return extractedOk
                ? TagResult.Complete(extracted ?? string.Empty)
                : await EvaluateCatchAllOrIncompleteAsync(spec.CatchAllMapping, context, cancellationToken).ConfigureAwait(false);
        }

        return !string.IsNullOrWhiteSpace(spec.IntoVariable)
            ? TagResult.Complete(string.Empty)
            : TagResult.Complete(response.Body);
    }

    /// <summary>
    /// Evaluates the catch-all branch if present; otherwise returns <see cref="TagResult.Incomplete"/>.
    /// </summary>
    /// <param name="catchAllMapping">The optional catch-all AST branch.</param>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The evaluated catch-all <see cref="TagResult"/> or <see cref="TagResult.Incomplete"/>.</returns>
    private static async ValueTask<TagResult> EvaluateCatchAllOrIncompleteAsync(
        TemplateNode? catchAllMapping,
        ITagEvaluationContext context,
        CancellationToken cancellationToken) =>
        catchAllMapping is not null
            ? await catchAllMapping.EvaluateAsync(context, cancellationToken).ConfigureAwait(false)
            : TagResult.Incomplete;

    /// <summary>
    /// Parses evaluated <c>{api ...}</c> arguments and pre-extracted AST status mapping rules into a structured <see cref="ApiRequestSpec"/>.
    /// </summary>
    /// <param name="args">The evaluated non-mapping argument strings.</param>
    /// <param name="exactStatusMap">Exact status-code mapping branches.</param>
    /// <param name="classStatusMap">Status-class mapping branches.</param>
    /// <param name="successMapping">Reference to the bare <c>-&gt;</c> success branch.</param>
    /// <param name="catchAllMapping">Reference to the <c>!-&gt;</c> / <c>*-&gt;</c> catch-all branch.</param>
    /// <param name="spec">When this method returns <see langword="true"/>, contains the parsed request specification.</param>
    /// <returns><see langword="true"/> if a valid URL and options were parsed; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseArguments(
        List<string> args,
        Dictionary<int, TemplateNode> exactStatusMap,
        Dictionary<int, TemplateNode> classStatusMap,
        ref TemplateNode? successMapping,
        ref TemplateNode? catchAllMapping,
        out ApiRequestSpec spec)
    {
        HttpMethod method = HttpMethod.Get;
        string? url = null;
        string? body = null;
        List<string> jsonPaths = [];
        string? intoVariable = null;
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i++)
        {
            string raw = args[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string trimmed = raw.Trim();

            if (i == 0 && TryParseHttpMethod(trimmed, out HttpMethod? parsedMethod))
            {
                method = parsedMethod!;
                continue;
            }

            if (trimmed.StartsWith("body=", StringComparison.OrdinalIgnoreCase))
            {
                body = trimmed[5..];
                continue;
            }

            if (trimmed.StartsWith("json=", StringComparison.OrdinalIgnoreCase))
            {
                string path = trimmed[5..];
                if (!string.IsNullOrWhiteSpace(path))
                {
                    jsonPaths.Add(path);
                }

                continue;
            }

            if (trimmed.StartsWith("into=", StringComparison.OrdinalIgnoreCase))
            {
                string variableName = trimmed[5..].Trim();
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    spec = default!;
                    return false;
                }

                intoVariable = variableName;
                continue;
            }

            if (trimmed.StartsWith("header=", StringComparison.OrdinalIgnoreCase))
            {
                _ = TryAddHeader(trimmed[7..], headers);
                continue;
            }

            if (TryParseStatusMappingPrefix(trimmed, out StatusMappingMatchKind matchKind, out int statusKey, out string remainder))
            {
                ApplyStatusMappingRule(
                    new StatusMappingRule(matchKind, statusKey, new LiteralNode(remainder)),
                    exactStatusMap,
                    classStatusMap,
                    ref successMapping,
                    ref catchAllMapping);
                continue;
            }

            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains(':', StringComparison.Ordinal)
                && TryAddHeader(trimmed, headers))
            {
                continue;
            }

            url ??= trimmed;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            spec = default!;
            return false;
        }

        spec = new ApiRequestSpec(
            method,
            url,
            headers,
            body,
            jsonPaths.AsReadOnly(),
            intoVariable,
            exactStatusMap,
            classStatusMap,
            successMapping,
            catchAllMapping);
        return true;
    }

    /// <summary>
    /// Attempts to match an HTTP verb name.
    /// </summary>
    /// <param name="token">The candidate verb token.</param>
    /// <param name="method">When this method returns <see langword="true"/>, contains the matching <see cref="HttpMethod"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="token"/> is GET, POST, PUT, PATCH, or DELETE; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseHttpMethod(string token, out HttpMethod? method)
    {
        if (string.Equals(token, "GET", StringComparison.OrdinalIgnoreCase))
        {
            method = HttpMethod.Get;
            return true;
        }

        if (string.Equals(token, "POST", StringComparison.OrdinalIgnoreCase))
        {
            method = HttpMethod.Post;
            return true;
        }

        if (string.Equals(token, "PUT", StringComparison.OrdinalIgnoreCase))
        {
            method = HttpMethod.Put;
            return true;
        }

        if (string.Equals(token, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            method = HttpMethod.Patch;
            return true;
        }

        if (string.Equals(token, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            method = HttpMethod.Delete;
            return true;
        }

        method = null;
        return false;
    }

    /// <summary>
    /// Parses a <c>Key: Value</c> header string and stores it in <paramref name="headers"/>.
    /// </summary>
    /// <param name="rawHeader">The raw header string.</param>
    /// <param name="headers">The destination header dictionary.</param>
    /// <returns><see langword="true"/> if a valid header name was parsed; otherwise, <see langword="false"/>.</returns>
    private static bool TryAddHeader(string rawHeader, Dictionary<string, string> headers)
    {
        int colonIndex = rawHeader.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex <= 0)
        {
            return false;
        }

        string name = rawHeader[..colonIndex].Trim();
        string value = rawHeader[(colonIndex + 1)..].Trim();
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        headers[name] = value;
        return true;
    }

    /// <summary>
    /// Attempts to extract an unevaluated status-mapping branch from an argument AST node.
    /// </summary>
    /// <param name="arg">The unevaluated argument AST node.</param>
    /// <param name="rule">When this method returns <see langword="true"/>, contains the extracted status mapping rule.</param>
    /// <returns><see langword="true"/> if <paramref name="arg"/> is a static status-mapping rule; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractStatusMappingBranch(TemplateNode arg, out StatusMappingRule rule)
    {
        if (arg is LiteralNode literal)
        {
            if (TryParseStatusMappingPrefix(literal.Text, out StatusMappingMatchKind kind, out int statusKey, out string remainder))
            {
                rule = new StatusMappingRule(kind, statusKey, new LiteralNode(remainder));
                return true;
            }

            rule = default;
            return false;
        }

        if (arg is CompositeNode composite && composite.Children.Count > 0 && composite.Children[0] is LiteralNode firstLiteral)
        {
            if (!TryParseStatusMappingPrefix(firstLiteral.Text, out StatusMappingMatchKind kind, out int statusKey, out string remainder))
            {
                rule = default;
                return false;
            }

            TemplateNode branchNode;
            if (composite.Children.Count == 1)
            {
                branchNode = new LiteralNode(remainder);
            }
            else
            {
                List<TemplateNode> branchChildren = new(composite.Children.Count);
                if (remainder.Length > 0)
                {
                    branchChildren.Add(new LiteralNode(remainder));
                }

                for (int i = 1; i < composite.Children.Count; i++)
                {
                    branchChildren.Add(composite.Children[i]);
                }

                branchNode = branchChildren.Count == 1
                    ? branchChildren[0]
                    : new CompositeNode(branchChildren.AsReadOnly());
            }

            rule = new StatusMappingRule(kind, statusKey, branchNode);
            return true;
        }

        rule = default;
        return false;
    }

    /// <summary>
    /// Applies a parsed <see cref="StatusMappingRule"/> to the status mapping collections.
    /// </summary>
    /// <param name="rule">The parsed status mapping rule.</param>
    /// <param name="exactStatusMap">Exact status-code mapping dictionary.</param>
    /// <param name="classStatusMap">Status-class mapping dictionary.</param>
    /// <param name="successMapping">Reference to the bare <c>-&gt;</c> success branch.</param>
    /// <param name="catchAllMapping">Reference to the <c>!-&gt;</c> / <c>*-&gt;</c> catch-all branch.</param>
    private static void ApplyStatusMappingRule(
        StatusMappingRule rule,
        Dictionary<int, TemplateNode> exactStatusMap,
        Dictionary<int, TemplateNode> classStatusMap,
        ref TemplateNode? successMapping,
        ref TemplateNode? catchAllMapping)
    {
        switch (rule.Kind)
        {
            case StatusMappingMatchKind.Success:
                successMapping = rule.Branch;
                break;

            case StatusMappingMatchKind.CatchAll:
                catchAllMapping = rule.Branch;
                break;

            case StatusMappingMatchKind.Exact:
                exactStatusMap[rule.StatusKey] = rule.Branch;
                break;

            case StatusMappingMatchKind.Class:
                classStatusMap[rule.StatusKey] = rule.Branch;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Parses a status mapping prefix such as <c>-&gt;</c>, <c>200-&gt;</c>, <c>404-&gt;</c>, <c>5xx-&gt;</c>, <c>!-&gt;</c>, or <c>*-&gt;</c>.
    /// </summary>
    /// <param name="token">The literal prefix text to inspect.</param>
    /// <param name="kind">When this method returns <see langword="true"/>, contains the mapping kind.</param>
    /// <param name="statusKey">When this method returns <see langword="true"/>, contains the exact status code or status class digit.</param>
    /// <param name="remainder">When this method returns <see langword="true"/>, contains the right-hand literal remainder after <c>-&gt;</c>.</param>
    /// <returns><see langword="true"/> if <paramref name="token"/> starts with a valid status mapping prefix; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseStatusMappingPrefix(
        string token,
        out StatusMappingMatchKind kind,
        out int statusKey,
        out string remainder)
    {
        kind = default;
        statusKey = 0;
        remainder = string.Empty;

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        string trimmedStart = token.TrimStart();
        if (trimmedStart.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmedStart.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int arrowIndex = token.IndexOf("->", StringComparison.Ordinal);
        if (arrowIndex < 0)
        {
            return false;
        }

        string pattern = token[..arrowIndex].Trim();
        remainder = token[(arrowIndex + 2)..];

        if (pattern.Length == 0)
        {
            kind = StatusMappingMatchKind.Success;
            return true;
        }

        if (string.Equals(pattern, "!", StringComparison.Ordinal) || string.Equals(pattern, "*", StringComparison.Ordinal))
        {
            kind = StatusMappingMatchKind.CatchAll;
            return true;
        }

        if (int.TryParse(pattern, NumberStyles.None, CultureInfo.InvariantCulture, out int exactCode)
            && exactCode is >= 100 and <= 599)
        {
            kind = StatusMappingMatchKind.Exact;
            statusKey = exactCode;
            return true;
        }

        if (pattern.Length == 3
            && pattern[0] is >= '1' and <= '5'
            && string.Equals(pattern[1..], "xx", StringComparison.OrdinalIgnoreCase))
        {
            kind = StatusMappingMatchKind.Class;
            statusKey = pattern[0] - '0';
            return true;
        }

        return false;
    }

    /// <summary>
    /// Matches an HTTP status code against exact and class status mapping rules.
    /// </summary>
    /// <param name="spec">The parsed request specification.</param>
    /// <param name="statusCode">The HTTP response status code.</param>
    /// <param name="mapped">When this method returns <see langword="true"/>, contains the matched AST branch node.</param>
    /// <returns><see langword="true"/> if an exact or class mapping matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryMatchStatusMapping(ApiRequestSpec spec, int statusCode, out TemplateNode? mapped)
    {
        if (spec.ExactStatusMap.TryGetValue(statusCode, out TemplateNode? exactValue))
        {
            mapped = exactValue;
            return true;
        }

        int statusClass = statusCode / 100;
        if (spec.ClassStatusMap.TryGetValue(statusClass, out TemplateNode? classValue))
        {
            mapped = classValue;
            return true;
        }

        mapped = null;
        return false;
    }

    /// <summary>
    /// Matches a 2xx success response against exact, class, or bare <c>-&gt;</c> status mapping rules.
    /// </summary>
    /// <param name="spec">The parsed request specification.</param>
    /// <param name="statusCode">The HTTP 2xx status code.</param>
    /// <param name="branch">When this method returns <see langword="true"/>, contains the matched AST branch node.</param>
    /// <returns><see langword="true"/> if a success branch matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryMatchSuccessBranch(ApiRequestSpec spec, int statusCode, out TemplateNode? branch)
    {
        if (TryMatchStatusMapping(spec, statusCode, out branch))
        {
            return true;
        }

        if (spec.SuccessMapping is not null)
        {
            branch = spec.SuccessMapping;
            return true;
        }

        branch = null;
        return false;
    }

    /// <summary>
    /// Evaluates a matched success AST branch while interpolating <c>{token}</c> JSON placeholders only within <see cref="LiteralNode"/> segments.
    /// </summary>
    /// <param name="branch">The matched branch AST node.</param>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="jsonState">The lazily parsed JSON state for placeholder resolution.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating whether a JSON placeholder failed to resolve and the evaluated <see cref="TagResult"/>.</returns>
    private static async ValueTask<(bool PlaceholderFailed, TagResult Result)> EvaluateBranchWithJsonAsync(
        TemplateNode branch,
        ITagEvaluationContext context,
        JsonInterpolationState jsonState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.IsCanceled)
        {
            return (false, TagResult.Incomplete);
        }

        if (branch is LiteralNode literal)
        {
            string raw = literal.Text ?? string.Empty;
            if (!TryInterpolateLiteral(raw, jsonState, out string interpolated))
            {
                return (true, TagResult.Incomplete);
            }

            context.AddBufferChars(interpolated.Length);
            return (false, TagResult.Complete(interpolated));
        }

        if (branch is CompositeNode composite)
        {
            if (composite.Children.Count == 0)
            {
                return (false, TagResult.Complete(string.Empty));
            }

            StringBuilder builder = new();
            bool allComplete = true;

            foreach (TemplateNode child in composite.Children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (context.IsCanceled)
                {
                    return (false, TagResult.Incomplete);
                }

                (bool childPlaceholderFailed, TagResult childResult) = await EvaluateBranchWithJsonAsync(
                    child,
                    context,
                    jsonState,
                    cancellationToken).ConfigureAwait(false);

                if (context.IsCanceled)
                {
                    return (false, TagResult.Incomplete);
                }

                if (childPlaceholderFailed)
                {
                    return (true, TagResult.Incomplete);
                }

                if (!childResult.IsComplete)
                {
                    allComplete = false;
                }

                if (builder.Length + childResult.Value.Length > context.MaxEvaluationBufferChars)
                {
                    throw new TagEvaluationException(
                        TagEvaluationFailureReason.MaxEvaluationBufferExceeded,
                        $"Template evaluation buffer exceeded {context.MaxEvaluationBufferChars} characters.");
                }

                _ = builder.Append(childResult.Value);
            }

            return (false, new TagResult(builder.ToString(), allComplete));
        }

        TagResult result = await branch.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return (false, result);
    }

    /// <summary>
    /// Performs single-pass <c>{token}</c> JSON placeholder interpolation on literal branch text.
    /// </summary>
    /// <param name="text">The literal text segment.</param>
    /// <param name="jsonState">The lazily parsed JSON state.</param>
    /// <param name="interpolated">When this method returns <see langword="true"/>, contains the interpolated string.</param>
    /// <returns><see langword="true"/> if all placeholders resolved successfully; otherwise, <see langword="false"/>.</returns>
    private static bool TryInterpolateLiteral(string text, JsonInterpolationState jsonState, out string interpolated)
    {
        int firstOpen = text.IndexOf('{', StringComparison.Ordinal);
        if (firstOpen < 0)
        {
            interpolated = text;
            return true;
        }

        StringBuilder builder = new(text.Length);
        int cursor = 0;

        while (cursor < text.Length)
        {
            int openIndex = text.IndexOf('{', cursor);
            if (openIndex < 0)
            {
                _ = builder.Append(text, cursor, text.Length - cursor);
                break;
            }

            int closeIndex = text.IndexOf('}', openIndex + 1);
            if (closeIndex < 0)
            {
                _ = builder.Append(text, cursor, text.Length - cursor);
                break;
            }

            int nestedOpen = text.IndexOf('{', openIndex + 1, closeIndex - openIndex - 1);
            if (nestedOpen >= 0)
            {
                _ = builder.Append(text, cursor, nestedOpen - cursor);
                cursor = nestedOpen;
                continue;
            }

            string token = text[(openIndex + 1)..closeIndex];
            if (string.IsNullOrWhiteSpace(token))
            {
                _ = builder.Append(text, cursor, closeIndex + 1 - cursor);
                cursor = closeIndex + 1;
                continue;
            }

            if (!jsonState.TryResolvePlaceholder(token, out string resolved))
            {
                interpolated = string.Empty;
                return false;
            }

            _ = builder.Append(text, cursor, openIndex - cursor);
            _ = builder.Append(resolved);
            cursor = closeIndex + 1;
        }

        interpolated = builder.ToString();
        return true;
    }

    /// <summary>
    /// Builds a deterministic per-message memoization key for GET requests.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="headers">The normalized request headers.</param>
    /// <returns>The memoization cache key.</returns>
    private static string BuildGetMemoKey(string url, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.Count == 0)
        {
            return $"api:GET:{url}";
        }

        StringBuilder builder = new();
        _ = builder.Append("api:GET:").Append(url);
        foreach (KeyValuePair<string, string> kv in headers.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            _ = builder.Append('|').Append(kv.Key).Append('=').Append(kv.Value);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Extracts multiple JSON paths from <paramref name="jsonBody"/> in a single parse and joins them with a single space.
    /// </summary>
    /// <param name="jsonBody">The raw JSON response body.</param>
    /// <param name="paths">The ordered extraction paths.</param>
    /// <param name="joined">When this method returns <see langword="true"/>, contains the space-joined extracted values.</param>
    /// <returns><see langword="true"/> if all paths resolved to non-null JSON values; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractMultipleJsonPaths(
        string jsonBody,
        IReadOnlyList<string> paths,
        out string? joined)
    {
        joined = null;
        if (string.IsNullOrWhiteSpace(jsonBody) || paths.Count == 0)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonBody);
            string[] values = new string[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                if (!TryExtractJsonPath(document.RootElement, paths[i], out string? extracted) || extracted is null)
                {
                    return false;
                }

                values[i] = extracted;
            }

            joined = string.Join(' ', values);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts a value from <paramref name="jsonBody"/> using a dot/bracket path such as <c>data.items[0].name</c>.
    /// </summary>
    /// <param name="jsonBody">The raw JSON response body.</param>
    /// <param name="path">The dot/array extraction path.</param>
    /// <param name="extracted">When this method returns <see langword="true"/>, contains the extracted string value.</param>
    /// <returns><see langword="true"/> if the path resolved to a non-null JSON value; otherwise, <see langword="false"/>.</returns>
    internal static bool TryExtractJsonPath(string jsonBody, string path, out string? extracted)
    {
        extracted = null;
        if (string.IsNullOrWhiteSpace(jsonBody) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonBody);
            return TryExtractJsonPath(document.RootElement, path, out extracted);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts a value from a parsed <see cref="JsonElement"/> using a dot/bracket path such as <c>data.items[0].name</c>.
    /// </summary>
    /// <param name="root">The root JSON element.</param>
    /// <param name="path">The dot/array extraction path.</param>
    /// <param name="extracted">When this method returns <see langword="true"/>, contains the extracted string value.</param>
    /// <returns><see langword="true"/> if the path resolved to a non-null JSON value; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractJsonPath(JsonElement root, string path, out string? extracted)
    {
        extracted = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        JsonElement current = root;
        int index = 0;
        string trimmedPath = path.Trim();

        while (index < trimmedPath.Length)
        {
            if (trimmedPath[index] == '.')
            {
                index++;
                continue;
            }

            if (trimmedPath[index] == '[')
            {
                int closeBracket = trimmedPath.IndexOf(']', index + 1);
                if (closeBracket < 0)
                {
                    return false;
                }

                string indexToken = trimmedPath[(index + 1)..closeBracket].Trim();
                if (!int.TryParse(indexToken, NumberStyles.None, CultureInfo.InvariantCulture, out int arrayIndex)
                    || current.ValueKind != JsonValueKind.Array
                    || arrayIndex < 0
                    || arrayIndex >= current.GetArrayLength())
                {
                    return false;
                }

                current = current[arrayIndex];
                index = closeBracket + 1;
                continue;
            }

            int nextDelimiter = trimmedPath.IndexOfAny(['.', '['], index);
            string propertySegment = nextDelimiter < 0
                ? trimmedPath[index..]
                : trimmedPath[index..nextDelimiter];
            index = nextDelimiter < 0 ? trimmedPath.Length : nextDelimiter;

            if (current.ValueKind == JsonValueKind.Array
                && int.TryParse(propertySegment, NumberStyles.None, CultureInfo.InvariantCulture, out int dotIndex))
            {
                if (dotIndex < 0 || dotIndex >= current.GetArrayLength())
                {
                    return false;
                }

                current = current[dotIndex];
                continue;
            }

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propertySegment, out JsonElement nextElement))
            {
                return false;
            }

            current = nextElement;
        }

        switch (current.ValueKind)
        {
            case JsonValueKind.String:
                extracted = current.GetString();
                return extracted is not null;

            case JsonValueKind.Number:
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                extracted = current.GetRawText();
                return true;

            case JsonValueKind.True:
                extracted = "true";
                return true;

            case JsonValueKind.False:
                extracted = "false";
                return true;
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
            default:
                return false;
        }
    }

    /// <summary>
    /// Identifies the match target of a status mapping rule.
    /// </summary>
    private enum StatusMappingMatchKind
    {
        /// <summary>
        /// Matches any 2xx success status (<c>-&gt;</c>).
        /// </summary>
        Success,

        /// <summary>
        /// Matches a specific 3-digit HTTP status code (<c>XXX-&gt;</c>).
        /// </summary>
        Exact,

        /// <summary>
        /// Matches a status code class (<c>Xxx-&gt;</c>).
        /// </summary>
        Class,

        /// <summary>
        /// Matches any unhandled failure or error (<c>!-&gt;</c> or <c>*-&gt;</c>).
        /// </summary>
        CatchAll,
    }

    /// <summary>
    /// Represents a parsed status mapping rule with its unevaluated AST branch node.
    /// </summary>
    /// <param name="Kind">The rule match kind.</param>
    /// <param name="StatusKey">The exact status code or status class digit.</param>
    /// <param name="Branch">The unevaluated right-hand side AST branch.</param>
    private readonly record struct StatusMappingRule(
        StatusMappingMatchKind Kind,
        int StatusKey,
        TemplateNode Branch);

    /// <summary>
    /// Holds lazily parsed JSON document state for single-parse <c>{token}</c> interpolation across a matched success branch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initializes a new instance of the <see cref="JsonInterpolationState"/> class.
    /// </para>
    /// </remarks>
    /// <param name="responseBody">The raw HTTP response body.</param>
    /// <param name="jsonPaths">The ordered <c>json=</c> extraction paths.</param>
    private sealed class JsonInterpolationState(string responseBody, IReadOnlyList<string> jsonPaths) : IDisposable
    {
        /// <summary>
        /// The raw JSON response body.
        /// </summary>
        private readonly string _responseBody = responseBody;

        /// <summary>
        /// The ordered <c>json=</c> extraction paths for numeric <c>{0}</c>, <c>{1}</c>, and subsequent placeholders.
        /// </summary>
        private readonly IReadOnlyList<string> _jsonPaths = jsonPaths;

        /// <summary>
        /// The cached parsed <see cref="JsonDocument"/>, if parsing succeeded.
        /// </summary>
        private JsonDocument? _document;

        /// <summary>
        /// Tracks whether <see cref="_responseBody"/> has already been parsed.
        /// </summary>
        private bool _parsed;

        /// <summary>
        /// Attempts to resolve a <c>{token}</c> placeholder against the parsed JSON body.
        /// </summary>
        /// <param name="token">The placeholder token inside braces.</param>
        /// <param name="value">When this method returns <see langword="true"/>, contains the extracted value.</param>
        /// <returns><see langword="true"/> if the placeholder resolved to a non-null JSON value; otherwise, <see langword="false"/>.</returns>
        public bool TryResolvePlaceholder(string token, out string value)
        {
            value = string.Empty;
            if (!this.EnsureParsed() || this._document is null)
            {
                return false;
            }

            string trimmed = token.Trim();
            string path = int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index >= 0
                && index < this._jsonPaths.Count
                ? this._jsonPaths[index]
                : trimmed;

            if (!TryExtractJsonPath(this._document.RootElement, path, out string? extracted) || extracted is null)
            {
                return false;
            }

            value = extracted;
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this._document?.Dispose();
            this._document = null;
        }

        /// <summary>
        /// Parses <see cref="_responseBody"/> once on demand.
        /// </summary>
        /// <returns><see langword="true"/> if a valid JSON document was parsed; otherwise, <see langword="false"/>.</returns>
        private bool EnsureParsed()
        {
            if (this._parsed)
            {
                return this._document is not null;
            }

            this._parsed = true;
            if (string.IsNullOrWhiteSpace(this._responseBody))
            {
                return false;
            }

            try
            {
                this._document = JsonDocument.Parse(this._responseBody);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Parsed specification for an outbound <c>{api ...}</c> call.
    /// </summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Url">The target URL string.</param>
    /// <param name="Headers">The custom request headers.</param>
    /// <param name="Body">The optional request body.</param>
    /// <param name="JsonPaths">The ordered JSON extraction paths.</param>
    /// <param name="IntoVariable">The optional routine variable name for storing the 2xx response body.</param>
    /// <param name="ExactStatusMap">Exact status-code output branches.</param>
    /// <param name="ClassStatusMap">Status-class (<c>2xx</c>/<c>4xx</c>/<c>5xx</c>) output branches.</param>
    /// <param name="SuccessMapping">Optional bare <c>-&gt;</c> success branch.</param>
    /// <param name="CatchAllMapping">Optional catch-all (<c>!-&gt;</c> / <c>*-&gt;</c>) branch.</param>
    private sealed record ApiRequestSpec(
        HttpMethod Method,
        string Url,
        IReadOnlyDictionary<string, string> Headers,
        string? Body,
        IReadOnlyList<string> JsonPaths,
        string? IntoVariable,
        IReadOnlyDictionary<int, TemplateNode> ExactStatusMap,
        IReadOnlyDictionary<int, TemplateNode> ClassStatusMap,
        TemplateNode? SuccessMapping,
        TemplateNode? CatchAllMapping);
}
