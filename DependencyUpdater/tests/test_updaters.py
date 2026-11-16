# coding=utf-8
#
# This file is part of StreamActions.
# Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
#
# StreamActions is free software: you can redistribute it and/or modify
# it under the terms of the GNU Affero General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# StreamActions is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU Affero General Public License for more details.
#
# You should have received a copy of the GNU Affero General Public License
# along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
#

"""
Comprehensive offline unit tests for DotNetUpdater, PythonUpdater, DockerUpdater,
GitHubActionsUpdater, and the DependencyUpdater CLI orchestrator.
"""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import MagicMock, patch

_DEP_UPDATER_ROOT = Path(__file__).resolve().parent.parent
if str(_DEP_UPDATER_ROOT) not in sys.path:
    sys.path.insert(0, str(_DEP_UPDATER_ROOT))

import DependencyUpdater as cli
from updaters.BaseUpdater import (
    BaseUpdater,
    DependencyUpdate,
    EcosystemResult,
    _sanitize_md_url,
    format_markdown_report,
)
from updaters.DockerUpdater import DockerUpdater, align_dotnet_image_tag, parse_image_reference
from updaters.DotNetUpdater import DotNetUpdater, is_runtime_aligned_package, parse_nuget_version
from updaters.GitHubActionsUpdater import (
    GitHubActionsUpdater,
    _validate_tag_name,
    parse_action_repo,
)
from updaters.PythonUpdater import PythonUpdater, update_pyproject_specifiers


