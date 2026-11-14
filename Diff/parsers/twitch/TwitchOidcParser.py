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


class TwitchOidcParser(TwitchMarkdownBaseParser):
    """
    Parse the Twitch OIDC UserInfo page into a format that can be diffed
    """

    def parse(self, data: str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {},
        }
        category = "OIDC UserInfo"
        ret["toc"][category] = []

        targets_tables = {
            "Get the user to authorize your app": "requestQuery",
            "Use the authorization code to get a token": "requestBody",
        }

        for section in self.splitSectionsMulti(data, levels=(2, 3), stop_at_higher=False):
            title = section["title"]
            slug = section["slug"]

            if title in targets_tables:
                endpoint = f"OIDC {title}"
                ret["toc"][category].append({"endpoint": endpoint})

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

                ret["endpoints"][endpoint] = {
                    "slug": slug,
                    targets_tables[title]: req_data,
                }

            elif title == "Getting claims information from an access token":
                endpoint = "UserInfo"
                ret["toc"][category].append({"endpoint": endpoint})

                ex_req_curl = None
                ex_res = None
                for _, block in self.extractCodeBlocks(section["body"]):
                    if block.lstrip().startswith("curl") and ex_req_curl is None:
                        ex_req_curl = block
                    elif block.lstrip().startswith("{") and ex_res is None:
                        ex_res = block

                ret["endpoints"][endpoint] = {
                    "slug": slug,
                    "exampleRequestCurl": ex_req_curl,
                    "exampleResponse": ex_res,
                }

        return ret


if __name__ == "__main__":
    parser = TwitchOidcParser()
    parser.main()
