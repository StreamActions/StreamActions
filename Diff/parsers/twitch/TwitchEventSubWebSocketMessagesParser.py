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


class TwitchEventSubWebSocketMessagesParser(TwitchMarkdownBaseParser):
    """
    Parse a Twitch EventSub WebSocket Messages page into a format that can be diffed
    """

    def parse(self, data: str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {},
        }

        for section in self.splitSections(data, level=2):
            category = section["title"]
            slug = section["slug"]
            body = section["body"]
            sub_sections = self.splitSections(body, level=3)

            pre_body = body.split("\n### ", 1)[0].strip() if "\n### " in body else (
                "" if body.lstrip().startswith("### ") else body.strip()
            )
            description = self.stripMarkdown(pre_body) or None
            message_type = None
            fields: list[dict] = []
            example = None

            for sub in sub_sections:
                title_lower = sub["title"].lower()
                sub_body = sub["body"]

                if title_lower == "message type":
                    message_type = self.stripMarkdown(sub_body) or None
                elif title_lower == "message fields":
                    fields = self.parseFieldBullets(sub_body, name_key="name", include_required=False)
                    if not fields:
                        tables = self.parseMarkdownTables(sub_body)
                        if tables:
                            for row in tables[0]:
                                vals = list(row.values())
                                if len(vals) >= 3:
                                    fields.append({
                                        "name": vals[0],
                                        "type": vals[1],
                                        "description": vals[2],
                                    })
                elif title_lower == "message example":
                    blocks = self.extractCodeBlocks(sub_body)
                    if blocks:
                        example = blocks[0][1]

            if not fields:
                tables = self.parseMarkdownTables(body)
                if tables:
                    for row in tables[0]:
                        vals = list(row.values())
                        if len(vals) >= 3:
                            fields.append({
                                "name": vals[0],
                                "type": vals[1],
                                "description": vals[2],
                            })

            if category not in ret["toc"]:
                ret["toc"][category] = []
            ret["toc"][category].append({"endpoint": category})

            ret["endpoints"][category] = {
                "description": description,
                "slug": slug,
                "messageType": message_type,
                "fields": fields,
                "example": example,
            }

        return ret


if __name__ == "__main__":
    parser = TwitchEventSubWebSocketMessagesParser()
    parser.main()
