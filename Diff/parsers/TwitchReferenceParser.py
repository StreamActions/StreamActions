# coding=utf-8
#
# This file is part of StreamActions.
# Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

"""
Parse a Twitch API Reference page into a format that can be diffed
"""

import argparse
from bs4 import BeautifulSoup
import json
import requests

def parseFromFile(path:str) -> dict:
    """
    Parse a Twitch API Reference page from the specified file and return a dict of parsed data

    The file should be stored in UTF-8 compatible encoding

    Args:
        path (str): The path to an HTML file containing a snapshot of a Twitch API Reference page

    Returns:
        dict: A dict containing the parsed data (see parse(str))
    """
    with open(path, "r", encoding="utf8") as html_file:
        return parse(html_file.read())

def parseFromUrl(url:str) -> dict:
    """
    Parse a Twitch API Reference page from the specified URL and return a dict of parsed data

    The user agent is sent as: streamactions.diff.twitchreferenceparser/2024

    Args:
        url (str): The URL to a Twitch API Reference page

    Returns:
        dict: A dict containing the parsed data (see parse(str))
    """
    resp = requests.get(url, headers = { "User-Agent": "streamactions.diff.twitchreferenceparser/2024" })
    if resp.status_code != 200:
        exit(1)
    return parse(resp.text)

def parse(html:str) -> dict:
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
                "url": url,
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

    Note that in the "endpoints" sub-dict that any value which is not present in the documentation for a particular endpoint will be present, but set to None.
    All keys are type str. All values which are present are type str

    Note that all values come from the highest level enclosing tag that will support the separation required.
    All child tags are stripped and the resulting strings joined with whitespace

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
            reqQuery = None
            reqBody = None
            resBody = None
            resCodes = None
            for tag in docs.children:
                newState = state
                if tag.name == "h2":
                    endpoint = str(tag.string).strip()
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
                        dataPoint["parameter" if state == 5 else ("code" if state == 8 else "field")] = str(cells[0].string).strip()
                        add = 0
                        if state <= 7 and len(cells) > 2:
                            add += 1
                            dataPoint["type"] = str(cells[1].string).strip()
                            if state <= 6 and len(cells) > 3:
                                add += 1
                                dataPoint["required"] = str(cells[2].string).strip()
                        dataPoint["description"] = (" ".join([str(x) for x in cells[1 + add].stripped_strings])).strip()
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
                    elif section.startswith("Response Body") or section.startswith("Return Values"):
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
                            exampleResponse = (" ".join(data)).strip()
                        data = []
                        state = newState
                if state == 1:
                    exampleRequestDescription = (" ".join(data)).strip()
                elif state == 2:
                    exampleRequestCurl = (" ".join(data)).strip()
                elif state == 3:
                    exampleResponse = (" ".join(data)).strip()
            if endpoint != None:
                ret["endpoints"][endpoint] = {
                    "description": description,
                    "rateLimits": rateLimits,
                    "authorization": authorization,
                    "url": url,
                    "requestQuery": reqQuery,
                    "requestBody": reqBody,
                    "responseBody": resBody,
                    "responseCodes": resCodes,
                    "exampleRequestDescription": exampleRequestDescription,
                    "exampleRequestCurl": exampleRequestCurl,
                    "exampleResponse": exampleResponse
                }
    return ret

def diffWithFileL(lhsPath:str, rhs:dict) -> dict:
    """
    Diff two dicts created by parse(str)

    The file should be stored in UTF-8 compatible encoding

    Args:
        lhsPath (str): The path to a JSON file containing the output of a previous call to parse(str). This will be the "original" file in the diff
        rhs (dict): A dict created by a call to parse(str). This will be the "new/modified" file in the diff

    Returns:
        dict: A dict containing the diff data (see diff(dict, dict))
    """
    with open(lhsPath, "r", encoding="utf8") as json_file:
        return diff(json.load(json_file.read()), rhs)

def diffWithFileR(lhs:dict, rhsPath:str) -> dict:
    """
    Diff two dicts created by parse(str)

    The file should be stored in UTF-8 compatible encoding

    Args:
        lhs (dict): A dict created by a call to parse(str). This will be the "original" file in the diff
        rhsPath (str): The path to a JSON file containing the output of a previous call to parse(str). This will be the "new/modified" file in the diff

    Returns:
        dict: A dict containing the diff data (see diff(dict, dict))
    """
    with open(rhsPath, "r", encoding="utf8") as json_file:
        return diff(lhs, json.load(json_file.read()))

def diffWithFiles(lhsPath:str, rhsPath:str) -> dict:
    """
    Diff two dicts created by parse(str)

    The files should be stored in UTF-8 compatible encoding

    Args:
        lhsPath (str): The path to a JSON file containing the output of a previous call to parse(str). This will be the "original" file in the diff
        rhsPath (str): The path to a JSON file containing the output of a previous call to parse(str). This will be the "new/modified" file in the diff

    Returns:
        dict: A dict containing the diff data (see diff(dict, dict))
    """
    with open(lhsPath, "r", encoding="utf8") as json_fileL:
        with open(rhsPath, "r", encoding="utf8") as json_fileR:
            return diff(json.load(json_fileL.read()), json.load(json_fileR.read()))

