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
Docker base image updater that automatically discovers all `Dockerfile` files,
aligns Microsoft .NET container image tags with the repository's active `<TargetFramework>`,
and pins base images to immutable multi-architecture `@sha256:...` manifest digests.
"""

import hashlib
import ipaddress
import re
import urllib.parse

from updaters.BaseUpdater import (
    BaseUpdater,
    DependencyUpdate,
    EcosystemResult,
    detect_newline_style,
    write_text_preserving_newlines,
)

_FROM_LINE_RE = re.compile(
    r"^(?P<prefix>\s*FROM\s+(?:--platform=\S+\s+)?)"
    r"(?P<image>[^\s:@]+)"
    r"(?::(?P<tag>[^\s@#]+))?"
    r"(?:@(?P<digest>sha256:[0-9a-fA-F]{64}))?"
    r"(?P<as_clause>\s+AS\s+(?P<stage>[A-Za-z0-9_.-]+))?"
    r"(?P<suffix>\s*(?:#.*)?)$",
    re.IGNORECASE,
)
_DOTNET_MCR_IMAGE_RE = re.compile(
    r"^mcr\.microsoft\.com/dotnet/(?:sdk|aspnet|runtime|runtime-deps)$",
    re.IGNORECASE,
)
_DOTNET_TAG_VERSION_RE = re.compile(r"^(?P<major>\d+)\.(?P<minor>\d+)(?P<variant>-.*)?$")
_BEARER_PARAM_RE = re.compile(r'(\w+)="([^"]*)"')
_MANIFEST_ACCEPT = (
    "application/vnd.oci.image.index.v1+json, "
    "application/vnd.docker.distribution.manifest.list.v2+json, "
    "application/vnd.oci.image.manifest.v1+json, "
    "application/vnd.docker.distribution.manifest.v2+json"
)


def parse_image_reference(image: str) -> tuple[str, str]:
    """
    Split a container image name into `(registry_host, repository_path)`.

    Args:
        image (str): Container image reference without tag or digest.

    Returns:
        tuple[str, str]: `(registry_host, repository_path)` suitable for Registry v2 API calls.
    """
    parts = image.split("/", 1)
    if len(parts) == 2 and ("." in parts[0] or ":" in parts[0] or parts[0] == "localhost"):
        return parts[0], parts[1]
    if "/" not in image:
        return "registry-1.docker.io", f"library/{image}"
    return "registry-1.docker.io", image


def align_dotnet_image_tag(image: str, tag: str, runtime_tfm: tuple[int, int] | None) -> str:
    """
    Align a `mcr.microsoft.com/dotnet/*` tag's `<major>.<minor>` prefix with the active `.NET` TFM.

    Args:
        image (str): Container image name.
        tag (str): Current image tag (for example `10.0` or `10.0-noble`).
        runtime_tfm (tuple[int, int] | None): Active `(major, minor)` runtime TFM.

    Returns:
        str: Aligned tag string.
    """
    if runtime_tfm is None or not _DOTNET_MCR_IMAGE_RE.match(image):
        return tag
    match = _DOTNET_TAG_VERSION_RE.match(tag)
    if not match:
        return tag
    variant = match.group("variant") or ""
    return f"{runtime_tfm[0]}.{runtime_tfm[1]}{variant}"


def _short_digest(digest: str) -> str:
    """
    Format a `sha256:...` digest into a concise identifier for PR display tables.

    Args:
        digest (str): Full `sha256:...` digest string.

    Returns:
        str: Truncated digest string such as `sha256:0123456789ab`.
    """
    return digest[:19] if digest.startswith("sha256:") else digest


class DockerUpdater(BaseUpdater):
    """
    Discovers Dockerfiles, resolves multi-arch OCI/Docker manifest digests via Registry v2 APIs,
    and pins `FROM` references to `<image>:<tag>@sha256:<digest>`.
    """

    ecosystem = "docker"

    def __init__(self, *args, **kwargs) -> None:
        """
        Initialize the Docker updater with an in-memory manifest digest cache.

        Args:
            *args: Positional arguments forwarded to `BaseUpdater`.
            **kwargs: Keyword arguments forwarded to `BaseUpdater`.
        """
        super().__init__(*args, **kwargs)
        self._digest_cache: dict[tuple[str, str], str] = {}

    def _fetch_bearer_token(self, www_authenticate: str) -> str | None:
        """
        Acquire an anonymous Registry v2 Bearer token from a `WWW-Authenticate` challenge header.

        Args:
            www_authenticate (str): `WWW-Authenticate` response header value.

        Returns:
            str | None: Bearer token string, or `None` if not a Bearer challenge.

        Raises:
            RuntimeError: If the Bearer realm URL is non-HTTPS, invalid, or targets a private/loopback host.
        """
        if not www_authenticate.lower().startswith("bearer "):
            return None
        params = dict(_BEARER_PARAM_RE.findall(www_authenticate))
        realm = params.get("realm")
        if not realm:
            return None

        parsed = urllib.parse.urlsplit(realm)
        if parsed.scheme.lower() != "https" or not parsed.hostname:
            raise RuntimeError(f"Refusing non-HTTPS or invalid realm URL: {realm}")

        hostname = parsed.hostname.lower()
        if (
            hostname == "localhost"
            or hostname.endswith(".localhost")
            or hostname.endswith(".local")
            or hostname.endswith(".internal")
        ):
            raise RuntimeError(f"Forbidden realm host: {parsed.hostname}")

        try:
            ip = ipaddress.ip_address(parsed.hostname)
            if (
                ip.is_private
                or ip.is_loopback
                or ip.is_link_local
                or ip.is_reserved
                or ip.is_multicast
            ):
                raise RuntimeError(f"Forbidden realm IP address: {parsed.hostname}")
        except ValueError:
            pass

        query_pairs = urllib.parse.parse_qsl(parsed.query, keep_blank_values=True)
        if params.get("service"):
            query_pairs.append(("service", params["service"]))
        if params.get("scope"):
            query_pairs.append(("scope", params["scope"]))

        new_query = urllib.parse.urlencode(query_pairs)
        token_url = urllib.parse.urlunsplit((
            parsed.scheme,
            parsed.netloc,
            parsed.path,
            new_query,
            parsed.fragment,
        ))
        data = self.http_get_json(token_url)
        if isinstance(data, dict):
            return data.get("token") or data.get("access_token")
        return None

    def _resolve_manifest_digest(self, image: str, tag: str) -> str:
        """
        Resolve the current multi-arch manifest list / OCI index `sha256:` digest for `image:tag`.

        Args:
            image (str): Container image name.
            tag (str): Image tag (for example `10.0`).

        Returns:
            str: Immutable `sha256:...` digest string.

        Raises:
            RuntimeError: If the registry query fails or returns no valid digest.
        """
        cache_key = (image.lower(), tag)
        if cache_key in self._digest_cache:
            return self._digest_cache[cache_key]

        registry, repository = parse_image_reference(image)
        url = f"https://{registry}/v2/{repository}/manifests/{tag}"
        headers = {"Accept": _MANIFEST_ACCEPT}

        resp = self.http_request("HEAD", url, headers=headers, allow_status=(200, 401, 405))
        if resp.status_code == 401:
            token = self._fetch_bearer_token(resp.headers.get("WWW-Authenticate", ""))
            if not token:
                raise RuntimeError(f"Registry {registry} returned 401 without a usable Bearer realm.")
            headers["Authorization"] = f"Bearer {token}"
            resp = self.http_request("HEAD", url, headers=headers, allow_status=(200, 405))

        if resp.status_code == 405 or not resp.headers.get("Docker-Content-Digest"):
            resp = self.http_request("GET", url, headers=headers, allow_status=(200,))

        digest = resp.headers.get("Docker-Content-Digest", "").strip().lower()
        if not digest:
            digest = f"sha256:{hashlib.sha256(resp.content).hexdigest()}"

        if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
            raise RuntimeError(f"Invalid digest '{digest}' returned for {image}:{tag}")

        self._digest_cache[cache_key] = digest
        return digest

    def update(self) -> EcosystemResult:
        """
        Discover all Dockerfiles in the repository and update/pin their `FROM` base images.

        Returns:
            EcosystemResult: Summary of pinned/updated Docker base images and errors.
        """
        result = EcosystemResult(ecosystem=self.ecosystem)
        runtime_tfm = self.discover_dotnet_runtime()
        dockerfiles = self.discover_files("Dockerfile", "*.Dockerfile")

        for dockerfile_path in dockerfiles:
            rel = self.rel_path(dockerfile_path)
            newline = detect_newline_style(dockerfile_path)
            with open(dockerfile_path, "r", encoding="utf-8", newline="") as handle:
                original_text = handle.read()

            lines = original_text.replace("\r\n", "\n").split("\n")
            known_stages: set[str] = {"scratch"}
            new_lines: list[str] = []
            file_changed = False

            for line in lines:
                match = _FROM_LINE_RE.match(line)
                if not match:
                    new_lines.append(line)
                    continue

                image = match.group("image")
                stage = match.group("stage")
                old_tag = match.group("tag")
                old_digest = match.group("digest")

                if image.lower() in known_stages and old_tag is None and old_digest is None:
                    if stage:
                        known_stages.add(stage.lower())
                    new_lines.append(line)
                    continue

                if stage:
                    known_stages.add(stage.lower())

                effective_old_tag = old_tag or "latest"
                target_tag = align_dotnet_image_tag(image, effective_old_tag, runtime_tfm)

                try:
                    new_digest = self._resolve_manifest_digest(image, target_tag)
                except RuntimeError as exc:
                    result.errors.append(f"{rel} ({image}:{target_tag}): {exc}")
                    new_lines.append(line)
                    continue

                as_clause = match.group("as_clause") or ""
                suffix = match.group("suffix") or ""
                updated_line = f"{match.group('prefix')}{image}:{target_tag}@{new_digest}{as_clause}{suffix}"

                if updated_line != line:
                    file_changed = True
                    old_display = (
                        f"{effective_old_tag}@{_short_digest(old_digest)}"
                        if old_digest
                        else f"{effective_old_tag} (unpinned)"
                    )
                    new_display = f"{target_tag}@{_short_digest(new_digest)}"
                    result.updates.append(
                        DependencyUpdate(
                            ecosystem=self.ecosystem,
                            name=image,
                            old_version=old_display,
                            new_version=new_display,
                            source_file=rel,
                            notes="multi-arch OCI digest pin",
                        )
                    )

                new_lines.append(updated_line)

            if file_changed:
                if rel not in result.modified_files:
                    result.modified_files.append(rel)
                if not self.dry_run:
                    write_text_preserving_newlines(dockerfile_path, "\n".join(new_lines), newline)

        return result