class DotNetUpdaterTests(unittest.TestCase):
    """
    Tests for NuGet version parsing, runtime ceiling enforcement, TFM checks, and file discovery.
    """

    def test_runtime_aligned_package_detection(self) -> None:
        """
        Verify only runtime-aligned Microsoft packages trigger the major-version prefix cap.
        """
        self.assertTrue(is_runtime_aligned_package("Microsoft.AspNetCore.DataProtection"))
        self.assertTrue(is_runtime_aligned_package("Microsoft.EntityFrameworkCore"))
        self.assertTrue(is_runtime_aligned_package("Microsoft.EntityFrameworkCore.Sqlite"))
        self.assertTrue(is_runtime_aligned_package("Microsoft.Extensions.Caching.Hybrid"))
        self.assertFalse(is_runtime_aligned_package("Microsoft.IdentityModel.JsonWebTokens"))
        self.assertFalse(is_runtime_aligned_package("Microsoft.NET.Test.Sdk"))
        self.assertFalse(is_runtime_aligned_package("FluentAssertions"))

    def test_dotnet_updater_caps_runtime_and_transitive_ef_packages(self) -> None:
        """
        Verify runtime-aligned packages allow 10.11.x but reject 11.0.0, third-party EF packages
        depending on Microsoft.EntityFrameworkCore >= 11.0.0 are rejected, net11.0-only packages
        are rejected, independent packages advance, and dependabot.yml ceiling stays synced.
        """
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "Directory.Build.props").write_text(
                "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n",
                encoding="utf-8",
            )
            (root / "Directory.Packages.props").write_text(
                "<Project>\n"
                "  <ItemGroup>\n"
                '    <PackageVersion Include="Microsoft.Extensions.Caching.Hybrid" Version="10.10.0" />\n'
                '    <PackageVersion Include="Microsoft.AspNetCore.DataProtection" Version="10.0.15" />\n'
                '    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />\n'
                '    <PackageVersion Include="Microsoft.IdentityModel.JsonWebTokens" Version="8.23.0" />\n'
                '    <PackageVersion Include="Some.FutureOnly.Lib" Version="1.0.0" />\n'
                "  </ItemGroup>\n"
                "</Project>\n",
                encoding="utf-8",
            )
            (root / ".config").mkdir()
            (root / ".config" / "dotnet-tools.json").write_text(
                json.dumps(
                    {
                        "version": 1,
                        "isRoot": True,
                        "tools": {
                            "dotnet-reportgenerator-globaltool": {
                                "version": "5.4.0",
                                "commands": ["reportgenerator"],
                            }
                        },
                    },
                    indent=2,
                )
                + "\n",
                encoding="utf-8",
            )
            (root / ".github").mkdir()
            (root / ".github" / "dependabot.yml").write_text(
                'version: 2\nupdates:\n  - package-ecosystem: "nuget"\n    ignore:\n'
                '      - dependency-name: "Microsoft.AspNetCore.*"\n        versions:\n          - ">= 9.0.0"\n',
                encoding="utf-8",
            )

            catalogs = {
                "microsoft.extensions.caching.hybrid": [
                    {"version": "10.11.0", "listed": True, "dependencyGroups": [{"targetFramework": "net10.0"}]},
                    {"version": "11.0.0", "listed": True, "dependencyGroups": [{"targetFramework": "netstandard2.0"}]},
                ],
                "microsoft.aspnetcore.dataprotection": [
                    {"version": "10.0.16", "listed": True, "dependencyGroups": [{"targetFramework": "net10.0"}]},
                    {"version": "11.0.0", "listed": True, "dependencyGroups": [{"targetFramework": "net11.0"}]},
                ],
                "npgsql.entityframeworkcore.postgresql": [
                    {
                        "version": "10.0.4",
                        "listed": True,
                        "dependencyGroups": [
                            {
                                "targetFramework": "net10.0",
                                "dependencies": [{"id": "Microsoft.EntityFrameworkCore", "range": "[10.0.15, )"}],
                            }
                        ],
                    },
                    {
                        "version": "11.0.0",
                        "listed": True,
                        "dependencyGroups": [
                            {
                                "targetFramework": "net10.0",
                                "dependencies": [{"id": "Microsoft.EntityFrameworkCore", "range": "[11.0.0, )"}],
                            }
                        ],
                    },
                ],
                "microsoft.identitymodel.jsonwebtokens": [
                    {"version": "9.1.0", "listed": True, "dependencyGroups": [{"targetFramework": "net10.0"}]}
                ],
                "some.futureonly.lib": [
                    {"version": "2.0.0", "listed": True, "dependencyGroups": [{"targetFramework": "net11.0"}]}
                ],
                "dotnet-reportgenerator-globaltool": [
                    {"version": "5.5.1", "listed": True, "dependencyGroups": [{"targetFramework": "net10.0"}]}
                ],
            }

            updater = DotNetUpdater(root, dry_run=False, verify=False)
            with patch.object(updater, "_fetch_catalog_entries", side_effect=lambda pkg: catalogs[pkg.lower()]):
                result = updater.update()

            self.assertEqual(result.errors, [])
            updated_map = {u.name: u.new_version for u in result.updates}
            self.assertEqual(updated_map["Microsoft.Extensions.Caching.Hybrid"], "10.11.0")
            self.assertEqual(updated_map["Microsoft.AspNetCore.DataProtection"], "10.0.16")
            self.assertEqual(updated_map["Npgsql.EntityFrameworkCore.PostgreSQL"], "10.0.4")
            self.assertEqual(updated_map["Microsoft.IdentityModel.JsonWebTokens"], "9.1.0")
            self.assertEqual(updated_map["dotnet-reportgenerator-globaltool"], "5.5.1")
            self.assertNotIn("Some.FutureOnly.Lib", updated_map)

            skipped_names = {s.name for s in result.skipped}
            self.assertIn("Microsoft.Extensions.Caching.Hybrid", skipped_names)
            self.assertIn("Npgsql.EntityFrameworkCore.PostgreSQL", skipped_names)
            self.assertIn("Some.FutureOnly.Lib", skipped_names)

            dependabot_text = (root / ".github" / "dependabot.yml").read_text(encoding="utf-8")
            self.assertIn('">= 11.0.0"', dependabot_text)

    def test_parse_nuget_version_ordering(self) -> None:
        """
        Verify stable releases sort higher than prereleases of the same version.
        """
        pre = parse_nuget_version("10.0.0-rc.1")
        stable = parse_nuget_version("10.0.0")
        assert pre is not None and stable is not None
        self.assertGreater(stable.sort_key, pre.sort_key)


