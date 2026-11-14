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
import sys

_TWITCH_DIR = str(Path(__file__).resolve().parent)
if _TWITCH_DIR not in sys.path:
    sys.path.insert(0, _TWITCH_DIR)

from TwitchMarkdownBaseParser import TwitchMarkdownBaseParser


class TwitchRefreshParser(TwitchMarkdownBaseParser):
    """
    Parse the Twitch Refresh OAuth page into a format that can be diffed
    """

    def parse(self, data: str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {},
        }
        category = "Refresh OAuth"
        ret["toc"][category] = []

        for section in self.splitSections(data, level=2):
            if section["title"] == "How to use a refresh token":
                endpoint = "Refresh OAuth"
                ret["toc"][category].append({"endpoint": endpoint})

                req_body = []
                tables = self.parseMarkdownTables(section["body"])
                if tables:
                    for row in tables[0]:
                        vals = list(row.values())
                        if len(vals) >= 4:
                            req_body.append({
                                "parameter": vals[0],
                                "required": vals[1],
                                "type": vals[2],
                                "description": vals[3],
                            })

                ret["endpoints"][endpoint] = {
                    "slug": section["slug"],
                    "requestBody": req_body,
                }

        return ret


if __name__ == "__main__":
    parser = TwitchRefreshParser()
    parser.main()
