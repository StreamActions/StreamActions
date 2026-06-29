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

"""
Find DocParser attributes in a folder structure and return their parameters
"""

import argparse
import json
import os
from pathlib import Path
import re

docparserpattern = re.compile(r"(?s)\[DocParser\(\s*\"(?P<friendlyname>[^\"]+)\"\s*,\s*(?P<issue>[0-9]+)\s*,\s*\"(?P<url>[^\"]+)\"\s*,\s*\"(?P<parser>[^\"]+)\"\s*\)\]")

def testfolder(folder: str | Path, glob: str = "*.cs") -> dict:
    """
    Tests if any files in the folder, or sub-folders, matching the glob contain an DocParser attribute and returns the results

    Args:
        folder (str | Path): The folder to check
        glob (str): The glob to use for picking files to check. Default: ".cs"

    Returns:
        dict: A dict of files where at least 1 DocParser attribute was found. The key is the file path relative to DocParserFinder.py, the value is the output of testfile(filePath)
    """
    ret = {}
    execpath = os.path.dirname(os.path.realpath(__file__))
    for file in Path(folder).rglob(glob):
        data = testfile(file)
        if data is not None:
            ret[str(file.absolute().relative_to(execpath, walk_up=True))] = data
    return ret

def testfile(filePath: str | Path) -> list | None:
    """
    Tests if the file contains an DocParser attribute and returns the result

    Args:
        filePath (str | Path): The file to check

    Returns:
        list | None: None if no matches are found; otherwise a list containing dicts as described below

    For a description of the values in the output dicts, see StreamActions.Common.Attributes.DocParserAttribute
    [
        {
            "friendlyname": "friendlyName",
            "issue": issueId,
            "url": "uri",
            "parser": "parser"
        },
        ...
    ]
    """
    with open(filePath, "r", encoding="utf8", errors="ignore") as file:
        lines = file.read()
    m = docparserpattern.search(lines)
    if m is None:
        return None
    matches = []
    while m is not None:
        groups = m.groupdict()
        groups["issue"] = int(groups["issue"])
        matches.append(groups)
        m = docparserpattern.search(lines, m.end())
    return matches

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Find DocParser attributes in a folder structure and return their parameters")
    parser.add_argument("--folder", action="store", help="The folder to search", required=True)
    parser.add_argument("--out", action="store", help="Output to the specified file instead of STDOUT")
    args = parser.parse_args()
    ret = testfolder(args.folder)
    if args.out == None:
        print(json.dumps(ret, indent=4))
    else:
        with open(args.out, "w", encoding="utf8") as out_file:
            json.dump(ret, out_file)