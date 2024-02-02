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
import difflib
import StripData

#python3 Diff.py -context -stripblank -strip -findfirst '<div class="main">' -findlast '<div class="subscribe-footer">' -remre 'cloudcannon[^"]*' -rem '<a href="/docs/product-lifecycle"><span class="pill pill-new">NEW</span></a>' '<a href="/docs/product-lifecycle"><span class="pill pill-beta">BETA</span></a>' -filea helix_2022-05-12.htm -fileb helix_2022-08-28.htm -fileout test.htm

def __doprint(args, message, iend='\n', verbose=True):
    """Handles printing console output depending on the value of args.silent, args.verbose, and verbose

    args: A dict containing the args returned by argparse
    message: The message to potentially print
    iend: The `end` parameter to print()
    verbose: If `True` (default), the message is only printed if args.verbose is `True`; if `False`, the value of args.verbose has no effect
    """
    if not args.silent:
        if args.verbose or not verbose:
            print(message, end=iend)

def diff(args, shouldReturn=False):
    """Performs a diff of the specified files, then outputs the resulting diff info

    args: A dict containing the args returned by argparse
           args.filea and args.fileb are always required
           If shouldReturn is `False` or not specified, args.fileout is also required
    shouldReturn: If set to `True`, the output html will be returned as a list of lines, with trailing LF
           If set to `False` or not specified, the output html will be written to the file specified in args.fileout

    returns: The output HTML as a list of lines, if shouldReturn was `True`
    """
    if not 'fileout' in args:
        args.fileout = 'out.html'

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
            __doprint(args, 'Preparing files', verbose=False)
            falines = fa.readlines()
            fblines = fb.readlines()

            if editfiles:
                falines = StripData.stripdata(args, falines)
                fblines = StripData.stripdata(args, fblines)

            __doprint(args, 'Generating diff', verbose=False)
            if args.table:
                html = difflib.HtmlDiff().make_table(falines, fblines, namea, nameb, args.context, args.n)
            else:
                html = difflib.HtmlDiff().make_file(falines, fblines, namea, nameb, args.context, args.n)

    __doprint(args, 'Splitlines')
    html = html.splitlines(keepends=True)

    if not args.nowrap:
        __doprint(args, 'Inserting css and removing nowrap')
        findstr1 = 'td.diff_header {text-align:right}'
        findstr2 = 'nowrap="nowrap"'
        numline = str(len(html))

        for i,line in enumerate(html):
            __doprint(args, '\r' + str(i) + '/' + numline, iend='')
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

        __doprint(args, '\r' + numline + '/' + numline)

    __doprint(args, 'Writing output')
    if shouldReturn:
        return html
    else:
        with open(args.fileout, 'w', encoding='utf-8') as fo:
            fo.writelines(html)

    __doprint(args, 'Done', verbose=False)

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
    StripData.addargparse(inputgroup)
    overridegroup = parser.add_argument_group('Override')
    overridegroup.add_argument('-namea', help='Override left (Original) file name', default=None)
    overridegroup.add_argument('-nameb', help='Override right (Modified) file name', default=None)
    miscgroup = parser.add_argument_group('Misc')
    miscgroup.add_argument('-silent', help='If set, only errors will print (Default not set)', action='store_true')
    miscgroup.add_argument('-verbose', help='If set, verbose status information will print (Default not set)', action='store_true')
    args, _ = parser.parse_known_args(args=inargs)
    return args

if __name__ == '__main__':
    args = parseargs(None)
    diff(args)
