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
import hashlib
from StripData import stripdata

#python3 Hash.py -stripblank -strip -findfirst '<div class="main">' -findlast '<div class="subscribe-footer">' -remre 'cloudcannon[^"]*' -rem '<a href="/docs/product-lifecycle"><span class="pill pill-new">NEW</span></a>' '<a href="/docs/product-lifecycle"><span class="pill pill-beta">BETA</span></a>' -file helix_2022-05-12.htm

# Main function
# args: A dict containing the args returned by argparse
#           When calling from another module, args.file is always required
def main(args):
    if args.strip or args.stripblank or args.findfirst != None or args.findlast != None or args.rem != None:
        editfiles = True
    else:
        editfiles = False

    with open(args.file, encoding='utf-8') as f:
        lines = f.readlines()
        if editfiles:
            lines = stripdata(args, lines)

    h = hashlib.sha3_256()
    for line in lines:
        h.update(line.encode(encoding='utf-8'))

    digest = h.hexdigest()
    if len(lines) == 0 or digest == 'a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a':
        print('nocontent')
    else:
        print(digest)

def parseargs(inargs):
    parser = argparse.ArgumentParser(usage='%(prog)s [options] -file FILE')
    coregroup = parser.add_argument_group('Core')
    coregroup.add_argument('-file', help='Input file', required=True)
    inputgroup = parser.add_argument_group('Input')
    inputgroup.add_argument('-findfirst', help='Find the first occurrence of this string, and drop all text before it on both input files', default=None)
    inputgroup.add_argument('-findlast', help='Find the last occurrence of this string, and drop all text after it on both input files', default=None)
    inputgroup.add_argument('-rem', help='Remove all occurrences of this string in both input files. May be specified multiple times', nargs='*', default=None)
    inputgroup.add_argument('-remre', help='Remove all matches of this regex in both input files. May be specified multiple times', nargs='*', default=None)
    inputgroup.add_argument('-strip', help='If set, all whitespace is striped from the beginning and end of each line in both input files', action='store_true')
    inputgroup.add_argument('-stripblank', help='If set, all blank lines are removed from both input files', action='store_true')
    return parser.parse_args(args=inargs)

if __name__ == '__main__':
    args = parseargs(None)
    main(args)

