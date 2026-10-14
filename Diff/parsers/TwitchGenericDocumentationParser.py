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

from bs4 import BeautifulSoup
from BaseParser import BaseParser

class TwitchGenericDocumentationParser(BaseParser):
    """
    Parse a Twitch Documentation page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        """
        Parse a Twitch Documentation page from the input HTML and return a dict of parsed data

        The format of the returned dict is:
        {
            "toc": {
                header: [
                    {
                        "endpoint": header
                    }
                ]
            },
            "endpoints": {
                header: {
                    "body": body, // The stripped strings of the HTML body under this header, joined with whitespace
                    "slug": slug // The URI fragment
                },
                ...
            }
        }

        All keys are type str. All values which are present are type str

        Note that all values come from the highest level enclosing HTML tag that will support the separation required.
        All child HTML tags are stripped and the resulting strings joined with whitespace

        Args:
            html (str): The HTML from a Twitch EventSub Subscription Types page which will be parsed

        Returns:
            dict: A dict containing the parsed data, as described above
        """
        ret = {
            "toc": {},
            "endpoints": {}
        }
        soup = BeautifulSoup(html, "html5lib")
        nodes = soup.find(class_="main").find_all(class_="text-content")
        for node in nodes:
            if node.find(["h1", "h2", "h3"]) is not None:
                for child in node.children:
                    if child.name == "h1" or child.name == "h2" or child.name == "h3":
                        header = " ".join([str(x) for x in child.stripped_strings])
                        slug = "#" + str(child.attrs["id"]).strip() if "id" in child.attrs else None
                        body = ""
                        nextNode = child.find_next_sibling()
                        while nextNode != None and (nextNode.name != "h1" and nextNode.name != "h2" and nextNode.name != "h3"):
                            body += " ".join([str(x) for x in nextNode.stripped_strings]) + " "
                            nextNode = nextNode.find_next_sibling()
                        body = body.strip()
                        if header not in ret["toc"]:
                            ret["toc"][header] = []
                        ret["toc"][header].append({
                            "endpoint": header
                        })
                        ret["endpoints"][header] = {
                            "body": body,
                            "slug": slug
                        }
        return ret

if __name__ == "__main__":
    parser = TwitchGenericDocumentationParser()
    parser.main()