class PythonUpdaterTests(unittest.TestCase):
    """
    Tests for `pyproject.toml` specifier syncing and automatic `uv` project discovery.
    """

    def test_update_pyproject_specifiers_preserves_extras_and_markers(self) -> None:
        """
        Verify `>=` version bounds update cleanly without disturbing extras, upper bounds, or markers.
        """
        content = (
            "[project]\n"
            'dependencies = [\n'
            '    "requests>=2.34.2",\n'
            '    "httpx[http2]>=0.27.0,<1.0.0",\n'
            '    "idna>=3.18 ; python_version >= \'3.13\'",\n'
            "]\n"
        )
        updated, changes = update_pyproject_specifiers(
            content,
            {"requests": "2.35.1", "httpx": "0.28.1", "idna": "3.18"},
        )
        self.assertIn('"requests>=2.35.1"', updated)
        self.assertIn('"httpx[http2]>=0.28.1,<1.0.0"', updated)
        self.assertIn('"idna>=3.18 ; python_version >= \'3.13\'"', updated)
        self.assertEqual(len(changes), 2)

    def test_python_updater_updates_pyproject_and_lock(self) -> None:
        """
        Verify PythonUpdater runs `uv lock --upgrade`, updates `pyproject.toml`, and skips excluded dirs.
        """
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            proj = root / "Diff"
            proj.mkdir()
            (proj / "pyproject.toml").write_text(
                '[project]\nname = "diff"\nversion = "1.0.0"\ndependencies = [\n    "requests>=2.34.2",\n]\n',
                encoding="utf-8",
            )
            (proj / "uv.lock").write_text(
                'version = 1\n\n[[package]]\nname = "requests"\nversion = "2.34.2"\n',
                encoding="utf-8",
            )
            ignored = root / ".venv" / "sub"
            ignored.mkdir(parents=True)
            (ignored / "pyproject.toml").write_text('[project]\nname = "ignored"\n', encoding="utf-8")

            updater = PythonUpdater(root, dry_run=False, verify=True)

            def fake_run(args: list[str], cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
                if args[:3] == ["uv", "lock", "--upgrade"]:
                    (proj / "uv.lock").write_text(
                        'version = 1\n\n[[package]]\nname = "requests"\nversion = "2.35.0"\n',
                        encoding="utf-8",
                    )
                return subprocess.CompletedProcess(args=args, returncode=0, stdout="", stderr="")

            with patch.object(updater, "run_command", side_effect=fake_run):
                result = updater.update()

            self.assertEqual(result.errors, [])
            self.assertIn("Diff/pyproject.toml", result.modified_files)
            self.assertIn("requests>=2.35.0", (proj / "pyproject.toml").read_text(encoding="utf-8"))
            self.assertTrue(any("PASS" in v for v in result.verification))


class DockerUpdaterTests(unittest.TestCase):
    """
    Tests for Dockerfile discovery, `.NET` tag alignment, and multi-arch SHA digest pinning.
    """

    def test_image_reference_and_dotnet_tag_alignment(self) -> None:
        """
        Verify registry/repository splitting and `.NET` runtime tag synchronization.
        """
        self.assertEqual(parse_image_reference("mcr.microsoft.com/dotnet/sdk"), ("mcr.microsoft.com", "dotnet/sdk"))
        self.assertEqual(parse_image_reference("alpine"), ("registry-1.docker.io", "library/alpine"))
        self.assertEqual(
            align_dotnet_image_tag("mcr.microsoft.com/dotnet/aspnet", "10.0-noble", (11, 0)),
            "11.0-noble",
        )

    def test_docker_updater_pins_digests_and_skips_stage_aliases(self) -> None:
        """
        Verify DockerUpdater pins base images to `@sha256:...` while ignoring `FROM build` stage refs.
        """
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "Directory.Build.props").write_text(
                "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n",
                encoding="utf-8",
            )
            app_dir = root / "StreamActions.Daemon"
            app_dir.mkdir()
            (app_dir / "Dockerfile").write_text(
                "FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build\n"
                "FROM build AS publish\n"
                "FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime\n",
                encoding="utf-8",
            )
            sdk_digest = "sha256:" + ("a" * 64)
            aspnet_digest = "sha256:" + ("b" * 64)

            updater = DockerUpdater(root, dry_run=False)
            with patch.object(
                updater,
                "_resolve_manifest_digest",
                side_effect=lambda img, tag: sdk_digest if img.endswith("/sdk") else aspnet_digest,
            ):
                result = updater.update()

            self.assertEqual(result.errors, [])
            content = (app_dir / "Dockerfile").read_text(encoding="utf-8")
            self.assertIn(f"FROM mcr.microsoft.com/dotnet/sdk:10.0@{sdk_digest} AS build", content)
            self.assertIn("FROM build AS publish", content)
            self.assertIn(f"FROM mcr.microsoft.com/dotnet/aspnet:10.0@{aspnet_digest} AS runtime", content)


