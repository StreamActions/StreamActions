# coding=utf-8
#
# This file is part of StreamActions.
# Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
import re

# Strips data from the given lines according to the defined arguments
# args: A dict containing the args returned by argparse
# lines: A list of lines
def stripdata(args, lines):
    remre = None
    if args.remre != None:
        remre = []
        for rem in args.remre:
            remre.append(re.compile(rem))

    temp = []
    if args.findfirst != None:
        foundfirst = False
    else:
        foundfirst = True
    foundlast = -1
    for line in lines:
        if not foundfirst and args.findfirst != None:
            idx = line.find(args.findfirst)
            if idx != -1:
                foundfirst = True
        if args.findlast != None:
            idx = line.find(args.findlast)
            if idx != -1:
                foundlast = len(temp) + 1
        if args.strip:
            line = line.strip()
        if args.rem != None:
            for rem in args.rem:
                idx = line.find(rem)
                if idx != -1:
                    if len(rem) == len(line):
                        line = ''
                    else:
                        line = line[0:idx] + line[idx + len(rem):]
        if remre != None:
            for rem in remre:
                matches = rem.finditer(line)
                for match in matches:
                    idx = match.start()
                    if len(match.group()) == len(line):
                        line = ''
                    else:
                        line = line[0:idx] + line[idx + len(match.group()):]
        if len(line) > 0 or not args.stripblank:
            if foundfirst:
                temp.append(line)
    if foundlast > -1:
        temp = temp[0:foundlast]
    return temp

def parseargs(inargs):
    parser = argparse.ArgumentParser()
    parser.add_argument('-findfirst', help='Find the first occurrence of this string, and drop all text before it on both input files', default=None)
    parser.add_argument('-findlast', help='Find the last occurrence of this string, and drop all text after it on both input files', default=None)
    parser.add_argument('-rem', help='Remove all occurrences of this string in both input files. May be specified multiple times', nargs='*', default=None)
    parser.add_argument('-remre', help='Remove all matches of this regex in both input files. May be specified multiple times', nargs='*', default=None)
    parser.add_argument('-strip', help='If set, all whitespace is striped from the beginning and end of each line in both input files', action='store_true')
    parser.add_argument('-stripblank', help='If set, all blank lines are removed from both input files', action='store_true')
    return parser.parse_args(args=inargs)

