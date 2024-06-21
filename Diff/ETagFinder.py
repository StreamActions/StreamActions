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
Find ETag attributes in a fodler structure and return their parameters
"""

import argparse
import json
import os
from pathlib import Path
import re

etagpattern = re.compile(r"(?s)\[ETag\(\s*\"(?P<friendlyname>[^\"]+)\"\s*,\s*(?P<issue>[0-9]+)\s*,\s*\"(?P<url>[^\"]+)\"\s*,\s*\"(?P<hash>[^\"]+)\"\s*,\s*\"(?P<date>[^\"]+)\"\s*,\s*\"(?P<parser>[^\"]+)\"\s*,\s*[{\[](?P<parameters>.*?)[}\]]\s*\)\]")
parampattern = re.compile(r"(?s)(\"(?P<param>([^\"]|\\\")+)\"(,|$))")

def testfolder(folder: str | Path):
    """Tests if any files in the folder contain an ETag attribute and returns the results

    folder: The folder to check

    returns: A dict of files where at least 1 ETag attribute was found. The key is the filename relative to ETagFinder.py, the value is the output of testfile(file)
    """
    ret = {}
    execpath = os.path.dirname(os.path.realpath(__file__))
    for file in Path(folder).rglob("*.cs"):
        data = testfile(file)
        if data is not None:
            ret[str(file.absolute().relative_to(execpath, walk_up=True))] = data
    return ret

def testfile(file: str | Path):
    """Tests if the file contains an ETag attribute and returns the result

    file: The file to check

    returns: `None` if no matches are found; otherwise a list containing dicts as described below

    For a description of the values in the output dicts, see StreamActions.Common.Attributes.ETagAttribute
    [
        {
            "friendlyname": "friendlyName",
            "issue": issueId,
            "url": "uri",
            "hash": "eTag",
            "date": "timestamp",
            "parser": "parser",
            "parameters": [
                "parameter",
                ...
            ]
        },
        ...
    ]
    """
    with open(file, "r", encoding="utf8") as csfile:
        lines = csfile.read()
    m = etagpattern.search(lines)
    if m is None:
        return None
    matches = []
    while m is not None:
        groups = m.groupdict()
        m2 = parampattern.search(groups["parameters"])
        parameters = []
        while m2 is not None:
            parameters.append(m2["param"])
            m2 = parampattern.search(groups["parameters"], m2.end())
        groups["parameters"] = parameters
        groups["issue"] = int(groups["issue"])
        matches.append(groups)
        m = etagpattern.search(lines, m.end())
    return matches

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Find ETag attributes in a fodler structure and return their parameters")
    parser.add_argument("--folder", action="store", help="The folder to search")
    parser.add_argument("--out", action="store", help="Output as JSON to the specified file instead of STDOUT")
    args = parser.parse_args()
    ret = testfolder(args.folder)
    if args.out == None:
        print(json.dumps(ret, indent=4))
    else:
        with open(args.out, "w", encoding="utf8") as out_file:
            json.dump(ret, out_file)