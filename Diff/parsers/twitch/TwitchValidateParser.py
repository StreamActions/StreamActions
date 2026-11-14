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


class TwitchValidateParser(TwitchMarkdownBaseParser):
    """
    Parse the Twitch Validate OAuth page into a format that can be diffed
    """

    def parse(self, data: str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {},
        }
        category = "Validate OAuth"
        ret["toc"][category] = []

        for section in self.splitSections(data, level=2):
            if section["title"] == "How to validate a token":
                endpoint = "Validate OAuth"
                ret["toc"][category].append({"endpoint": endpoint})

                res_body = []
                tables = self.parseMarkdownTables(section["body"])
                if tables:
                    for row in tables[0]:
                        vals = list(row.values())
                        if len(vals) >= 3:
                            res_body.append({
                                "field": vals[0],
                                "type": vals[1],
                                "description": vals[2],
                            })

                ret["endpoints"][endpoint] = {
                    "slug": section["slug"],
                    "responseBody": res_body,
                }

        return ret


if __name__ == "__main__":
    parser = TwitchValidateParser()
    parser.main()
