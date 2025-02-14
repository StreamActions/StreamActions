# coding=utf-8
#
# This file is part of StreamActions.
# Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

class TwitchScopesParser(BaseParser):
    """
    Parse a Twitch API Scope page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        """
        Parse a Twitch API Scope page from the input HTML and return a dict of parsed data

        The format of the returned dict is:
        {
            "toc": {
                category: [ // Name of category from H2 tag
                    {
                        "endpoint": scopeName // Scope
                    }
                ]
            },
            "endpoints": {
                scopeName: { // Scope from the 1st column of the table
                    "description": description, // Description. Typically the first line in the 2nd column of the table
                    "api": [
                        endpoint, // Names of Twitch API Reference endpoints after the `API` strong tag in the 2nd column of the table
                        ...
                    ],
                    "eventsub": [
                        topic, // Names of EventSub topics after the `EventSub` strong tag in the 2nd column of the table
                        ...
                    ]
                },
                ...
            }
        }

        Note that in the "endpoints" sub-dict that any value which is not present in the documentation for a particular endpoint will be present, but set to None (or possibly [] for lists).
        All keys are type str. All values which are present are type str

        Note that all values come from the highest level enclosing HTML tag that will support the separation required.
        All child HTML tags are stripped and the resulting strings joined with whitespace

        Args:
            html (str): The HTML from a Twitch API Scope page which will be parsed

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
            if node.find("h1", id="twitch-access-token-scopes") != None:
                docs = node.find_all("h2")
                for doc in docs:
                    category = str(doc.string).strip()
                    if category not in ret["toc"]:
                        ret["toc"][category] = []
                    table = doc.find_next_sibling("table")
                    for entry in table.find("tbody").find_all("tr"):
                        cells = entry.find_all("td")
                        endpoint = str(cells[0].string).strip()
                        data = cells[1].stripped_strings
                        section = "description"
                        out = {
                            "description": [],
                            "api": [],
                            "eventsub": []
                        }
                        for string in data:
                            string = str(string).strip()
                            if string == "API":
                                section = "api"
                            elif string == "EventSub":
                                section = "eventsub"
                            elif len(string) > 0:
                                out[section].append(string)
                        ret["toc"][category].append({
                            "endpoint": endpoint
                        })
                        ret["endpoints"][endpoint] = {
                            "description": " ".join(out["description"]).strip(),
                            "api": out["api"],
                            "eventsub": out["eventsub"]
                        }
        return ret

if __name__ == "__main__":
    parser = TwitchScopesParser()
    parser.main()