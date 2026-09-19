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

class TwitchEventSubReferenceParser(BaseParser):
    """
    Parse a Twitch EventSub Reference page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        """
        Parse a Twitch EventSub Reference page from the input HTML and return a dict of parsed data

        The format of the returned dict is:
        {
            "toc": {
                category: [ // Second lowest level header tag
                    {
                        "endpoint": objectName // Lowest level header tag
                    }
                ]
            },
            "endpoints": {
                objectName: { // Lowest level header tag
                    "description": description, // Description
                    "slug": slug, // The URI fragment
                    "fields": [
                        {
                            "name": name, // Field name
                            "type": type, // Field type
                            "description": description, // Field description
                            "required": required // For fields in the request body, whether the field is required or not. Not present for fields in the response body
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
            html (str): The HTML from a Twitch EventSub Reference page which will be parsed

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
            if node.find("h1") != None:
                headers = {
                    "h1": None,
                    "h2": None,
                    "h3": None
                }
                currentHeader = 0
                for child in node.children:
                    objectDescription = None
                    if child.name in headers:
                        currentHeader = int(child.name[1])
                        headers[child.name] = {
                            "name": child.string.strip() if child.string != None else None,
                            "slug": ("#" + child.attrs["id"]) if "id" in child.attrs else None
                        }
                    elif child.name == "p" and currentHeader > 1:
                        objectDescription = " ".join([str(x) for x in child.stripped_strings])
                        if headers["h" + str(currentHeader - 1)]["name"] not in ret["toc"]:
                            ret["toc"][headers["h" + str(currentHeader - 1)]["name"]] = []
                        inToc = False
                        for entry in ret["toc"][headers["h" + str(currentHeader - 1)]["name"]]:
                            if entry["endpoint"] == headers["h" + str(currentHeader)]["name"]:
                                inToc = True
                                break
                        if not inToc:
                            ret["toc"][headers["h" + str(currentHeader - 1)]["name"]].append({
                                "endpoint": headers["h" + str(currentHeader)]["name"]
                            })
                        if headers["h" + str(currentHeader)]["name"] not in ret["endpoints"]:
                            ret["endpoints"][headers["h" + str(currentHeader)]["name"]] = {
                                "description": objectDescription,
                                "slug": headers["h" + str(currentHeader)]["slug"],
                                "fields": []
                            }
                        elif ret["endpoints"][headers["h" + str(currentHeader)]["name"]]["description"] is None:
                            ret["endpoints"][headers["h" + str(currentHeader)]["name"]]["description"] = objectDescription
                        else:
                            ret["endpoints"][headers["h" + str(currentHeader)]["name"]]["description"] += " " + objectDescription
                    elif child.name == "table" and currentHeader > 1:
                        if headers["h" + str(currentHeader - 1)]["name"] not in ret["toc"]:
                            ret["toc"][headers["h" + str(currentHeader - 1)]["name"]] = []
                        inToc = False
                        for entry in ret["toc"][headers["h" + str(currentHeader - 1)]["name"]]:
                            if entry["endpoint"] == headers["h" + str(currentHeader)]["name"]:
                                inToc = True
                                break
                        if not inToc:
                            ret["toc"][headers["h" + str(currentHeader - 1)]["name"]].append({
                                "endpoint": headers["h" + str(currentHeader)]["name"]
                            })
                        if headers["h" + str(currentHeader)]["name"] not in ret["endpoints"]:
                            ret["endpoints"][headers["h" + str(currentHeader)]["name"]] = {
                                "description": None,
                                "slug": headers["h" + str(currentHeader)]["slug"],
                                "fields": []
                            }
                        table = child
                        docs = table.find("tbody").find_all("tr")
                        for doc in docs:
                            cells = doc.find_all("td")
                            name = str(cells[0].string).strip()
                            type_ = str(cells[1].string).strip()
                            required = str(cells[2].string).strip() if len(cells) > 3 else None
                            description = " ".join([str(x) for x in cells[3 if len(cells) > 3 else 2].stripped_strings])
                            obj = {
                                "name": name,
                                "type": type_,
                                "description": description
                            }
                            if required is not None:
                                obj["required"] = required
                            ret["endpoints"][headers["h" + str(currentHeader)]["name"]]["fields"].append(obj)
        return ret

if __name__ == "__main__":
    parser = TwitchEventSubReferenceParser()
    parser.main()