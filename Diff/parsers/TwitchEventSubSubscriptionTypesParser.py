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

class TwitchEventSubSubscriptionTypesParser(BaseParser):
    """
    Parse a Twitch EventSub Subscription Types page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        """
        Parse a Twitch EventSub Subscription Types page from the input HTML and return a dict of parsed data

        The format of the returned dict is:
        {
            "toc": {
                subscriptionType: [ // Subscription type from head table
                    {
                        "endpoint": eventName + " v" + version // Event name and version from head table
                    }
                ]
            },
            "endpoints": {
                eventName + " v" + version: { // Event name and version from head table
                    "title": subscriptionType, // Subscription type from head table
                    "event": eventName, // Event name from head table
                    "version": version, // The version of the subscription type
                    "description": description, // Description from the head table
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
        soup = BeautifulSoup(html, "html.parser")
        nodes = soup.find(class_="main").find_all(class_="text-content")
        for node in nodes:
            if node.find("h1", id="subscription-types") != None:
                table = node.find("h1", id="subscription-types").find_next_sibling("table")
                docs = table.find("tbody").find_all("tr")
                for doc in docs:
                    cells = doc.find_all("td")
                    title = " ".join([str(x) for x in cells[0].stripped_strings])
                    event = str(cells[1].string).strip()
                    version = str(cells[2].string).strip()
                    description = " ".join([str(x) for x in cells[3].stripped_strings])
                    slug = cells[0].find("a").attrs["href"] if cells[0].find("a") else None
                    endpoint = event + " v" + version
                    title = title.removesuffix("NEW").removesuffix("BETA").removesuffix("V" + version).strip()
                    if title not in ret["toc"]:
                        ret["toc"][title] = []
                    ret["toc"][title].append({
                        "endpoint": endpoint
                    })
                    ret["endpoints"][endpoint] = {
                        "title": title,
                        "event": event,
                        "version": version,
                        "description": description,
                        "slug": slug
                    }
        return ret

if __name__ == "__main__":
    parser = TwitchEventSubSubscriptionTypesParser()
    parser.main()