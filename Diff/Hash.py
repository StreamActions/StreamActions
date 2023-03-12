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
import StripData

#python3 Hash.py -stripblank -strip -findfirst '<div class="main">' -findlast '<div class="subscribe-footer">' -remre 'cloudcannon[^"]*' -rem '<a href="/docs/product-lifecycle"><span class="pill pill-new">NEW</span></a>' '<a href="/docs/product-lifecycle"><span class="pill pill-beta">BETA</span></a>' -file helix_2022-05-12.htm

def hash(args, shouldReturn=False):
    """Performs a SHA3-256 hash of the input file

    args: A dict containing the args returned by argparse; args.file is always required
    shouldReturn:
        If `False` (default), the hash is printed if the file contains content after any stripdata transformation;
          otherwise, the string `"nocontent"` is printed if the file was empty or didn't exist

        If `True`, the hash is returned if the file contains content after any stripdata transformation;
          otherwise, `None` if the file was empty or didn't exist

    returns: The hash if the file contains content after any stripdata transformations, or `None`, if shouldReturn is `True`
    """
    try:
        with open(args.file, encoding='utf-8') as f:
            lines = f.readlines()
    except:
        lines = []

    lines = StripData.stripdata(args, lines)

    try:
        h = hashlib.sha3_256()
        for line in lines:
            h.update(line.encode(encoding='utf-8'))
        digest = h.hexdigest()
    except:
        digest = None

    if len(lines) == 0 or digest == None or digest == 'a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a':
        if shouldReturn:
            return None
        else:
            print('nocontent')
    else:
        if shouldReturn:
            return digest
        else:
            print(digest)

def parseargs(inargs):
    parser = argparse.ArgumentParser(usage='%(prog)s [options] -file FILE')
    coregroup = parser.add_argument_group('Core')
    coregroup.add_argument('-file', help='Input file', required=True)
    inputgroup = parser.add_argument_group('Input')
    StripData.addargparse(inputgroup)
    args, _ = parser.parse_known_args(args=inargs)
    return args

if __name__ == '__main__':
    args = parseargs(None)
    hash(args)

