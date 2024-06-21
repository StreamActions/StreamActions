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

import argparse
import hashlib

def hash(file):
    """Performs a SHA3-256 hash of the input file

    file: The file to hash

    returns: The hash if the file contains content; otherwise `None`
    """
    try:
        with open(file, encoding='utf-8') as f:
            lines = f.readlines()
    except:
        lines = []

    try:
        h = hashlib.sha3_256()
        for line in lines:
            h.update(line.encode(encoding='utf-8'))
        digest = h.hexdigest()
    except:
        digest = None

    if len(lines) == 0 or digest == None or digest == 'a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a':
        return None
    else:
        return digest

def parseargs():
    parser = argparse.ArgumentParser()
    parser.add_argument('--file', help='Input file', required=True)
    return parser.parse_args()

if __name__ == '__main__':
    args = parseargs()
    print(hash(args.file))
