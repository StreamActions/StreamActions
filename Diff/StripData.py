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

import re

def stripdata(args, lines):
    """Strips data from the given lines according to the defined arguments

    args: A dict containing the args returned by argparse
    lines: A list of lines

    return: A list of lines after the transformations have been performed
    """
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

def addargparse(parsergroup):
    """Adds argparse arguments to the provided parser group for controlling what operations stripdata performs

    parsergroup: A parser group to add the arguments to
    """
    parsergroup.add_argument('-findfirst', help='Find the first occurrence of this string, and drop all text before it on the input file', default=None)
    parsergroup.add_argument('-findlast', help='Find the last occurrence of this string, and drop all text after it on the input file', default=None)
    parsergroup.add_argument('-rem', help='Remove all occurrences of this string in the input file. May be specified multiple times', nargs='*', default=None)
    parsergroup.add_argument('-remre', help='Remove all matches of this regex in the input file. May be specified multiple times', nargs='*', default=None)
    parsergroup.add_argument('-strip', help='If set, all whitespace is striped from the beginning and end of each line in the input file', action='store_true')
    parsergroup.add_argument('-stripblank', help='If set, all blank lines are removed from the input file', action='store_true')