class GitHubActionsUpdaterTests(unittest.TestCase):
    """
    Tests for GitHub Actions workflow discovery, vendor allowlisting, and SHA pinning.
    """

    def test_parse_action_repo(self) -> None:
        """
        Verify sub-actions resolve to their parent `owner/repo`.
        """
        self.assertEqual(parse_action_repo("github/codeql-action/init"), ("github", "codeql-action"))
        self.assertEqual(parse_action_repo("actions/checkout"), ("actions", "checkout"))

    def test_actions_updater_enforces_vendor_allowlist_and_pins_shas(self) -> None:
        """
        Verify approved actions are updated to 40-char SHAs with tag comments while unapproved owners fail.
        """
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            wf_dir = root / ".github" / "workflows"
            wf_dir.mkdir(parents=True)
            (wf_dir / "ci.yml").write_text(
                "jobs:\n  test:\n    steps:\n"
                "      - uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0\n"
                "      - uses: astral-sh/setup-uv@v7\n"
                "      - uses: random-user/unapproved-action@v1\n",
                encoding="utf-8",
            )
            checkout_sha = "1" * 40
            uv_sha = "2" * 40

            updater = GitHubActionsUpdater(root, dry_run=False)
            with patch.object(
                updater,
                "_resolve_latest_release",
                side_effect=lambda owner, repo: (
                    ("v6.1.0", checkout_sha, "https://github.com/actions/checkout/releases/tag/v6.1.0")
                    if repo == "checkout"
                    else ("v7.2.0", uv_sha, "https://github.com/astral-sh/setup-uv/releases/tag/v7.2.0")
                ),
            ):
                result = updater.update()

            self.assertEqual(len(result.errors), 1)
            self.assertIn("random-user", result.errors[0])
            text = (wf_dir / "ci.yml").read_text(encoding="utf-8")
            self.assertIn(f"- uses: actions/checkout@{checkout_sha} # v6.1.0", text)
            self.assertIn(f"- uses: astral-sh/setup-uv@{uv_sha} # v7.2.0", text)


class CliAndReportTests(unittest.TestCase):
    """
    Tests for CLI ecosystem selection and Markdown PR summary generation.
    """

    def test_parse_ecosystems_arg(self) -> None:
        """
        Verify `all` expands to all four ecosystems and invalid keys raise `ValueError`.
        """
        self.assertEqual(cli.parse_ecosystems_arg("all"), ["dotnet", "python", "docker", "actions"])
        self.assertEqual(cli.parse_ecosystems_arg("dotnet,docker"), ["dotnet", "docker"])
        with self.assertRaises(ValueError):
            cli.parse_ecosystems_arg("npm")

    def test_markdown_report_rendering(self) -> None:
        """
        Verify Markdown report formatting renders cleanly even with empty results.
        """
        report = format_markdown_report([], runtime_tfm=(10, 0))
        self.assertIn("net10.0", report)
        self.assertIn("No dependency updates available", report)


