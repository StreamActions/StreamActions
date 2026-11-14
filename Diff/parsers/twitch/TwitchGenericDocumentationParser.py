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


class TwitchGenericDocumentationParser(TwitchMarkdownBaseParser):
    """
    Parse a Twitch Documentation page into a format that can be diffed
    """

    def parse(self, data: str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {},
        }

        for section in self.splitSectionsMulti(data, levels=(1, 2, 3), stop_at_higher=False):
            header = section["title"]
            body = self.stripMarkdown(section["body"])
            slug = section["slug"]

            if header not in ret["toc"]:
                ret["toc"][header] = []
            ret["toc"][header].append({"endpoint": header})
            ret["endpoints"][header] = {
                "body": body,
                "slug": slug,
            }

        return ret


if __name__ == "__main__":
    parser = TwitchGenericDocumentationParser()
    parser.main()
