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


class TwitchScopesParser(TwitchMarkdownBaseParser):
    """
    Parse a Twitch API Scope page into a format that can be diffed
    """

    _SCOPE_RE = re.compile(r"^-\s+`([^`]+)`\s*(?:(?:—|–|-)\s*)?(.*)$")

    def parse(self, data: str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {},
        }

        for section in self.splitSections(data, level=2):
            category = section["title"]
            current_scope = None

            for line in section["body"].splitlines():
                m = self._SCOPE_RE.match(line)
                if m:
                    current_scope = m.group(1).strip()
                    desc = self.stripMarkdown(m.group(2))
                    if category not in ret["toc"]:
                        ret["toc"][category] = []
                    ret["toc"][category].append({"endpoint": current_scope})
                    ret["endpoints"][current_scope] = {
                        "description": desc,
                        "api": [],
                        "eventsub": [],
                    }
                elif current_scope is not None:
                    stripped = line.strip()
                    if stripped.startswith("- Twitch API:"):
                        items = stripped[len("- Twitch API:"):].strip()
                        ret["endpoints"][current_scope]["api"] = [
                            self.stripMarkdown(x) for x in items.split(",") if x.strip()
                        ]
                    elif stripped.startswith("- EventSub:"):
                        items = stripped[len("- EventSub:"):].strip()
                        ret["endpoints"][current_scope]["eventsub"] = [
                            self.stripMarkdown(x) for x in items.split(",") if x.strip()
                        ]

        return ret


if __name__ == "__main__":
    parser = TwitchScopesParser()
    parser.main()