class SecurityRemediationTests(unittest.TestCase):
    """
    Unit tests verifying remediation of SEC-001 (child env token stripping),
    SEC-002 (GitHubActionsUpdater tag/SHA validation and URL encoding),
    SEC-003 (HTTPS enforcement, Docker realm validation, NuGet origin checks, and token scoping),
    and SEC-004 (Markdown release_url sanitization).
    """

    def test_run_command_strips_sensitive_tokens_from_child_env(self) -> None:
        """
        Verify `BaseUpdater.run_command` strips sensitive tokens from the subprocess environment.
        """
        class _TestUpdater(BaseUpdater):
            """Minimal concrete updater for testing BaseUpdater helpers."""

            def update(self) -> EcosystemResult:
                """Return empty result."""
                return EcosystemResult(self.ecosystem)

        with tempfile.TemporaryDirectory() as tmp:
            updater = _TestUpdater(tmp)
            sensitive_vars = {
                "GITHUB_TOKEN": "secret-gh",
                "GH_TOKEN": "secret-token",
                "INPUT_GITHUB-TOKEN": "secret-action",
                "ACTIONS_RUNTIME_TOKEN": "secret-runtime",
                "ACTIONS_ID_TOKEN_REQUEST_TOKEN": "secret-oidc",
                "GIT_PUSH_TOKEN": "secret-push",
                "SAFE_PRESERVED_VAR": "expected-value",
            }
            with patch.dict(os.environ, sensitive_vars, clear=False):
                with patch("subprocess.run") as mock_run:
                    mock_run.return_value = subprocess.CompletedProcess(args=["dummy"], returncode=0, stdout="", stderr="")
                    updater.run_command(["dummy"])

                    self.assertTrue(mock_run.called)
                    passed_env = mock_run.call_args.kwargs.get("env")
                    self.assertIsNotNone(passed_env)
                    for key in [
                        "GITHUB_TOKEN",
                        "GH_TOKEN",
                        "INPUT_GITHUB-TOKEN",
                        "ACTIONS_RUNTIME_TOKEN",
                        "ACTIONS_ID_TOKEN_REQUEST_TOKEN",
                        "GIT_PUSH_TOKEN",
                    ]:
                        self.assertNotIn(key, passed_env)
                    self.assertEqual(passed_env.get("SAFE_PRESERVED_VAR"), "expected-value")

    def test_http_request_enforces_https_and_scopes_github_token(self) -> None:
        """
        Verify `BaseUpdater.http_request` rejects non-HTTPS URLs and attaches GitHub tokens
        only for ecosystem='actions' against api.github.com.
        """
        class _NonActionsUpdater(BaseUpdater):
            """Non-actions updater for testing token scoping."""

            ecosystem = "dotnet"

            def update(self) -> EcosystemResult:
                """Return empty result."""
                return EcosystemResult(self.ecosystem)

        class _ActionsUpdater(BaseUpdater):
            """Actions updater for testing token scoping."""

            ecosystem = "actions"

            def update(self) -> EcosystemResult:
                """Return empty result."""
                return EcosystemResult(self.ecosystem)

        with tempfile.TemporaryDirectory() as tmp:
            updater_dotnet = _NonActionsUpdater(tmp, github_token="gh-secret")
            # Non-HTTPS URL rejection
            with self.assertRaises(RuntimeError) as cm:
                updater_dotnet.http_request("GET", "http://api.github.com/repos/foo/bar")
            self.assertIn("Refusing non-HTTPS URL", str(cm.exception))

            # Non-hostname rejection
            with self.assertRaises(RuntimeError) as cm:
                updater_dotnet.http_request("GET", "https://")
            self.assertIn("Refusing non-HTTPS URL", str(cm.exception))

            # Non-actions ecosystem does NOT attach token even to api.github.com
            mock_resp = MagicMock()
            mock_resp.status_code = 200
            with patch.object(updater_dotnet.session, "request", return_value=mock_resp) as mock_req:
                updater_dotnet.http_request("GET", "https://api.github.com/repos/foo/bar")
                headers = mock_req.call_args.kwargs.get("headers", {})
                self.assertNotIn("Authorization", headers)

            # Actions ecosystem does NOT attach token to non-github.com host
            updater_actions = _ActionsUpdater(tmp, github_token="gh-secret")
            with patch.object(updater_actions.session, "request", return_value=mock_resp) as mock_req:
                updater_actions.http_request("GET", "https://other.service.com/api")
                headers = mock_req.call_args.kwargs.get("headers", {})
                self.assertNotIn("Authorization", headers)

            # Actions ecosystem DOES attach token to api.github.com
            with patch.object(updater_actions.session, "request", return_value=mock_resp) as mock_req:
                updater_actions.http_request("GET", "https://api.github.com/repos/foo/bar")
                headers = mock_req.call_args.kwargs.get("headers", {})
                self.assertEqual(headers.get("Authorization"), "Bearer gh-secret")
                self.assertEqual(headers.get("X-GitHub-Api-Version"), "2022-11-28")

    def test_markdown_report_sanitizes_or_omits_unsafe_release_urls(self) -> None:
        """
        Verify `format_markdown_report` sanitizes/omits unsafe `release_url` values
        such as non-HTTPS, spaces/newlines, and unescaped parens/pipes.
        """
        self.assertIsNone(_sanitize_md_url(None))
        self.assertIsNone(_sanitize_md_url(""))
        self.assertIsNone(_sanitize_md_url("   "))
        self.assertIsNone(_sanitize_md_url("http://github.com/actions/checkout/releases"))
        self.assertIsNone(_sanitize_md_url("https://github.com/actions/checkout with space"))
        self.assertIsNone(_sanitize_md_url("https://github.com/actions\ninjection"))
        self.assertIsNone(_sanitize_md_url("javascript:alert(1)"))

        # Escapes Markdown breakout characters
        escaped = _sanitize_md_url("https://github.com/org/repo/releases/tag/v1.0(alpha)|[tag]<test>\"'`")
        self.assertEqual(
            escaped,
            "https://github.com/org/repo/releases/tag/v1.0%28alpha%29%7C%5Btag%5D%3Ctest%3E%22%27%60",
        )

        # In report rendering:
        unsafe_update = DependencyUpdate(
            ecosystem="actions",
            name="actions/checkout",
            old_version="v5",
            new_version="v6",
            source_file=".github/workflows/ci.yml",
            release_url="http://insecure.org/evil",
        )
        safe_update = DependencyUpdate(
            ecosystem="actions",
            name="astral-sh/setup-uv",
            old_version="v6",
            new_version="v7",
            source_file=".github/workflows/ci.yml",
            release_url="https://github.com/astral-sh/setup-uv/releases/tag/v7(final)|[stable]",
        )
        result = EcosystemResult(ecosystem="actions", updates=[unsafe_update, safe_update])
        report = format_markdown_report([result])

        # Insecure URL should omit link and render backtick cell
        self.assertIn("`actions/checkout`", report)
        self.assertNotIn("http://insecure.org/evil", report)

        # Safe URL should escape parens, pipes, brackets
        self.assertIn(
            "[astral-sh/setup-uv](https://github.com/astral-sh/setup-uv/releases/tag/v7%28final%29%7C%5Bstable%5D)",
            report,
        )

    def test_github_actions_updater_tag_and_sha_validation_and_chain_dereferencing(self) -> None:
        """
        Verify `GitHubActionsUpdater` rejects non-40-hex SHAs, non-commit objects, and unsafe tag names,
        and resolves chained annotated tags.
        """
        # _validate_tag_name checks
        self.assertEqual(_validate_tag_name("v1.2.3"), "v1.2.3")
        self.assertEqual(_validate_tag_name("releases/v1"), "releases/v1")
        with self.assertRaises(RuntimeError):
            _validate_tag_name("..")
        with self.assertRaises(RuntimeError):
            _validate_tag_name("../escape")
        with self.assertRaises(RuntimeError):
            _validate_tag_name("tag\nnewline")
        with self.assertRaises(RuntimeError):
            _validate_tag_name("")
        with self.assertRaises(RuntimeError):
            _validate_tag_name("invalid tag with spaces")

        with tempfile.TemporaryDirectory() as tmp:
            updater = GitHubActionsUpdater(tmp)

            # Non-commit object (e.g. tag pointing to tree)
            with patch.object(
                updater,
                "http_get_json",
                return_value={"object": {"type": "tree", "sha": "a" * 40}},
            ):
                with self.assertRaises(RuntimeError) as cm:
                    updater._resolve_tag_to_commit_sha("actions", "checkout", "v4.0.0")
                self.assertIn("Failed to resolve tag", str(cm.exception))

            # Non-40-hex SHA
            with patch.object(
                updater,
                "http_get_json",
                return_value={"object": {"type": "commit", "sha": "not-a-valid-sha"}},
            ):
                with self.assertRaises(RuntimeError) as cm:
                    updater._resolve_tag_to_commit_sha("actions", "checkout", "v4.0.0")
                self.assertIn("Failed to resolve tag", str(cm.exception))

            # Invalid intermediate tag object SHA
            with patch.object(
                updater,
                "http_get_json",
                return_value={"object": {"type": "tag", "sha": "malformed-sha"}},
            ):
                with self.assertRaises(RuntimeError) as cm:
                    updater._resolve_tag_to_commit_sha("actions", "checkout", "v4.0.0")
                self.assertIn("Invalid tag object SHA", str(cm.exception))

            # Chained annotated tag dereferencing: tag -> tag -> commit
            tag1_sha = "1" * 40
            tag2_sha = "2" * 40
            commit_sha = "3" * 40

            def _mock_http_get_json(url: str) -> dict:
                """Mock GitHub API git references and tag objects."""
                if "/git/ref/tags/" in url:
                    return {"object": {"type": "tag", "sha": tag1_sha}}
                if f"/git/tags/{tag1_sha}" in url:
                    return {"object": {"type": "tag", "sha": tag2_sha}}
                if f"/git/tags/{tag2_sha}" in url:
                    return {"object": {"type": "commit", "sha": commit_sha}}
                raise ValueError(f"Unexpected URL: {url}")

            with patch.object(updater, "http_get_json", side_effect=_mock_http_get_json):
                resolved = updater._resolve_tag_to_commit_sha("actions", "checkout", "v4.0.0")
                self.assertEqual(resolved, commit_sha)

    def test_docker_updater_realm_validation_and_query_encoding(self) -> None:
        """
        Verify `DockerUpdater._fetch_bearer_token` rejects non-HTTPS, private, and loopback realm URLs,
        and properly URL-encodes service and scope query parameters.
        """
        with tempfile.TemporaryDirectory() as tmp:
            updater = DockerUpdater(tmp)

            # Rejections
            forbidden_realms = [
                "http://auth.docker.io/token",
                "https://localhost/token",
                "https://sub.localhost/token",
                "https://server.local/token",
                "https://service.internal/token",
                "https://127.0.0.1/token",
                "https://10.0.0.5/token",
                "https://192.168.1.1/token",
                "https://172.16.0.1/token",
                "https://[::1]/token",
                "https://[fe80::1]/token",
            ]
            for realm in forbidden_realms:
                challenge = f'Bearer realm="{realm}",service="registry.docker.io"'
                with self.assertRaises(RuntimeError):
                    updater._fetch_bearer_token(challenge)

            # Query parameter encoding & preservation
            valid_challenge = 'Bearer realm="https://auth.docker.io/token?base=1",service="registry docker",scope="repository:user/app:pull,push"'
            captured_urls: list[str] = []

            def _mock_get_json(url: str) -> dict:
                """Capture token URL and return dummy token."""
                captured_urls.append(url)
                return {"token": "valid-bearer-token"}

            with patch.object(updater, "http_get_json", side_effect=_mock_get_json):
                token = updater._fetch_bearer_token(valid_challenge)
                self.assertEqual(token, "valid-bearer-token")
                self.assertEqual(len(captured_urls), 1)
                req_url = captured_urls[0]
                self.assertTrue(req_url.startswith("https://auth.docker.io/token?"))
                self.assertIn("base=1", req_url)
                self.assertIn("service=registry+docker", req_url)
                self.assertIn("scope=repository%3Auser%2Fapp%3Apull%2Cpush", req_url)

    def test_dotnet_updater_catalog_origin_validation_and_url_encoding(self) -> None:
        """
        Verify `DotNetUpdater._fetch_catalog_entries` rejects non-api.nuget.org pagination URLs
        and properly URL-encodes the package ID cache key.
        """
        with tempfile.TemporaryDirectory() as tmp:
            updater = DotNetUpdater(tmp)

            # URL-encoding package ID in index URL
            with patch.object(updater, "http_get_json", return_value={"items": []}) as mock_get:
                updater._fetch_catalog_entries("Special/Package Name")
                called_url = mock_get.call_args[0][0]
                self.assertEqual(
                    called_url,
                    "https://api.nuget.org/v3/registration5-gz-semver2/special%2Fpackage%20name/index.json",
                )

            # Reject non-api.nuget.org @id URL
            untrusted_index = {
                "items": [
                    {
                        "@id": "https://evil.attacker.com/nuget/page.json",
                        "items": None,
                    }
                ]
            }
            with patch.object(updater, "http_get_json", return_value=untrusted_index):
                with self.assertRaises(RuntimeError) as cm:
                    updater._fetch_catalog_entries("Test.Package.Untrusted")
                self.assertIn("Untrusted NuGet registration page URL", str(cm.exception))

            # Reject non-HTTPS @id URL
            non_https_index = {
                "items": [
                    {
                        "@id": "http://api.nuget.org/nuget/page.json",
                        "items": None,
                    }
                ]
            }
            with patch.object(updater, "http_get_json", return_value=non_https_index):
                with self.assertRaises(RuntimeError) as cm:
                    updater._fetch_catalog_entries("Test.Package.Http")
                self.assertIn("Untrusted NuGet registration page URL", str(cm.exception))


if __name__ == "__main__":
    unittest.main()

