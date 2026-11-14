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
import itertools
import json
from pathlib import Path
import idna
import requests

tlds = {}
itlds = {}
xntlds = {}

eopmark = "_"

escapes = ["(", ")", ".", "\\", "[", "]"]

script_dir = Path(__file__).resolve().parent

parser = argparse.ArgumentParser()
parser.add_argument("-v", action="store_true", help="Verbose (Creates syntax tree files and tld text file)")
parser.add_argument("--out", default="tldregex.txt", help="Output file path for the generated regex (default: tldregex.txt)")
parser.add_argument("--replacements", default=str(script_dir / "tlds-anti-workaround-replacements.txt"), help="Path to anti-workaround replacements file")
parser.add_argument("--anti-workaround-tlds", default=str(script_dir / "tlds-anti-workaround-tlds.txt"), help="Path to anti-workaround TLDs file")
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

def add_tld(tld_map, tld_s):
    if not tld_s:
        return
    if tld_s[0] in tld_map:
        tld = tld_map[tld_s[0]]
    else:
        tld = {}
    if len(tld_s) > 1:
        tld_map[tld_s[0]], _ = addTLDRecursive(tld_s[1:], tld)
    else:
        tld[eopmark] = {}
        tld_map[tld_s[0]] = tld

def load_replacements(path):
    replacements = {}
    with open(path, encoding="utf8") as f:
        for raw_line in f:
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split()
            key_spec = parts[0]
            omittable = key_spec.endswith("!")
            ch = (key_spec[:-1] if omittable else key_spec).lower()
            options = [ch]
            if omittable:
                options.append("")
            for rep in parts[1:]:
                rep_lower = rep.lower()
                if rep_lower not in options:
                    options.append(rep_lower)
            replacements[ch] = options
    return replacements

def generate_anti_workaround_tlds(tlds_path, replacements):
    variants = []
    seen = set()
    with open(tlds_path, encoding="utf8") as f:
        for raw_line in f:
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            tld = line.lower()
            char_options = [replacements.get(ch, [ch]) for ch in tld]
            for combo in itertools.product(*char_options):
                variant = "".join(combo)
                if variant and variant not in seen:
                    seen.add(variant)
                    variants.append(variant)
    return variants

resp = requests.get("https://data.iana.org/TLD/tlds-alpha-by-domain.txt", headers = { "User-Agent": "gmt2001.tldregexupdater/2023" }, timeout=30)
if resp.status_code != 200:
    exit(1)
tldsresponse = resp.text
lines = tldsresponse.splitlines()
for line in lines:
    line = line.strip()
    if line and not line.startswith("#"):
        tld_s = line.lower()
        if tld_s.startswith("xn--"):
            xntlds[tld_s] = idna.decode(line)
            add_tld(tlds, xntlds[tld_s])
        add_tld(tlds, tld_s)

replacements = load_replacements(args.replacements)
for variant in generate_anti_workaround_tlds(args.anti_workaround_tlds, replacements):
    add_tld(tlds, variant)

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

with open(args.out, "w", encoding="utf8") as tld_file:
    tld_file.write(regex)

