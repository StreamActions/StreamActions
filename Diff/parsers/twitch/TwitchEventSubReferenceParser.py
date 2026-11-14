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

_TWITCH_DIR = str(Path(__file__).resolve().parent)
if _TWITCH_DIR not in sys.path:
    sys.path.insert(0, _TWITCH_DIR)

from TwitchMarkdownBaseParser import TwitchMarkdownBaseParser


class TwitchEventSubReferenceParser(TwitchMarkdownBaseParser):
    """
    Parse the Twitch EventSub Reference index and its category sub-pages into a format that can be diffed
    """

    _INDEX_ITEM_RE = re.compile(
        r"^-\s+\[([^\]]+)\]\(([^)#\s]+\.md)(#[^)\s]+)?\)(?::\s*(.*))?$"
    )

    def parse(self, data: str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {},
        }
        subpages: list[tuple[str, str]] = []
        seen_urls: set[str] = set()

        for section in self.splitSections(data, level=2):
            category = section["title"]
            if category.lower() == "contents":
                continue

            for line in section["body"].splitlines():
                m = self._INDEX_ITEM_RE.match(line.strip())
                if m:
                    ep_name = self.stripMarkdown(m.group(1))
                    sub_url = m.group(2).strip()
                    desc = self.stripMarkdown(m.group(4) or "")
                    if category not in ret["toc"]:
                        ret["toc"][category] = []
                    entry = {"endpoint": ep_name}
                    if desc:
                        entry["description"] = desc
                    ret["toc"][category].append(entry)

                    if sub_url not in seen_urls:
                        seen_urls.add(sub_url)
                        cat_slug = sub_url.rstrip("/").rsplit("/", 1)[-1].removesuffix(".md")
                        subpages.append((cat_slug, sub_url))

        if subpages:
            for cat_slug, sub_url in subpages:
                sub_md = self.fetchUrl(sub_url)
                self._parseSubpage(sub_md, cat_slug, ret)
        else:
            self._parseSubpage(data, "", ret)

        return ret

    def _parseSubpage(self, md: str, cat_slug: str, ret: dict) -> None:
        for section in self.splitSections(md, level=2):
            name = section["title"]
            if name.lower() == "contents":
                continue

            slug = f"{cat_slug}/{section['slug']}" if cat_slug else section["slug"]
            body = section["body"]
            sub_sections = self.splitSections(body, level=3)

            pre_body = body.split("\n### ", 1)[0].strip() if "\n### " in body else (
                "" if body.lstrip().startswith("### ") else body.strip()
            )
            description = self.stripMarkdown(pre_body) or None
            subscription = None
            url = None
            authorization = None
            request_body = None
            event_payload = None
            example = None

            for sub in sub_sections:
                title_lower = sub["title"].lower()
                sub_body = sub["body"]

                if title_lower == "subscription":
                    subscription = self.stripMarkdown(sub_body) or None
                elif title_lower == "endpoint":
                    url = self.stripMarkdown(sub_body) or None
                elif title_lower in ("authorization", "authentication"):
                    authorization = self.stripMarkdown(sub_body) or None
                elif title_lower == "subscription request body":
                    request_body = self.parseFieldBullets(sub_body, name_key="name", include_required=True)
                elif title_lower == "event payload":
                    event_payload = self.parseFieldBullets(sub_body, name_key="name", include_required=False)
                elif "example" in title_lower:
                    blocks = self.extractCodeBlocks(sub_body)
                    if blocks:
                        example = blocks[0][1]
                    else:
                        example = self.stripMarkdown(sub_body) or None

            ret["endpoints"][name] = {
                "description": description,
                "slug": slug,
                "subscription": subscription,
                "url": url,
                "authorization": authorization,
                "requestBody": request_body,
                "eventPayload": event_payload,
                "example": example,
            }


if __name__ == "__main__":
    parser = TwitchEventSubReferenceParser()
    parser.main()
