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
Parser for Twitch OAuth token documentation (Authorization Code, PKCE, and Client Credentials flows).
"""

from pathlib import Path
import sys

_TWITCH_DIR = str(Path(__file__).resolve().parent)
if _TWITCH_DIR not in sys.path:
    sys.path.insert(0, _TWITCH_DIR)

from TwitchMarkdownBaseParser import TwitchMarkdownBaseParser


class TwitchOAuthParser(TwitchMarkdownBaseParser):
    """
    Parse the Twitch Authorize OAuth page into a format that can be diffed
    """

    def parse(self, data: str) -> dict:
        """
        Parse Twitch OAuth Markdown content into TOC and endpoint parameter definitions.
        """
        ret = {
            "toc": {},
            "endpoints": {},
        }
        category = "Authorize OAuth"
        ret["toc"][category] = []

        targets = {
            "Client Credentials Grant Flow": "requestBody",
            "Get the user to authorize your app (PKCE)": "requestQuery",
            "Use the authorization code to get a token (PKCE)": "requestBody",
            "Get the user to authorize your app": "requestQuery",
            "Use the authorization code to get a token": "requestBody",
        }
        targets_by_lower = {k.lower(): v for k, v in targets.items()}

        for section in self.splitSectionsMulti(data, levels=(2, 3), stop_at_higher=False):
            title = section["title"]
            target_key = targets_by_lower.get(title.lower())
            if target_key is None:
                continue

            slug = section["slug"]
            ret["toc"][category].append({"endpoint": title})

            req_data = []
            tables = self.parseMarkdownTables(section["body"])
            if tables:
                for row in tables[0]:
                    vals = list(row.values())
                    if len(vals) >= 4:
                        req_data.append({
                            "parameter": vals[0],
                            "required": vals[1],
                            "type": vals[2],
                            "description": vals[3],
                        })

            ret["endpoints"][title] = {
                "slug": slug,
                target_key: req_data,
            }

        return ret


if __name__ == "__main__":
    parser = TwitchOAuthParser()
    parser.main()
