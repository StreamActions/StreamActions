# coding=utf-8
#
# This file is part of StreamActions.
# Copyright � 2019-2025 StreamActions Team (streamactions.github.io)
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

class TwitchReferenceParser(BaseParser):
    """
    Parse a Twitch API Reference page into a format that can be diffed
    """
    def parse(self, html:str) -> dict:
        """
        Parse a Twitch API Reference page from the input HTML and return a dict of parsed data

        The format of the returned dict is:
        {
            "toc": { // Table of Contents
                resource: [ // This is the value of the first column of the TOC
                    {
                        "endpoint": endpointName,
                        "description": tocDescription // BETA and NEW tags are stripped
                    }
                ]
            },
            "endpoints": {
                endpointName: { // This is the text of the h2 tag at the top of the section
                    "description": description, // BETA and NEW tags are stripped
                    "rateLimits": rateLimits,
                    "authorization": authorization,
                    "url": url, // Endpoint URL
                    "slug": slug, // Fragment for doc page
                    "requestQuery": [
                        {
                            "parameter": parameter,
                            "type": jsonDataType,
                            "required": isRequired,
                            "description": description
                        },
                        ...
                    ],
                    "requestBody": [
                        {
                            "field": field,
                            "type": jsonDataType,
                            "required": isRequired,
                            "description": description
                        },
                        ...
                    ],
                    "responseBody": [
                        {
                            "field": field,
                            "type": jsonDataType,
                            "description": description
                        },
                        ...
                    ],
                    "responseCodes": [
                        {
                            "code": httpResponseCode, // Includes the number and name. ex: 200 OK
                            "description": description
                        },
                        ...
                    ],
                    "exampleRequestDescription": exampleRequestDescription,
                    "exampleRequestCurl": exampleRequestCurl,
                    "exampleResponse": exampleResponse
                },
                ...
            }
        }

        Note that in the "endpoints" sub-dict that any value which is not present in the documentation for a particular endpoint will be present, but set to None (or possibly [] for lists).
        All keys are type str. All values which are present are type str

        Note that all values come from the highest level enclosing HTML tag that will support the separation required.
        All child HTML tags are stripped and the resulting strings joined with whitespace

        Args:
            html (str): The HTML from a Twitch API Reference page which will be parsed

        Returns:
            dict: A dict containing the parsed data, as described above
        """
        ret = {
            "toc": {},
            "endpoints": {}
        }
        soup = BeautifulSoup(html, "html.parser")
        nodes = soup.find(class_="main").find_all(class_="doc-content")
        for node in nodes:
            if node.find("h1", id="twitch-api-reference") != None:
                for entry in node.find("tbody").find_all("tr"):
                    cells = entry.find_all("td")
                    resource = str(cells[0].string).strip()
                    endpoint = str(cells[1].string).strip()
                    description = (" ".join([str(x) for x in cells[2].stripped_strings])).removeprefix("BETA ").removeprefix("NEW ").strip()
                    if resource not in ret["toc"]:
                        ret["toc"][resource] = []
                    ret["toc"][resource].append({
                        "endpoint": endpoint,
                        "description": description
                    })
            else:
                docs = node.find(class_="left-docs")
                if docs == None:
                    continue
                # 0 = Start
                # 1 = Description
                # 2 = Per-Endpoint Rate Limits
                # 3 = Authorization
                # 4 = URL
                # 5 = Request Query
                # 6 = Request Body
                # 7 = Response Body
                # 8 = Response Codes
                state = 0
                data = []
                endpoint = None
                description = None
                rateLimits = None
                authorization = None
                url = None
                slug = None
                reqQuery = None
                reqBody = None
                resBody = None
                resCodes = None
                for tag in docs.children:
                    newState = state
                    if tag.name == "h2":
                        endpoint = str(tag.string).strip()
                        slug = "#" + str(tag.attrs["id"]).strip() if "id" in tag.attrs else None
                        newState = 1
                    elif tag.name == "p" or tag.name == "ul" or (tag.name == "table" and state < 5):
                        if state == 1:
                            strong = tag.find("strong")
                            if strong != None and strong.string.startswith("Rate Limit"):
                                description = (" ".join(data)).removeprefix("BETA ").removeprefix("NEW ").strip()
                                state = 2
                                newState = state
                                data = []
                        if state < 5:
                            data.append(" ".join([str(x) for x in tag.stripped_strings]))
                        else:
                            description += " ".join([str(x) for x in tag.stripped_strings])
                    elif tag.name == "table":
                        data = []
                        for entry in tag.find("tbody").find_all("tr"):
                            cells = entry.find_all("td")
                            dataPoint = {}
                            valid = False
                            dataPoint["parameter" if state == 5 else ("code" if state == 8 else "field")] = str(cells[0].string).strip()
                            if dataPoint["parameter" if state == 5 else ("code" if state == 8 else "field")].lower() != "parameter" and dataPoint["parameter" if state == 5 else ("code" if state == 8 else "field")].lower() != "code" and dataPoint["parameter" if state == 5 else ("code" if state == 8 else "field")].lower() != "field":
                                valid = True
                            add = 0
                            if state <= 7 and len(cells) > 2:
                                add += 1
                                dataPoint["type"] = str(cells[1].string).strip()
                                if dataPoint["type"].lower() != "type":
                                    valid = True
                                if state <= 6 and len(cells) > 3:
                                    add += 1
                                    dataPoint["required"] = str(cells[2].string).strip()
                                    if dataPoint["required"].lower() != "required" and dataPoint["required"].lower() != "required?":
                                        valid = True
                            dataPoint["description"] = (" ".join([str(x) for x in cells[1 + add].stripped_strings])).strip()
                            if dataPoint["description"].lower() != "description":
                                valid = True
                            if valid == True:
                                data.append(dataPoint)
                    elif tag.name == "h3":
                        section = tag.string.strip()
                        if section == "Authorization" or section == "Authentication":
                            newState = 3
                        elif section == "URL":
                            newState = 4
                        elif section.startswith("Request Query"):
                            newState = 5
                        elif section.startswith("Request Body"):
                            newState = 6
                        elif section.startswith("Response Body") or section.startswith("Return Value"):
                            newState = 7
                        elif section.startswith("Response Code"):
                            newState = 8
                    if newState != state:
                        if state == 1:
                            description = (" ".join(data)).removeprefix("BETA ").removeprefix("NEW ").strip()
                        elif state == 2:
                            rateLimits = (" ".join(data)).strip()
                        elif state == 3:
                            authorization = (" ".join(data)).strip()
                        elif state == 4:
                            url = (" ".join(data)).strip()
                        elif state == 5:
                            reqQuery = data.copy() if data != None else None
                        elif state == 6:
                            reqBody = data.copy() if data != None else None
                        elif state == 7:
                            resBody = data.copy() if data != None else None
                        elif state == 8:
                            resCodes = data.copy() if data != None else None
                        if newState <= 4:
                            data = []
                        else:
                            data = None
                        state = newState
                if state == 1:
                    description = (" ".join(data)).strip()
                elif state == 2:
                    rateLimits = (" ".join(data)).strip()
                elif state == 3:
                    authorization = (" ".join(data)).strip()
                elif state == 4:
                    url = (" ".join(data)).strip()
                elif state == 5:
                    reqQuery = data.copy()
                elif state == 6:
                    reqBody = data.copy()
                elif state == 7:
                    resBody = data.copy()
                elif state == 8:
                    resCodes = data.copy()
                example = node.find(class_="right-code")
                # 0 = Start
                # 1 = Request Description
                # 2 = Request cURL
                # 3 = Response
                state = 0
                data = None
                exampleRequestDescription = None
                exampleRequestCurl = None
                exampleResponse = None
                if example != None:
                    for tag in example.children:
                        newState = state
                        if state == 1 and tag.name == "div":
                            exampleRequestDescription = (" ".join(data)).strip()
                            state = 2
                            newState = state
                            data = []
                        elif tag.name == "h3":
                            section = tag.string.strip()
                            if section == "Example Request":
                                newState = 1
                            elif section == "Example Response":
                                newState = 3
                        if state > 0 and tag.name != "h3":
                            data.append(" ".join([str(x) for x in tag.stripped_strings]))
                        if newState != state:
                            if state == 1:
                                exampleRequestDescription = (" ".join(data)).strip()
                            elif state == 2:
                                exampleRequestCurl = (" ".join(data)).strip()
                            elif state == 3:
                                exampleResponse = (" ".join(data)).replace("\u00e2\u0080\u008b", "").strip()
                            data = []
                            state = newState
                    if state == 1:
                        exampleRequestDescription = (" ".join(data)).strip()
                    elif state == 2:
                        exampleRequestCurl = (" ".join(data)).strip()
                    elif state == 3:
                        exampleResponse = (" ".join(data)).strip().replace("\u00e2\u0080\u008b", "").strip()
                if endpoint != None:
                    ret["endpoints"][endpoint] = {
                        "description": description,
                        "rateLimits": rateLimits,
                        "authorization": authorization,
                        "url": url,
                        "slug": slug,
                        "requestQuery": reqQuery,
                        "requestBody": reqBody,
                        "responseBody": resBody,
                        "responseCodes": resCodes,
                        "exampleRequestDescription": exampleRequestDescription,
                        "exampleRequestCurl": exampleRequestCurl,
                        "exampleResponse": exampleResponse
                    }
        return ret

if __name__ == "__main__":
    parser = TwitchReferenceParser()
    parser.main()