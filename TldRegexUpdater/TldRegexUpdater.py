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

import argparse
import json
import idna
import requests

tlds = {}
itlds = {}
xntlds = {}

eopmark = "_"

escapes = ["(", ")", ".", "\\", "[", "]"]

parser = argparse.ArgumentParser()
parser.add_argument("-v", action="store_true", help="Verbose (Creates syntax tree files and tld text file)")
args = parser.parse_args()

def escape(echr):
    global escapes
    if echr in escapes:
        return "\\" + echr
    return echr

def addTLDRecursive(tld_s, tld):
    global eopmark
    if tld_s[0] in tld:
        ctld = tld[tld_s[0]]
    else:
        ctld = {}
    if len(tld_s) > 1:
        tld[tld_s[0]], isend = addTLDRecursive(tld_s[1:], ctld)
    else:
        ctld[eopmark] = {}
        tld[tld_s[0]] = ctld
    isend = len(tld) == 1 and eopmark in tld
    return tld, isend

resp = requests.get("http://data.iana.org/TLD/tlds-alpha-by-domain.txt", headers = { "User-Agent": "gmt2001.tldregexupdater/2023" })
if resp.status_code != 200:
    exit(1)
tldsresponse = resp.text
lines = tldsresponse.splitlines()
for line in lines:
    line = line.strip()
    if not line.startswith("#"):
        tld_s = line.lower()
        if tld_s.startswith("xn--"):
            xntlds[tld_s] = idna.decode(line)
            if xntlds[tld_s][0] in tlds:
                xntld = tlds[xntlds[tld_s][0]]
            else:
                xntld = {}
            tlds[xntlds[tld_s][0]], isend = addTLDRecursive(xntlds[tld_s][1:], xntld)
        if tld_s[0] in tlds:
            tld = tlds[tld_s[0]]
        else:
            tld = {}
        tlds[tld_s[0]], isend = addTLDRecursive(tld_s[1:], tld)

def combineTldsRecursive(tlds):
    global eopmark
    ntlds = {}
    for tld in tlds:
        if tld == eopmark:
            ntlds[tld] = tlds[tld]
        else:
            ntld = combineTldsRecursive(tlds[tld])
            nkey = list(ntld)[0]
            if len(ntld) == 1:
                if nkey == eopmark:
                    ntlds[tld] = ntld
                else:
                    ntlds[tld + nkey] = ntld[nkey]
            else:
                ntlds[tld] = ntld
    return ntlds

itlds = combineTldsRecursive(tlds)

def combineSingles(singles):
    retval = []
    curval = ""
    for single in singles:
        if len(curval) == 0:
            curval = single
        elif ord(single) != ord(curval[-1]) + 1:
            if len(curval) == 1:
                retval.append(curval)
            elif len(curval) == 2:
                retval.append(curval[0:1])
                retval.append(curval[1:])
            else:
                retval.append(curval[0:1] + "-" + curval[-1])
            curval = single
        else:
            curval = curval + single
    if len(curval) != 0:
        if len(curval) == 1:
            retval.append(curval)
        elif len(curval) == 2:
            retval.append(curval[0:1])
            retval.append(curval[1:])
        else:
            retval.append(curval[0:1] + "-" + curval[-1])
    return retval

def compileRegexRecursive(tlds):
    global eopmark
    singles = {}
    nonsingles = []
    for tld in tlds:
        if tld != eopmark:
            retval = compileRegexRecursive(tlds[tld])
            if len(tld) == 1:
                if not retval in singles:
                    singles[retval] = []
                singles[retval].append(tld)
            else:
                nonsingles.append(tld + retval)

    regex = ""
    if len(singles) + len(nonsingles) > 0:
        regex = regex + "(?:"
        first = True
        if eopmark in tlds:
            regex = regex + "|"
        for single in singles:
            if not first:
                regex = regex + "|"
            first = False
            if len(singles[single]) > 1:
                regex = regex + "["
                singles[single] = combineSingles(singles[single])
                for c in singles[single]:
                    regex = regex + c
                regex = regex + "]"
            else:
                regex = regex + singles[single][0]
            regex = regex + single
        for nonsingle in nonsingles:
            if not first:
                regex = regex + "|"
            first = False
            regex = regex + nonsingle
        regex = regex + ")"
    return regex

regex = "(?<tld>"

regex = regex + compileRegexRecursive(itlds)

regex = regex + ")"

if args.v:
    with open("tld_tree.json", "w", encoding="utf8") as tld_file:
        json.dump(tlds, tld_file)
    with open("tld_itree.json", "w", encoding="utf8") as tld_file:
        json.dump(itlds, tld_file)
    with open("xntlds.json", "w", encoding="utf8") as tld_file:
        json.dump(xntlds, tld_file)
    with open("tlds.txt", "w", encoding="utf8") as tld_file:
        out = tldsresponse
        for xntld in xntlds:
            out = out + '\n' + xntlds[xntld]
        tld_file.write(out)

with open("tldregex.txt", "w", encoding="utf8") as tld_file:
    tld_file.write(regex)

