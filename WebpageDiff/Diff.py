#
# This file is part of StreamActions.
# Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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
import difflib
from StripData import stripdata

#python3 Diff.py -context -stripblank -strip -findfirst '<div class=\"main\">' -findlast '<div class=\"subscribe-footer\">' -remre 'cloudcannon[^\"]*' -rem '<a href=\"/docs/product-lifecycle\"><span class=\"pill pill-new\">NEW</span></a> ' '<a href=\"/docs/product-lifecycle\"><span class=\"pill pill-beta\">BETA</span></a> ' -filea helix_2022-05-12.htm -fileb helix_2022-08-28.htm -fileout test.htm

# Handles silent option when calling print
def doprint(args, message, iend='\n'):
    if not args.silent:
        print(message, end=iend)

# Main function
# args: A dict containing the args returned by argparse
#           When calling from another module, args.filea and args.fileb are always required
#           If shouldReturn is False or not specified, fileout is also required
# shouldReturn: If set to True, the output html will be returned as a list of lines, with trailing LF
#           If set to False or not specified, the output html will be written to the file specified in args.fileout
def main(args, shouldReturn=False):
    if not 'fileout' in args:
        args.fileout = 'out.html'

    if not 'main' in args or not args.main:
        args = parseargs(args)

    if args.namea != None:
        namea = args.namea
    else:
        namea = args.filea

    if args.nameb != None:
        nameb = args.nameb
    else:
        nameb = args.fileb

    if args.strip or args.stripblank or args.findfirst != None or args.findlast != None or args.rem != None:
        editfiles = True
    else:
        editfiles = False

    with open(args.filea, encoding='utf-8') as fa:
        with open(args.fileb, encoding='utf-8') as fb:
            doprint(args, 'Preparing files')
            falines = fa.readlines()
            fblines = fb.readlines()

            if editfiles:
                falines = stripdata(args, falines)
                fblines = stripdata(args, fblines)

            doprint(args, 'Generating diff')
            if args.table:
                html = difflib.HtmlDiff().make_table(falines, fblines, namea, nameb, args.context, args.n)
            else:
                html = difflib.HtmlDiff().make_file(falines, fblines, namea, nameb, args.context, args.n)

    doprint(args, 'Splitlines')
    html = html.splitlines(keepends=True)

    if not args.nowrap:
        doprint(args, 'Inserting css and removing nowrap')
        findstr1 = 'td.diff_header {text-align:right}'
        findstr2 = 'nowrap="nowrap"'
        numline = str(len(html))

        for i,line in enumerate(html):
            doprint(args, '\r' + str(i) + '/' + numline, iend='')
            idx = line.find(findstr1)
            changed = False

            if idx != -1:
                html[i] = line[0:(idx + len(findstr1) - 1)] + '; word-break: normal;' + line[idx + len(findstr1) - 1:]
                html.insert(i, '        td {word-break: break-all; overflow-wrap: break-word; vertical-align: top}\n')

            idx = line.find(findstr2)
            while idx != -1:
                line = line[0:idx - 1] + line[idx + len(findstr2):]
                idx = line.find(findstr2)
                changed = True

            if changed:
                html[i] = line

        doprint(args, '\r' + numline + '/' + numline)

    doprint(args, 'Writing output')
    if shouldReturn:
        return html
    else:
        with open(args.fileout, 'w', encoding='utf-8') as fo:
            fo.writelines(html)

    doprint(args, 'Done')

def parseargs(inargs):
    parser = argparse.ArgumentParser(usage='%(prog)s [options] -filea FILEA -fileb FILEB -fileout FILEOUT')
    coregroup = parser.add_argument_group('Core')
    coregroup.add_argument('-filea', help='Left (Original) file', required=True)
    coregroup.add_argument('-fileb', help='Right (Modified) file', required=True)
    coregroup.add_argument('-fileout', help='Output file', required=True)
    outputgroup = parser.add_argument_group('Output')
    outputgroup.add_argument('-context', help='If set, output context diff instead of entire files (Default not set)', action='store_true')
    outputgroup.add_argument('-n', help='Number of context lines for Context mode and jump links (Default 5)', type=int, default=5)
    outputgroup.add_argument('-nowrap', help='If set, output does not use line wrapping (Default not set)', action='store_true')
    outputgroup.add_argument('-table', help='If set, output only contains the table, not the headers, legend, and footers (Default not set)', action='store_true')
    inputgroup = parser.add_argument_group('Input')
    inputgroup.add_argument('-findfirst', help='Find the first occurrence of this string, and drop all text before it on both input files', default=None)
    inputgroup.add_argument('-findlast', help='Find the last occurrence of this string, and drop all text after it on both input files', default=None)
    inputgroup.add_argument('-rem', help='Remove all occurrences of this string in both input files. May be specified multiple times', nargs='*', default=None)
    inputgroup.add_argument('-remre', help='Remove all matches of this regex in both input files. May be specified multiple times', nargs='*', default=None)
    inputgroup.add_argument('-strip', help='If set, all whitespace is striped from the beginning and end of each line in both input files', action='store_true')
    inputgroup.add_argument('-stripblank', help='If set, all blank lines are removed from both input files', action='store_true')
    overridegroup = parser.add_argument_group('Override')
    overridegroup.add_argument('-namea', help='Override left (Original) file name', default=None)
    overridegroup.add_argument('-nameb', help='Override right (Modified) file name', default=None)
    miscgroup = parser.add_argument_group('Misc')
    miscgroup.add_argument('-silent', help='If set, only errors will print (Default not set)', action='store_true')
    return parser.parse_args(args=inargs)

if __name__ == '__main__':
    args = parseargs(None)
    args.main = True
    main(args)
