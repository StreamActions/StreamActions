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

from pathlib import Path
import re
import sys

_PARSERS_DIR = str(Path(__file__).resolve().parent.parent)
_TWITCH_DIR = str(Path(__file__).resolve().parent)
if _PARSERS_DIR not in sys.path:
    sys.path.insert(0, _PARSERS_DIR)
if _TWITCH_DIR not in sys.path:
    sys.path.insert(0, _TWITCH_DIR)

from BaseParser import BaseParser


class TwitchMarkdownBaseParser(BaseParser):
    """
    Base class for Twitch Markdown (.md) documentation parsers
    """

    _HEADING_RE = re.compile(r"^(#{1,6})\s+(.+?)(?:\s+\{#([a-zA-Z0-9_-]+)\})?\s*$")
    _LINK_RE = re.compile(r"\[([^\]]+)\]\([^)]+\)")
    _FIELD_BULLET_RE = re.compile(
        r"^(\s*)-\s+`([^`]+)`\s*(?:(?:—|–|-)\s*)?([^.\n]+?)(?:\.\s*(.*))?$"
    )
    _CODE_BULLET_RE = re.compile(
        r"^-\s+\*\*([^*]+)\*\*(?:\s*(?:(?:—|–|-)\s*)?(.*))?$"
    )

    @staticmethod
    def toMarkdownUrl(url: str) -> str:
        """
        Convert a Twitch HTML documentation URL to its corresponding .md URL.
        Strips any trailing '/' before appending '.md'. Preserves any '#fragment'.
        """
        base, sep, frag = url.partition("#")
        base = base.rstrip("/")
        if not base.endswith(".md"):
            base += ".md"
        return f"{base}{sep}{frag}" if sep else base

    def parseFromUrl(self, url: str, out: str = None) -> dict:
        """
        Resolve the Twitch HTML documentation URL to its .md equivalent and parse it.
        """
        return super().parseFromUrl(self.toMarkdownUrl(url), out)

    @staticmethod
    def slugify(title: str) -> str:
        """
        Generate a URL fragment anchor from a Markdown heading title.
        """
        clean = TwitchMarkdownBaseParser.stripMarkdown(title).lower()
        clean = re.sub(r"[^a-z0-9\s_-]", "", clean)
        clean = re.sub(r"[\s]+", "-", clean.strip())
        return f"#{clean}" if clean else ""

    @classmethod
    def parseHeading(cls, line: str) -> tuple[int, str, str] | None:
        """
        Parse a Markdown heading line, returning (level, title, slug) or None.
        """
        m = cls._HEADING_RE.match(line)
        if not m:
            return None
        level = len(m.group(1))
        title = cls.stripMarkdown(m.group(2))
        slug = f"#{m.group(3)}" if m.group(3) else cls.slugify(title)
        return (level, title, slug)

    @classmethod
    def splitSections(cls, md: str, level: int = 2) -> list[dict]:
        """
        Split Markdown text into sections at the specified heading level,
        ignoring headings inside fenced code blocks.
        """
        return cls.splitSectionsMulti(md, levels=(level,), stop_at_higher=True)

    @classmethod
    def splitSectionsMulti(
        cls,
        md: str,
        levels: tuple[int, ...] = (2, 3),
        stop_at_higher: bool = True,
    ) -> list[dict]:
        """
        Split Markdown text into sections at any heading in `levels`,
        ignoring headings inside fenced code blocks.
        """
        sections: list[dict] = []
        current: dict | None = None
        body_lines: list[str] = []
        in_code = False
        min_level = min(levels)

        for line in md.splitlines():
            stripped = line.lstrip()
            if stripped.startswith("```"):
                in_code = not in_code
                if current is not None:
                    body_lines.append(line)
                continue

            if not in_code:
                parsed = cls.parseHeading(line)
                if parsed is not None:
                    h_level, title, slug = parsed
                    if h_level in levels:
                        if current is not None:
                            current["body"] = "\n".join(body_lines).strip()
                            sections.append(current)
                        current = {
                            "level": h_level,
                            "title": title,
                            "slug": slug,
                            "body": "",
                        }
                        body_lines = []
                        continue
                    if stop_at_higher and h_level < min_level and current is not None:
                        current["body"] = "\n".join(body_lines).strip()
                        sections.append(current)
                        current = None
                        body_lines = []
                        continue

            if current is not None:
                body_lines.append(line)

        if current is not None:
            current["body"] = "\n".join(body_lines).strip()
            sections.append(current)

        return sections

    @classmethod
    def stripMarkdown(cls, text: str) -> str:
        """
        Strip inline Markdown formatting and normalize whitespace.
        """
        s = cls._LINK_RE.sub(r"\1", text)
        s = s.replace("**", "").replace("__", "").replace("`", "")
        s = s.replace("\\_", "_")
        s = re.sub(r"(?<!\w)\*([^*]+)\*(?!\w)", r"\1", s)
        s = re.sub(r"(?<!\w)_([^_]+)_(?!\w)", r"\1", s)
        return " ".join(s.split()).strip()

    @staticmethod
    def extractCodeBlocks(text: str) -> list[tuple[str, str]]:
        """
        Extract fenced code blocks as (language, content) tuples.
        """
        blocks: list[tuple[str, str]] = []
        in_code = False
        lang = ""
        lines: list[str] = []

        for line in text.splitlines():
            stripped = line.lstrip()
            if stripped.startswith("```"):
                if not in_code:
                    in_code = True
                    lang = stripped[3:].strip().lower()
                    lines = []
                else:
                    in_code = False
                    blocks.append((lang, "\n".join(lines).strip()))
                    lang = ""
                    lines = []
            elif in_code:
                lines.append(line)

        return blocks

    @classmethod
    def parseMarkdownTables(cls, text: str) -> list[list[dict[str, str]]]:
        """
        Parse all GFM Markdown tables in `text` into lists of row dicts keyed by header name.
        Handles tables with or without trailing pipe characters.
        """
        tables: list[list[dict[str, str]]] = []
        lines = text.splitlines()
        i = 0
        in_code = False

        while i < len(lines):
            line = lines[i]
            if line.lstrip().startswith("```"):
                in_code = not in_code
                i += 1
                continue

            if not in_code and line.lstrip().startswith("|") and i + 1 < len(lines):
                sep_line = lines[i + 1].strip()
                if sep_line.startswith("|") and re.match(r"^\|[\s:|-]+\|?\s*$", sep_line):
                    headers = [cls.stripMarkdown(c) for c in cls._splitTableRow(line)]
                    rows: list[dict[str, str]] = []
                    i += 2
                    while i < len(lines) and lines[i].lstrip().startswith("|"):
                        cells = [cls.stripMarkdown(c) for c in cls._splitTableRow(lines[i])]
                        if cells:
                            row = {}
                            for idx, h in enumerate(headers):
                                row[h] = cells[idx] if idx < len(cells) else ""
                            rows.append(row)
                        i += 1
                    tables.append(rows)
                    continue
            i += 1

        return tables

    @staticmethod
    def _splitTableRow(line: str) -> list[str]:
        s = line.strip()
        if s.startswith("|"):
            s = s[1:]
        if s.endswith("|"):
            s = s[:-1]
        return [cell.strip() for cell in s.split("|")]

    @classmethod
    def parseFieldBullets(
        cls,
        text: str,
        name_key: str = "field",
        include_required: bool = True,
    ) -> list[dict]:
        """
        Parse Twitch Markdown bulleted field definitions:
        `- `field_name` — Type, **Required**. Description.`
        """
        fields: list[dict] = []
        current: dict | None = None
        desc_parts: list[str] = []
        in_code = False

        def flush_current():
            if current is not None:
                current["description"] = cls.stripMarkdown(" ".join(desc_parts))
                fields.append(current)

        for line in text.splitlines():
            if line.lstrip().startswith("```"):
                in_code = not in_code
                continue
            if in_code:
                continue

            m = cls._FIELD_BULLET_RE.match(line)
            if m:
                flush_current()
                field_name = m.group(2).strip()
                type_part = m.group(3).strip()
                inline_desc = (m.group(4) or "").strip()

                required_val = "No"
                if re.search(r"\*\*Required\*\*|\bRequired\b", type_part, re.IGNORECASE):
                    required_val = "Yes"
                    type_part = re.sub(
                        r",?\s*\*\*Required\*\*|,?\s*\bRequired\b",
                        "",
                        type_part,
                        flags=re.IGNORECASE,
                    ).strip(" ,")

                current = {
                    name_key: field_name,
                    "type": cls.stripMarkdown(type_part),
                }
                if include_required:
                    current["required"] = required_val
                desc_parts = [inline_desc] if inline_desc else []
            elif current is not None:
                stripped = line.strip()
                if stripped:
                    if stripped.startswith("- "):
                        desc_parts.append(stripped[2:].strip())
                    else:
                        desc_parts.append(stripped)

        flush_current()
        return fields

    @classmethod
    def parseResponseCodes(cls, text: str) -> list[dict]:
        """
        Parse Twitch Markdown response code bullets:
        `- **200 OK** — Description.`
        """
        codes: list[dict] = []
        current: dict | None = None
        desc_parts: list[str] = []
        in_code = False

        def flush_current():
            if current is not None:
                current["description"] = cls.stripMarkdown(" ".join(desc_parts))
                codes.append(current)

        for line in text.splitlines():
            if line.lstrip().startswith("```"):
                in_code = not in_code
                continue
            if in_code:
                continue

            m = cls._CODE_BULLET_RE.match(line)
            if m:
                flush_current()
                current = {"code": cls.stripMarkdown(m.group(1))}
                inline_desc = (m.group(2) or "").strip()
                desc_parts = [inline_desc] if inline_desc else []
            elif current is not None:
                stripped = line.strip()
                if stripped:
                    if stripped.startswith("- "):
                        desc_parts.append(stripped[2:].strip())
                    else:
                        desc_parts.append(stripped)

        flush_current()
        return codes
