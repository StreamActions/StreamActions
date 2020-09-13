import argparse
import json
import requests
from os import path
from itertools import product

parser = argparse.ArgumentParser()
parser.add_argument("-s", action="store_true", help="Short Regex (Only use the included text files)")
parser.add_argument("-v", action="store_true", help="Verbose (Creates syntax tree files)")
args = parser.parse_args()

tlds = {}
ctlds = {}
replacements = {}
replacements_marks = {}

marks = ["!"]
escapes = ["(", ")", ".", "\\", "[", "]"]

def escape(chr):
    global escapes
    if chr in escapes:
        return "\\" + chr
    return chr

def getMark(chr):
    global replacements_marks
    retval = ""
    if chr in replacements_marks:
        for mark in replacements_marks[chr]:
            if mark == "!":
                retval = retval + "\\s*"
    return retval

def addTLDRecursive(tld_s, tld, usemarks):
    global replacements_marks
    if tld_s[0] in tld:
        ctld = tld[tld_s[0]]
    else:
        ctld = {}
    if len(tld_s) > 1:
        tld[tld_s[0]], isend = addTLDRecursive(tld_s[1:], ctld, usemarks)
        if usemarks and tld_s[1:2] in replacements_marks:
            if not isend:
                tld["_mark"] = getMark(tld_s[1:2])
    else:
        ctld["_"] = {}
        tld[tld_s[0]] = ctld
    isend = len(tld) == 1 and "_" in tld
    return tld, isend

if not args.s:
    resp = requests.get("http://data.iana.org/TLD/tlds-alpha-by-domain.txt", headers = { "User-Agent": "gmt2001.tldregexupdater/2020" })
    if resp.status_code != 200:
        exit(1)
    response = resp.text.splitlines()
    for line in response:
        line = line.strip()
        if not line.startswith("#"):
            tld_s = line.lower()
            if tld_s[0] in tlds:
                tld = tlds[tld_s[0]]
            else:
                tld = {}
            tlds[tld_s[0]], isend = addTLDRecursive(tld_s[1:], tld, False)

def filler(word):
    global replacements
    combos = [(c,) if c not in replacements else replacements[c] for c in word]
    return ("".join(o) for o in product(*combos))

if path.exists("tlds-anti-workaround-tlds.txt") and path.exists("tlds-anti-workaround-replacements.txt"):
    with open("tlds-anti-workaround-replacements.txt") as repl_file:
        lines = repl_file.read().splitlines()
        for line in lines:
            line = line.strip()
            if not line.startswith("#"):
                replacements_marks[line[0].lower()] = []
                while len(line) > 1 and line[1] in marks:
                    replacements_marks[line[0].lower()].append(line[1].lower())
                    line_s = line[0]
                    line_e = ""
                    if len(line) > 2:
                        line_e = line[2:]
                    line = line_s + line_e
                replacements[line[0]] = line.split()
    with open("tlds-anti-workaround-tlds.txt") as tld_file:
        lines = tld_file.read().splitlines()
        for line in lines:
            line = line.strip()
            if not line.startswith("#"):
                permutations = filler(line)
                orig_start = line[0].lower()
                for aline in permutations:
                    tld_s = aline.lower()
                    if tld_s[0] in ctlds:
                        tld = ctlds[tld_s[0]]
                    else:
                        tld = {}
                    if orig_start in replacements_marks:
                        tld["_selfmark"] = getMark(orig_start)
                    if tld_s[1:2] in replacements_marks:
                        tld["_mark"] = getMark(tld_s[1:2])
                    ctlds[tld_s[0]], isend = addTLDRecursive(tld_s[1:], tld, True)

def lookAheadIsSame(tlds, ttld):
    same = True
    nahead = ""
    cahead = ""
    for tld in tlds:
        if tld != "_mark" and tld != "_selfmark":
            last = ""
            for subtld in tlds[tld]:
                if subtld != "_mark" and subtld != "_selfmark":
                    if last != "" and subtld != last:
                        same = False
                    last = subtld
            if same:
                same, ahead = lookAheadIsSame(tlds[tld], tld)
                if nahead != "" and ahead[1:] != nahead:
                    same = False
                nahead = ahead[1:]
                cahead = ahead
    return same, ttld + cahead

def compileRegexRecursive(tlds, issame, root):
    regex = ""
    singles = []
    postsingle = ""
    num_patterns = 0
    max_patterns = 0
    mark = ""
    markcount = 0
    if "_mark" in tlds:
        mark = tlds["_mark"]
        markcount = markcount + 1
    if "_selfmark" in tlds:
        markcount = markcount + 1
    for tld in tlds:
        submarkcount = 0
        selfmark = ""
        if "_mark" in tlds[tld]:
            submarkcount = submarkcount + 1
        if "_selfmark" in tlds[tld]:
            submarkcount = submarkcount + 1
            selfmark = tlds[tld]["_selfmark"]
        if tld != "_" and tld != "_mark" and tld != "_selfmark":
            if len(tlds[tld]) - submarkcount == 1 and "_" in tlds[tld]:
                singles.append(tld)
            else:
                tlen = len(tlds[tld]) - submarkcount
                if len(regex) > 0:
                    regex = regex + "|"
                newissame, ahead = lookAheadIsSame(tlds[tld], tld)
                retval, patterns, tot_patterns, onlysingles = compileRegexRecursive(tlds[tld], newissame, False)
                max_patterns = max(max_patterns, patterns)
                if patterns > 1 or root:
                    regex = regex + "(?:"
                if not issame:
                    regex = regex + escape(tld) + selfmark + mark
                if tlen > 1 and not onlysingles:
                    regex = regex + "(?:"
                if issame:
                    singles.append(escape(tld))
                    if postsingle == "":
                        postsingle = retval
                else:
                    regex = regex + retval
                    num_patterns = num_patterns + 1
                if tlen > 1 and not onlysingles:
                    regex = regex + ")"
                if patterns > 1 or root:
                    regex = regex + ")"
                if regex == "(?:)":
                    regex = ""

    if len(singles) > 1:
        if len(regex) > 0:
            regex = regex + "|"
        regex = regex + "["
        for single in singles:
            regex = regex + single
        regex = regex + "]"
        num_patterns = num_patterns + 1
    elif len(singles) == 1:
        if len(regex) > 0:
            regex = regex + "|"
        regex = regex + singles[0]
        num_patterns = num_patterns + 1
    if issame:
        regex = regex + mark
    regex = regex + postsingle
    if "_" in tlds and len(regex) > 0:
        regex = regex + "|"
        num_patterns = num_patterns + 1
    return regex, max(max_patterns, num_patterns), num_patterns, len(singles) == len(tlds) - markcount

regex = "("

if not args.s:
    regex = regex + compileRegexRecursive(tlds, False, True)[0]

if path.exists("tlds-anti-workaround-tlds.txt") and path.exists("tlds-anti-workaround-replacements.txt"):
    if not args.s and len(regex) > 1:
        regex = regex + "|"
    regex = regex + compileRegexRecursive(ctlds, False, True)[0]

regex = regex + ")"

if args.v:
    with open("tld_tree.json", "w", encoding="utf8") as tld_file:
        json.dump(tlds, tld_file)
    with open("tld_ctree.json", "w", encoding="utf8") as tld_file:
        json.dump(ctlds, tld_file)

with open("tldregex.txt", "w", encoding="utf8") as tld_file:
    tld_file.write(regex)

