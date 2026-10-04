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

class TwitchOidcParser(BaseParser):
    """
    Parse the Twitch OIDC UserInfo page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        ret = {
            "toc": {},
            "endpoints": {}
        }
        soup = BeautifulSoup(html, "html5lib")
        main = soup.find(class_="main") or soup
        
        category = "OIDC UserInfo"
        ret["toc"][category] = []
        
        targets_tables = [
            "Get the user to authorize your app",
            "Use the authorization code to get a token"
        ]
        
        # Keep track if we are under "OIDC authorization code grant flow" to distinguish from implicit flow if needed
        # although the table parsing will just pick up the tables right under these H3s.
        for tag in main.find_all(['h2', 'h3']):
            text = str(tag.string).strip() if tag.string else tag.get_text(strip=True)
            slug = "#" + str(tag.attrs.get("id", "")).strip() if tag.has_attr("id") else None
            
            if text in targets_tables:
                endpoint = f"OIDC {text}"
                ret["toc"][category].append({"endpoint": endpoint})
                
                reqData = []
                table = tag.find_next_sibling("table")
                if table and table.find("tbody"):
                    for entry in table.find("tbody").find_all("tr"):
                        cells = entry.find_all("td")
                        if len(cells) >= 4:
                            reqData.append({
                                "parameter": str(cells[0].string).strip() if cells[0].string else " ".join(cells[0].stripped_strings),
                                "required": str(cells[1].string).strip() if cells[1].string else " ".join(cells[1].stripped_strings),
                                "type": str(cells[2].string).strip() if cells[2].string else " ".join(cells[2].stripped_strings),
                                "description": " ".join(cells[3].stripped_strings).strip()
                            })
                
                if text == "Get the user to authorize your app":
                    ret["endpoints"][endpoint] = {
                        "slug": slug,
                        "requestQuery": reqData
                    }
                else:
                    ret["endpoints"][endpoint] = {
                        "slug": slug,
                        "requestBody": reqData
                    }
                    
            elif text == "Getting claims information from an access token":
                endpoint = "UserInfo"
                ret["toc"][category].append({"endpoint": endpoint})
                
                exampleRequestCurl = None
                exampleResponse = None
                
                # Look for curl and json response
                for sibling in tag.find_next_siblings():
                    if sibling.name in ['h2', 'h3']:
                        break
                    
                    if sibling.name == 'pre':
                        curl_text = sibling.get_text(strip=True)
                        if curl_text.startswith("curl"):
                            exampleRequestCurl = curl_text
                    
                    if sibling.name == 'div' and sibling.get('class') and 'highlighter-rouge' in sibling.get('class'):
                        json_text = sibling.get_text(strip=True)
                        if json_text.startswith("{") and not exampleResponse:
                            exampleResponse = json_text
                
                ret["endpoints"][endpoint] = {
                    "slug": slug,
                    "exampleRequestCurl": exampleRequestCurl,
                    "exampleResponse": exampleResponse
                }
        
        return ret

if __name__ == "__main__":
    parser = TwitchOidcParser()
    parser.main()