def diff(lhs:dict, rhs:dict) -> dict:
    """
    Diff two dicts created by parse(str)

    The format of the dict depends on the operation for each entry. If the entire entry is new, the highest level possible will be marked.
    For example:
    {
        "toc":{
            "Ads": {
                "_operation": "add"
            }
        }
    }

    The above dict indicates that the entire contents of the "Ads" resource in the RHS is newly added to the TOC, including the "Ads" key itself

    {
        "toc":{
            "Ads": [
                {
                    "_operation": "add"
                    "endpoint": "Start Commercial"
                }
            ]
        }
    }

    By contrast, this dict indicates that the "Ads" resource was already in the TOC on the LHS, but the entire entry object for "Start Commercial" is new on the RHS

    It is assumed that if a particular sub-object defined in parse(str) is marked as "add" or "remove" as above,
    then the specified operation is applied to the entire sub-object, and the "endpoint", "parameter", "field", or "code" keys are being used as context

    If a specific value has changed, but not the entire object, it will be defined in a sub-object defining the operation
    For example:
    {
        "endpoints": {
            "Start Commercial": {
                "rateLimits": {
                    "_operation": "replace"
                    "lhs": "Only partners<del>/</del>affiliates may run commercials",
                    "rhs": "Only partners<ins> and </ins>affiliates may run commercials",
                    "combined": "Only partners<del>/</del><ins> and </ins>affiliates may run commercials"
                },
                "requestBody": [
                    {
                        "field": "broadcaster_id",
                        "type": {
                            "_operation": "replace"
                            "lhs": "<del>Integer</del>"
                            "rhs": "<ins>String</ins>"
                            "combined": "<del>Integer</del><ins>String</ins>"
                        }
                    }
                ]
            }
        }
    }

    This dict indicates that in "rateLimits", a "/" was replaced with " and ". It also indicates that the "type" for the field "broadcaster_id" in the "requestBody" was
    changed from "Integer" to "String"

    As shown above, "replace" operations will output a string showing just the LHS with <del></del> tags surrounding the removed text, a string showing just the RHS
    with <ins></ins> tags surrounding the added text, and a combined string showing both sets of tags

    Operations:
    - add: Add a new sub-object or string, where one previously did not exist or was set to None. See examples and explanation for how this is represented
    - remove: Remove an existing sub-object or string. A string would be replaced with None, a sub-object would simply be removed from the dict or list. See examples and explanation for "add" for how this is represented
    - insert: Insert the text that is surrounded by the <ins></ins> tags. Contains only sub-key "rhs" from the "replace" example. May contain only enough surrounding text to provide appropriate context
    - delete: Remove the text that is surrounded by the <del></del> tags. Contains only sub-key "lhs" from the "replace" example. May contain only enough surrounding text to provide appropriate context
    - replace: Replace the text that is surrounded by the <del></del> tags with the text that is surrounded by the <ins></ins> tags. See example and explanation for how this is represented

    Args:
        lhs (dict): A dict created by a call to parse(str). This will be the "original" file in the diff
        rhs (dict): A dict created by a call to parse(str). This will be the "new/modified" file in the diff

    Returns:
        dict: A dict containing the diff data, as described above
    """

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Parse a Twitch API Reference page into a format that can be diffed")
    pgroup = parser.add_argument_group("Parse HTML", "Parse the HTML of a Twitch API Reference page and return a dict of parsed data")
    pigroup = pgroup.add_mutually_exclusive_group()
    pigroup.add_argument("--file", action="store", help="Parse the HTML from a file stored in a UTF-8 compatible encoding")
    pigroup.add_argument("--url", action="store", help="Parse the HTML from a URL")
    dgroup = parser.add_argument_group("Diff", "Diff two dicts created by the parser. If only one of --lhs/--rhs is specified, the other is taken from the output of parsing --file/--url")
    dgroup.add_argument("--lhs", action="store", help="Load a JSON file created by parse as the LHS (Original)")
    dgroup.add_argument("--rhs", action="store", help="Load a JSON file created by parse as the RHS (New/Modified)")
    parser.add_argument("--out", action="store", help="Output as JSON to the specified file instead of STDOUT")
    args = parser.parse_args()
    if args.url == None and args.file == None and args.lhs == None and args.rhs == None:
        parser.error("must provide at least 1 argument")
    if args.file != None and args.lhs != None and args.rhs != None:
        parser.error("argument --file: not allowed when using both arguments --lhs and --rhs")
    if args.url != None and args.lhs != None and args.rhs != None:
        parser.error("argument --url: not allowed when using both arguments --lhs and --rhs")
    if args.url == None and args.file == None and (args.lhs == None or args.rhs == None):
        parser.error("can not diff with only 1 input")
    if args.file != None:
        ret = parseFromFile(args.file)
    if args.url != None:
        ret = parseFromUrl(args.url)
    if args.lhs != None and args.rhs != None:
        ret = diffWithFiles(args.lhs, args.rhs)
    elif args.lhs != None:
        ret = diffWithFileL(args.lhs, ret)
    elif args.rhs != None:
        ret = diffWithFileR(ret, args.rhs)
    if args.out == None:
        print(json.dumps(ret, indent=4))
    else:
        with open(args.out, "w", encoding="utf8") as out_file:
            json.dump(ret, out_file)