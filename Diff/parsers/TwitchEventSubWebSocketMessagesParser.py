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

class TwitchEventSubWebSocketMessagesParser(BaseParser):
    """
    Parse a Twitch EventSub WebSocket Messages page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        """
        Parse a Twitch EventSub WebSocket Messages page from the input HTML and return a dict of parsed data

        The format of the returned dict is:
        {
            "toc": {
                messageName: [ // Message name from H2 tag
                    {
                        "endpoint": messageName // Message name from H2 tag
                    }
                ]
            },
            "endpoints": {
                messageName: { // Message name
                    "description": description, // Description, if available. Typically the text from the first paragraph after the H2 tag for the message name
                    "slug": slug, // The URI fragment
                    "fields": [
                        {
                            "name": fieldName, // Name of the field. For close message, the code
                            "type": fieldType, // Type of the field. For close message, the reason text associated with the code
                            "description": fieldDescription // Description of the field, if available. For close message, the notes on the codes meaning
                        },
                        ...
                    ]
                },
                ...
            }
        }

        All keys are type str. All values which are present are type str

        Note that all values come from the highest level enclosing HTML tag that will support the separation required.
        All child HTML tags are stripped and the resulting strings joined with whitespace

        Args:
            html (str): The HTML from a Twitch EventSub WebSocket Messages page which will be parsed

        Returns:
            dict: A dict containing the parsed data, as described above
        """
        ret = {
            "toc": {},
            "endpoints": {}
        }
        soup = BeautifulSoup(html, "html.parser")
        nodes = soup.find(class_="main").find_all(class_="text-content")
        for node in nodes:
            if node.find("h1", id="websocket-messages") != None:
                docs = node.find_all("h2")
                for doc in docs:
                    category = str(doc.string).strip()
                    if category not in ret["toc"]:
                        ret["toc"][category] = []
                    nextsibling = doc.find_next_sibling()
                    fields = []
                    description = None
                    if nextsibling and nextsibling.name == "p":
                        description = " ".join([str(x) for x in nextsibling.stripped_strings])
                        nextsibling = nextsibling.find_next_sibling()
                        while nextsibling and nextsibling.name == "p":
                            nextsibling = nextsibling.find_next_sibling()
                    if nextsibling and nextsibling.name == "table":
                        for entry in nextsibling.find("tbody").find_all("tr"):
                            cells = entry.find_all("td")
                            out = {
                                "name": str(cells[0].string).strip(),
                                "type": str(cells[1].string).strip(),
                                "description": " ".join([str(x) for x in cells[2].stripped_strings])
                            }
                            fields.append(out)
                    ret["toc"][category].append({
                        "endpoint": category
                    })
                    ret["endpoints"][category] = {
                        "description": description if description else None,
                        "slug": "#" + str(doc.attrs["id"]).strip() if "id" in doc.attrs else None,
                        "fields": fields
                    }
        return ret

if __name__ == "__main__":
    parser = TwitchEventSubWebSocketMessagesParser()
    parser.main()