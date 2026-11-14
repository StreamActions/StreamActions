---
trigger: glob
globs: "**/*.py, **/pyproject.toml, **/uv.lock"
---

# Python / uv Standards

## Execution
- Always run Python through `uv` from the repository root with `--project`, for example `uv run --project Diff Diff/parsers/twitch/TwitchReferenceParser.py --url <url>`. Never call `python` or `pip` directly, and never activate a virtual environment
- `uv` always runs outside the sandbox (see `05-sandbox-execution.md`). `uv run` and `uv sync` are auto-approved in the synced allowlist; `uv add`, `uv remove`, and `uv lock` prompt the operator and must be skipped in unattended runs
- Manage dependencies only with `uv add --project <folder> <package>` or `uv remove --project <folder> <package>`, so that `pyproject.toml` and `uv.lock` change together. Never hand-edit `uv.lock`. CI runs `uv sync --locked`, so a stale lock fails the workflow
- Keep `requires-python = ">=3.13"`. `.py` files use the `.py` section of `StreamActions.sln.licenseheader`
- Add Python docstrings (pyDoc) for all modules, classes, functions, and methods, including non-public/internal (`_name`, `__name`) helpers

## Doc parsers (`/Diff`)
- `Diff/parsers/BaseParser.py` must remain 100% website-agnostic (providing `fetchUrl(url)` for multi-page parsers and `parseFromUrl(url, out)`). Platform-specific parsers live in `Diff/parsers/<platform>/<Name>Parser.py` (e.g., `Diff/parsers/twitch/`, `Diff/parsers/youtube/`, `Diff/parsers/discord/`), subclass `BaseParser` (or a platform base like `TwitchMarkdownBaseParser`), implement `parse(data) -> dict`, and call `main()` when run as a script
- The output contract is consumed by `.github/workflows/update-issues.yml`. Never rename these keys:
  `{"toc": {"<Category>": [{"endpoint": "<Friendly Name>", "description"?: "..."}]}, "endpoints": {"<Friendly Name>": {"slug": "#fragment-or-relative-path", ...}}}`
- Diff output keys entries under `endpoints` and marks changes with `_operation`. Keep the existing diff semantics of `BaseParser`
- The parser path relative to `Diff/parsers/` (without `.py`) must equal the `parser` argument of `[DocParser("friendlyName", issueNumber, "url", "<platform>/ParserName")]` in C#. `DocParserFinder.py` matches that exact attribute shape with a regex, so do not change the attribute's argument order or quoting
- On a non-200 HTTP response, exit with status 1 and write `<out>.stderr.txt`. The workflow reads that file, so preserve this behavior
- Write JSON as UTF-8 with `json.dump`. Indent only with `--pretty` or `--diffpretty`. Write to stdout when `--out` or `--diffout` is omitted
- Strip non-printable characters with `BaseParser.printable` before parsing. Keep the `streamactions.diff.parser/1` User-Agent
- When agents use the parsers for research, either omit `--out` (stdout) or write only under `.agents/scratch/docparser/`

## TLD regex generator (`/TldRegexUpdater`)
- The source is `https://data.iana.org/TLD/tlds-alpha-by-domain.txt`. The output is a single named group `(?<tld>...)` written to `tldregex.txt`
- Generated regexes must stay backtracking-safe:
	- Only use non-capturing groups `(?:...)`, alternation factored as a prefix trie, and character classes (with ranges) for single-character branches
	- Never emit quantifiers (`*`, `+`, `?` used as a quantifier, `{n,m}`) inside the generated group
	- Escape `( ) . \ [ ]`
- Include IDN TLDs in both punycode (`xn--`) and Unicode form (`idna.decode`)
- Generated files (`tldregex.txt`, and with `-v`: `tld_tree.json`, `tld_itree.json`, `xntlds.json`, `tlds.txt`) are build outputs. Never commit them

