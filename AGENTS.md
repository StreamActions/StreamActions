# AGENTS.md
This file contains instructions for Generative AI Agents

## Where instructions live
- This file only describes the solution topology. Detailed rules live in `.agents/rules/`, and multi-step workflows live in `.agents/skills/`
- Antigravity loads these automatically. Other agents must read them manually:
	- Always: `.agents/rules/00-global-discipline.md` and `.agents/rules/05-sandbox-execution.md`
	- When creating or editing `.cs`, `.csproj`, `.sln`, `.slnx`, `Directory.Build.props`, `Directory.Packages.props`, or `.editorconfig`: `.agents/rules/10-csharp-dotnet.md`, then follow `.agents/skills/dotnet-build-verify/SKILL.md` to verify
	- When creating or editing `.py`, `pyproject.toml`, or `uv.lock`: `.agents/rules/20-python-uv.md`
- Folder-level `AGENTS.md` files contain domain-specific invariants. How to resolve them is defined in `.agents/rules/00-global-discipline.md`

## Solution topology
- Visual Studio 2026 solution: `StreamActions.sln`. The Python utilities are Visual Studio Python projects (`.pyproj`)
- Centralized configuration lives at the repository root. Never duplicate these settings in individual projects:
	- `.editorconfig`: code style and analyzer severities
	- `Directory.Build.props`: `AnalysisLevel` and `AnalysisLevelStyle` (`latest-all`), `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`, `AccelerateBuildsInVisualStudio`
	- `Directory.Packages.props`: Central Package Management (`ManagePackageVersionsCentrally`). Every NuGet version is declared here
	- `StreamActions.sln.licenseheader`: license header templates per file extension

## C# projects
| Project | Role |
| :-- | :-- |
| `StreamActions.Common` | Shared utilities (`Util.BuildUri`, JSON converters, `NameValueCollection` extensions, exceptions, `DocParserAttribute`) |
| `StreamActions.Twitch` | Pure Twitch Helix API, EventSub, and OAuth client library |
| `StreamActions.Core` | Pure domain models, store/plugin/platform abstractions, `BootstrapOptions`, `ISecretRedactor`, bounded `InMemoryEventBus`, and `PluginManager` (zero DB/ASP.NET Core dependencies) |
| `StreamActions.Database` | EF Core 10 persistence (`StreamActionsDbContext`, `IDbContextFactory`, SQLite/PostgreSQL/MySQL/MariaDB/SQL Server providers, Data Protection encryption, `HybridCache`, `IWorkspaceKeyValueStore`, hot backups, `.sabackup` archives) |
| `StreamActions.Web` | ASP.NET Core & Blazor Server 10 shell, strongly-typed SignalR hubs, and `/healthz` / `/readyz` health endpoints |
| `StreamActions.Daemon` | Host composition root (`Program.cs`), pre-DB Sentry bootstrap, Windows Service / `systemd`, 3-phase graceful shutdown, CLI & local IPC, `Dockerfile`, and RID publish profiles |
| `StreamActions.Common.Tests`, `StreamActions.Twitch.Tests`, `StreamActions.Core.Tests`, `StreamActions.Database.Tests`, `StreamActions.Daemon.Tests` | xUnit v3 + FluentAssertions test projects |

- The project name is the root namespace and the project folder name. Sub-folders map to sub-namespaces
- Projects may contain `Usings.cs` (project-wide `global using` directives) and `GlobalSuppressions.cs` (assembly-level `SuppressMessage` attributes)
- All projects target `net10.0` and must stay forward-compatible with .NET 11 and later
- Each test project is named `<Project>.Tests` and is granted `InternalsVisibleTo` by the project under test

## Python tooling (uv)
- Each Python folder is an independent `uv` project with its own `pyproject.toml` and `uv.lock` (Python >= 3.13)
- `/Diff`: Platform documentation parsers (`BaseParser.py` is 100% website-agnostic; platform parsers live in `Diff/parsers/<platform>/`, e.g., `Diff/parsers/twitch/`). `DocParserFinder.py` discovers `[DocParser(...)]` attributes in `.cs` files, and `.github/workflows/update-issues.yml` runs the matching parser in `Diff/parsers/` to keep tracking issues up to date
- `/TldRegexUpdater`: generates the TLD portion of the chat link-detection regex from the IANA TLD list

## CI
- `.github/workflows/ci.yml`: `dotnet test` on .NET 9, with timezone (`UTC`, `America/New_York`, `Europe/London`, `Asia/Tokyo`) and OS (Ubuntu, Windows, macOS) matrices
- `.github/workflows/codeql-analysis.yml`: CodeQL
- `.github/workflows/update-issues.yml`: scheduled DocParser diff and issue updater (uses `uv`)