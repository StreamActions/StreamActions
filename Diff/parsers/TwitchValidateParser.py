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

class TwitchValidateParser(BaseParser):
    """
    Parse the Twitch Validate OAuth page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {}
        }
        soup = BeautifulSoup(html, "html5lib")
        main = soup.find(class_="main") or soup
        
        category = "Validate OAuth"
        ret["toc"][category] = []
        
        for tag in main.find_all("h2"):
            text = str(tag.string).strip() if tag.string else tag.get_text(strip=True)
            if text == "How to validate a token":
                endpoint = "Validate OAuth"
                slug = "#" + str(tag.attrs["id"]).strip() if "id" in tag.attrs else None
                
                ret["toc"][category].append({"endpoint": endpoint})
                
                resBody = []
                table = tag.find_next_sibling("table")
                if table and table.find("tbody"):
                    for entry in table.find("tbody").find_all("tr"):
                        cells = entry.find_all("td")
                        if len(cells) >= 3:
                            resBody.append({
                                "field": str(cells[0].string).strip() if cells[0].string else " ".join(cells[0].stripped_strings),
                                "type": str(cells[1].string).strip() if cells[1].string else " ".join(cells[1].stripped_strings),
                                "description": " ".join(cells[2].stripped_strings).strip()
                            })
                
                ret["endpoints"][endpoint] = {
                    "slug": slug,
                    "responseBody": resBody
                }
        
        return ret

if __name__ == "__main__":
    parser = TwitchValidateParser()
    parser.main()

