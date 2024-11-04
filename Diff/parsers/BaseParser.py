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
Base class for a parser which converts a page into a JSON format that can be diffed
"""

import argparse
from difflib import SequenceMatcher
import json
import requests

class BaseParser:
    def parseFromFile(self, path:str) -> dict:
        """
        Parse a page from the specified file and return a dict of parsed data

        The file should be stored in UTF-8 compatible encoding

        Args:
            path (str): The path to an HTML file containing a snapshot of a page

        Returns:
            dict: A dict containing the parsed data (see parse(str))
        """
        with open(path, "r", encoding="utf8") as html_file:
            return self.parse(html_file.read())

    def parseFromUrl(self, url:str) -> dict:
        """
        Parse a page from the specified URL and return a dict of parsed data

        The user agent is sent as: streamactions.diff.parser/2024

        Args:
            url (str): The URL to a page

        Returns:
            dict: A dict containing the parsed data (see parse(str))
        """
        resp = requests.get(url, headers = { "User-Agent": "streamactions.diff.parser/2024" })
        if resp.status_code != 200:
            exit(1)
        return self.parse(resp.text)

    def parse(self, html:str) -> dict:
        """
        Parse a from the input HTML and return a dict of parsed data

        For compatibility with the GitHub Action, the output of this function should be either a dict of endpoints,
        or a dict containing a `toc` key and an `endpoints` key, where `endpoints` is a dict of endpoints

        For `endpoints`, the key should be the Friendly Name and the value should be a dict. In the dict, there should be a key `slug`
        with a value of the URI fragment or relative path from the base API doc URL
        {
            "Start Commercial": {
                "slug": "#start-commercial", // Fragment for doc page
            },
        }

        Args:
            html (str): The HTML from a page which will be parsed

        Returns:
            dict: A dict containing the parsed data
        """
        raise NotImplementedError()

    def diffWithFileL(self, lhsPath:str, rhs:dict) -> dict:
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
            return self.diff(json.load(json_file), rhs)

    def diffWithFileR(self, lhs:dict, rhsPath:str) -> dict:
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
            return self.diff(lhs, json.load(json_file))

    def diffWithFiles(self, lhsPath:str, rhsPath:str) -> dict:
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
                return self.diff(json.load(json_fileL), json.load(json_fileR))

    def diffobj(self, lhs:any, rhs:any) -> dict:
        """
        Diff two objects

        Args:
            lhs (any): The "original" object in the diff
            rhs (any): The "new/modified" object in the diff

        Returns:
            dict: A dict containing the diff data, as described in diff(dict, dict)
        """
        if isinstance(lhs, str) and rhs == None:
                return {"_operation": "remove", "lhs": lhs}
        elif isinstance(rhs, str) and lhs == None:
                return {"_operation": "add", "rhs": rhs}
        elif isinstance(lhs, dict) and rhs == None:
                lhs["_operation"] = "remove"
                return lhs
        elif isinstance(rhs, dict) and lhs == None:
                rhs["_operation"] = "add"
                return rhs
        elif rhs == lhs:
            return {"_operation": "none"}
        elif isinstance(lhs, dict) and isinstance(rhs, dict):
            ret = {}
            foundk = []
            hasOp = False
            for lk,lv in lhs.items():
                if lk in rhs:
                    foundk.append(lk)
                    res = self.diffobj(lv, rhs[lk])
                    hasOp = True
                else:
                    res = {"_operation": "remove"}
                    hasOp = True
                if res["_operation"] != "none":
                    ret[lk] = res
                    hasOp = True
            for rk in rhs:
                if rk not in foundk:
                    ret[rk] = {"_operation": "add"}
                    hasOp = True
            if hasOp == False:
                ret["_operation"] = "none"
            return ret
        elif isinstance(lhs, str) and isinstance(rhs, str):
            seqm = SequenceMatcher(None, lhs, rhs)
            lhs_str = []
            rhs_str = []
            combined_str = []
            hasIns = False
            hasDel = False
            for opcode, a0, a1, b0, b1 in seqm.get_opcodes():
                if opcode == "equal":
                    lhs_str.append(seqm.a[a0:a1])
                    rhs_str.append(seqm.a[a0:a1])
                    combined_str.append(seqm.a[a0:a1])
                elif opcode == "insert":
                    rhs_str.append("<ins>" + seqm.b[b0:b1] + "</ins>")
                    combined_str.append("<ins>" + seqm.b[b0:b1] + "</ins>")
                    hasIns = True
                elif opcode == "delete":
                    lhs_str.append("<del>" + seqm.a[a0:a1] + "</del>")
                    combined_str.append("<del>" + seqm.a[a0:a1] + "</del>")
                    hasDel = True
                elif opcode == "replace":
                    lhs_str.append("<del>" + seqm.a[a0:a1] + "</del>")
                    rhs_str.append("<ins>" + seqm.b[b0:b1] + "</ins>")
                    combined_str.append("<del>" + seqm.a[a0:a1] + "</del>")
                    combined_str.append("<ins>" + seqm.b[b0:b1] + "</ins>")
                    hasIns = True
                    hasDel = True
            if hasIns:
                if hasDel:
                    return {"_operation": "replace", "lhs": "".join(lhs_str), "rhs": "".join(rhs_str), "combined": "".join(combined_str)}
                else:
                    return {"_operation": "insert", "rhs": "".join(rhs_str)}
            elif hasDel:
                return {"_operation": "delete", "lhs": "".join(lhs_str)}
            else:
                return {"_operation": "none"}
        return {"_operation": "unknown", "lhs": lhs, "rhs": rhs}

    def diff(self, lhs:dict, rhs:dict) -> dict:
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
        - none: No operation. Should not normally occur
        - unknown: Unable to determine operation. Should not normally occur

        Args:
            lhs (dict): A dict created by a call to parse(str). This will be the "original" file in the diff
            rhs (dict): A dict created by a call to parse(str). This will be the "new/modified" file in the diff

        Returns:
            dict: A dict containing the diff data, as described above
        """
        return self.diffobj(lhs, rhs)

    def main(self):
        parser = argparse.ArgumentParser(description="Parse a page into a JSON format that can be diffed")
        pgroup = parser.add_argument_group("Parse HTML", "Parse the HTML of a page and return a dict of parsed data")
        pigroup = pgroup.add_mutually_exclusive_group()
        pigroup.add_argument("--file", action="store", help="Parse the HTML from a file stored in a UTF-8 compatible encoding")
        pigroup.add_argument("--url", action="store", help="Parse the HTML from a URL")
        pgroup.add_argument("--out", action="store", help="Output JSON object from HTML to the specified file instead of STDOUT")
        dgroup = parser.add_argument_group("Diff", "Diff two dicts created by the parser. If only one of --lhs/--rhs is specified, the other is taken from the output of parsing --file/--url")
        dgroup.add_argument("--lhs", action="store", help="Load a JSON file created by parse as the LHS (Original)")
        dgroup.add_argument("--rhs", action="store", help="Load a JSON file created by parse as the RHS (New/Modified)")
        dgroup.add_argument("--diffout", action="store", help="Output diff as JSON to the specified file instead of STDOUT")
        args = parser.parse_args()
        if args.url == None and args.file == None and args.lhs == None and args.rhs == None:
            parser.error("must provide at least 1 argument")
        if args.file != None and args.lhs != None and args.rhs != None:
            parser.error("argument --file: not allowed when using both arguments --lhs and --rhs")
        if args.url != None and args.lhs != None and args.rhs != None:
            parser.error("argument --url: not allowed when using both arguments --lhs and --rhs")
        if args.url == None and args.file == None and (args.lhs == None or args.rhs == None):
            parser.error("can not diff with only 1 input")
        retp = None
        retd = None
        if args.file != None:
            retp = self.parseFromFile(args.file)
        elif args.url != None:
            retp = self.parseFromUrl(args.url)
        if args.lhs != None and args.rhs != None:
            retd = self.diffWithFiles(args.lhs, args.rhs)
        elif args.lhs != None and retp != None:
            retd = self.diffWithFileL(args.lhs, retp)
        elif args.rhs != None and retp != None:
            retd = self.diffWithFileR(retp, args.rhs)
        if retp != None:
            if args.out == None:
                print(json.dumps(retp, indent=4))
            else:
                with open(args.out, "w", encoding="utf8") as pout_file:
                    json.dump(retp, pout_file)
        if retd != None:
            if retp != None and args.out == None:
                print("")
            if args.diffout == None:
                print(json.dumps(retd, indent=4))
            else:
                with open(args.diffout, "w", encoding="utf8") as dout_file:
                    json.dump(retd, dout_file)