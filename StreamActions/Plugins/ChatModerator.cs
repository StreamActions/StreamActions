/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using StreamActions.Database;
using StreamActions.Database.Documents.Moderation;
using StreamActions.Database.Documents.Users;
using StreamActions.Enums;
using StreamActions.Plugin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles various moderation actions against chat messages, such as links protection.
    /// </summary>
    [Guid("723C3D97-7FB3-40C9-AFC5-904D38170384")]
    public partial class ChatModerator : IPlugin
    {
        #region Public Constructors

        /// <summary>
        /// Class constructor.
        /// </summary>
        public ChatModerator()
        {
        }

        #endregion Public Constructors

        #region Public Properties

        public bool AlwaysEnabled => true;

        public IReadOnlyCollection<Guid> Dependencies => ImmutableArray<Guid>.Empty;

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Chat Moderation plugin for StreamActions";

        public Guid PluginId => typeof(ChatModerator).GUID;

        public string PluginName => "ChatModerator";

        public Uri PluginUri => new Uri("https://github.com/StreamActions/StreamActions");

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        public void Disabled()
        {
        }

        public void Enabled()
        {
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnBlocklistCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnCapsCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnColouredMessageCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnEmotesCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnFakePurgeCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnLinksCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnLongMessageCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnOneManSpamCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnRepetitionCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnSymbolsCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnZalgoCheck;

            PluginManager.Instance.OnMessagePreModeration += this.ChatModerator_OnMessagePreModeration;
        }

        public string GetCursor() => this.PluginId.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods

        #region Internal Methods

        /// <summary>
        /// Returns the filter settings for a channel.
        /// </summary>
        /// <param name="channelId">Id of the channel.</param>
        /// <returns>The document settings.</returns>
        internal static async Task<ModerationDocument> GetFilterDocumentForChannel(string channelId)
        {
            //TODO: Refactor Mongo
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(m => m.ChannelId, channelId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            return (await cursor.FirstAsync().ConfigureAwait(false));
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Regular expression that is used for getting the number of capital letters in a string.
        /// Example: I AM YELLING!
        /// </summary>
        private readonly Regex _capsRegex = new Regex(@"[A-Z]", RegexOptions.Compiled);

        // Ignore Spelling: cdefgilmnoqrstuwxz, abdefghijmnorstvwyz, acdfghiklmnoruvxyz, ejkmoz, cegrstu, fyi, ijkmor, abdefghilmnpqrstuwy, kmnrtu, delmnoqrst, emop, eghimnrwyz
        // Ignore Spelling: eghimnrwyz, eghimnrwyz, eghimnrwyz, abcikrstuvy, mobi, moe, acdeghklmnopqrstuvwxyz, acefgilopruz, aefghklmnrstwy, qa, eouw, abcdeghijklmnortuvyz
        // Ignore Spelling: cdfghjklmnoprtvwz, agkmsyz, ceginu, xxx, fs, etu, amw
        /// <summary>
        /// Regular expression that is used to getting the number of repeating characters in a string.
        /// Example: aaaaaaaaaaaaaaaaaaaaaaaa
        /// </summary>
        private readonly Regex _characterRepetitionRegex = new Regex(@"(\S)\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the amount of grouped symbols in a string.
        /// Example: #$#$#$#$#$#$#$#$#$#$$#$#$$##### how are you ???????????????
        /// </summary>
        private readonly Regex _groupedSymbolsRegex = new Regex(@"([-!$%#^&*()_+|~=`{}\[\]:'<>?,.\/\\;""])\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is for finding URLs in a string. Lenient version.
        /// Example: google.com
        /// </summary>
        private readonly Regex _linkLenientRegex = new Regex(@"((?:(http(?:|s)):\/\/((?:[a-zA-Z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,64}(?:\:(?:[a-zA-Z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,25})?\@)?)?((?:[a-zA-Z0-9][a-zA-Z0-9\-]{0,64}(?:\s)*(?:\.|\*|\(|\)|\[|\]|d(?:\s)*o(?:\s)*t|(?:\s)+(?:b|c|o|0|1|i|n))+(?:\s)*)+)((?:c\s*(?:(?:o\s*(?:m|))|(?:0\s*(?:m|))))|(?:o\s*(?:r\s*g|m))|(?:0\s*(?:r\s*g|m))|(?:n\s*(?:e\s*t|3\s*t|f\s*[o0]))|(?:i\s*n\s*f\s*[o0])|(?:1\s*(?:n\s*f\s*[o0]|y))|(?:l\s*y)|(?:b[e3]\s*)|(?:r\s*g)|(?:e\s*t)|(?:3\s*t))(?:\:\d{1,5})?)(?:\b|$)|((?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9]))", RegexOptions.IgnoreCase);

        /// <summary>
        /// Regular expression that is for finding URLs in a string. Full version with multi-protocol support.
        /// Example: google.com
        /// </summary>
        private readonly Regex _linkRegex = new Regex(@"((?:((?:|s)ftp(?:|s)|(?:|s)http(?:|s)|rtsp(?:|s)|ws(?:|s)):\/\/((?:[a-zA-Z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,64}(?:\:(?:[a-zA-Z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,25})?\@)?)?((?:[a-zA-Z0-9][a-zA-Z0-9\-]{0,64}(?:\s)*(?:\.|\*|\(|\)|\[|\]|d(?:\s)*o(?:\s)*t|(?:\s)+(?:b|c|o|0|1|i|n))+(?:\s)*)+)((?:a(?:(?:a(?:rp|a))|(?:b(?:arth|(?:b(?:ott|vie|))|le|ogado|udhabi|c))|(?:c(?:ademy|(?:c(?:enture|(?:o(?:u(?:n(?:t(?:a(?:n(?:t(?:s|))))))))))|tor|o|))|(?:d(?:ac|ult|s|))|(?:e(?:ro|tna|g|))|(?:f(?:amilycompany|rica|l|))|(?:g(?:akhan|ency|))|(?:i(?:(?:r(?:bus|force|tel))|g|))|kdn|(?:l(?:faromeo|(?:i(?:baba|pay))|(?:l(?:finanz|state|y))|(?:s(?:ace|tom))|))|(?:m(?:azon|(?:e(?:(?:r(?:i(?:c(?:a(?:n(?:express|family))))))|x))|fam|ica|sterdam|))|(?:n(?:alytics|droid|quan|z))|(?:o(?:l|))|(?:p(?:artments|(?:p(?:le|))))|(?:q(?:uarelle|))|(?:r(?:(?:a(?:mco|b))|chi|my|pa|(?:t(?:e|))|))|(?:s(?:da|ia|sociates|))|(?:t(?:hleta|torney|))|(?:u(?:ction|(?:d(?:i(?:ble|o|)))|spost|(?:t(?:hor|(?:o(?:s|))))|))|vianca|(?:w(?:s|))|(?:x(?:a|))|(?:z(?:ure|))))|(?:b(?:(?:a(?:by|idu|(?:n(?:(?:a(?:mex|narepublic))|[dk]))|(?:r(?:(?:c(?:elona|(?:l(?:a(?:y(?:card|s))))))|efoot|gains|))|(?:s(?:eball|ketball))|uhaus|yern|))|(?:b(?:va|[ct]|))|c[gn]|(?:e(?:(?:a(?:ts|uty))|er|ntley|rlin|(?:s(?:t(?:buy|)))|t|))|(?:h(?:arti|))|(?:i(?:ble|ke|(?:n(?:g(?:o|)))|[doz]|))|(?:l(?:(?:a(?:c(?:k(?:friday|))))|(?:o(?:ckbuster|omberg|g))|ue))|(?:m(?:[sw]|))|(?:n(?:pparibas|))|(?:o(?:ats|ehringer|fa|nd|(?:o(?:(?:k(?:ing|))|))|(?:s(?:ch|(?:t(?:ik|on))))|utique|[mtx]|))|(?:r(?:adesco|idgestone|(?:o(?:adway|ker|ther))|ussels|))|(?:u(?:dapest|gatti|(?:i(?:l(?:d(?:ers|))))|siness|zz|y))|(?:z(?:h|))|[dfgjstvwy]))|(?:c(?:(?:a(?:fe|(?:l(?:vinklein|l|))|(?:m(?:era|p|))|(?:n(?:cerresearch|on))|(?:p(?:etown|(?:i(?:t(?:a(?:l(?:one|)))))))|(?:r(?:avan|ds|(?:e(?:(?:e(?:r(?:s|)))|))|s|))|(?:s(?:(?:e(?:ih|))|ino|[ah]))|(?:t(?:ering|holic|))|b|))|(?:b(?:re|[ans]))|(?:e(?:nter|rn|[bo]))|(?:f(?:[ad]|))|(?:h(?:(?:a(?:(?:n(?:el|nel))|rity|se|t))|eap|intai|(?:r(?:istmas|ome))|urch|))|(?:i(?:priani|rcle|sco|(?:t(?:adel|(?:i(?:c|))|(?:y(?:eats|))))|))|(?:l(?:aims|eaning|(?:i(?:ck|(?:n(?:i(?:que|c)))))|(?:o(?:thing|ud))|(?:u(?:b(?:med|)))|))|(?:o(?:ach|des|ffee|(?:l(?:lege|ogne))|(?:m(?:cast|(?:m(?:bank|unity))|(?:p(?:(?:a(?:ny|re))|uter))|sec|))|(?:n(?:dos|(?:s(?:truction|ulting))|(?:t(?:act|ractors))))|(?:o(?:(?:k(?:i(?:n(?:g(?:channel|)))))|[lp]))|rsica|(?:u(?:ntry|(?:p(?:o(?:n(?:s|))))|rses))|))|pa|(?:r(?:(?:e(?:d(?:i(?:t(?:card|union|)))))|icket|own|(?:u(?:i(?:s(?:e(?:s|)))))|s|))|sc|(?:u(?:isinella|))|(?:y(?:mru|ou|))|[cdgkmnvwxz]))|(?:d(?:(?:a(?:bur|nce|(?:t(?:ing|sun|[ae]))|[dy]))|clk|ds|(?:e(?:(?:a(?:l(?:er|s|)))|gree|(?:l(?:ivery|oitte|ta|l))|mocrat|(?:n(?:t(?:al|ist)))|(?:s(?:i(?:gn|)))|v|))|hl|(?:i(?:amonds|et|gital|(?:r(?:e(?:c(?:t(?:ory|)))))|(?:s(?:(?:c(?:o(?:unt|ver)))|h))|y))|np|(?:o(?:(?:c(?:tor|s))|mains|wnload|[gt]|))|rive|tv|(?:u(?:bai|ck|nlop|pont|rban))|(?:v(?:ag|r))|[jkmz]))|(?:e(?:(?:a(?:rth|t))|(?:c(?:o|))|(?:d(?:eka|(?:u(?:cation|))))|(?:m(?:ail|erck))|(?:n(?:ergy|(?:g(?:i(?:n(?:e(?:e(?:r(?:ing|)))))))|terprises))|pson|quipment|(?:r(?:icsson|ni|))|(?:s(?:tate|q|))|(?:t(?:isalat|))|(?:u(?:rovision|s|))|vents|(?:x(?:change|(?:p(?:ert|osed|ress))|traspace))|[eg]))|(?:f(?:(?:a(?:ge|(?:i(?:rwinds|th|l))|mily|(?:n(?:s|))|(?:r(?:m(?:ers|)))|(?:s(?:hion|t))))|(?:e(?:dex|edback|(?:r(?:r(?:ari|ero)))))|(?:i(?:at|(?:d(?:elity|o))|lm|(?:n(?:a(?:(?:n(?:c(?:ial|e)))|l)))|(?:r(?:(?:e(?:stone|))|mdale))|(?:s(?:h(?:ing|)))|(?:t(?:ness|))|))|(?:l(?:(?:i(?:ckr|ghts|r))|(?:o(?:rist|wers))|y))|(?:o(?:(?:o(?:(?:d(?:network|))|tball|))|(?:r(?:ex|sale|um|d))|undation|x|))|(?:r(?:(?:e(?:senius|e))|(?:o(?:gans|(?:n(?:t(?:door|ier)))))|l|))|tr|(?:u(?:(?:j(?:i(?:tsu|xerox)))|(?:n(?:d|))|rniture|tbol))|yi|[jkm]))|(?:g(?:(?:a(?:(?:l(?:(?:l(?:ery|up|o))|))|(?:m(?:e(?:s|)))|rden|[py]|))|(?:b(?:iz|))|(?:d(?:n|))|(?:e(?:(?:n(?:t(?:ing|)))|orge|a|))|(?:g(?:ee|))|(?:i(?:(?:f(?:t(?:s|)))|(?:v(?:es|ing))|))|(?:l(?:(?:a(?:de|ss))|(?:o(?:b(?:al|o)))|e|))|(?:m(?:ail|bh|[ox]|))|(?:o(?:daddy|(?:l(?:(?:d(?:point|))|f))|(?:o(?:dyear|(?:g(?:le|))|))|[ptv]))|(?:r(?:(?:a(?:inger|phics|tis))|een|ipe|(?:o(?:cery|up))|))|(?:u(?:ardian|cci|ge|(?:i(?:de|tars))|ru|))|[fhnpqstwy]))|(?:h(?:(?:a(?:ir|mburg|ngout|us))|bo|(?:d(?:f(?:c(?:bank|))))|(?:e(?:(?:a(?:l(?:t(?:h(?:care|)))))|(?:l(?:sinki|p))|(?:r(?:mes|e))))|gtv|(?:i(?:phop|samitsu|tachi|v))|(?:k(?:t|))|(?:o(?:ckey|(?:l(?:dings|iday))|(?:m(?:e(?:depot|goods|(?:s(?:ense|)))))|nda|rse|(?:s(?:pital|(?:t(?:ing|))))|(?:t(?:(?:e(?:l(?:es|s)))|mail|))|use|w))|sbc|(?:u(?:ghes|))|(?:y(?:att|undai))|[mnrt]))|(?:i(?:bm|(?:c(?:bc|[eu]))|(?:e(?:ee|))|fm|kano|(?:m(?:amat|db|(?:m(?:o(?:bilien|)))|))|(?:n(?:dustries|(?:f(?:initi|o))|(?:s(?:titute|(?:u(?:r(?:ance|e)))))|(?:t(?:(?:e(?:rnational|l))|uit|))|vestments|[cgk]|))|piranga|(?:r(?:ish|))|(?:s(?:maili|(?:t(?:anbul|))|))|(?:t(?:au|v|))|veco|[dloq]))|(?:j(?:(?:a(?:guar|va))|c[bp]|(?:e(?:ep|tzt|welry|))|io|ll|(?:m(?:p|))|nj|(?:o(?:(?:b(?:urg|s))|[ty]|))|(?:p(?:morgan|rs|))|(?:u(?:egos|niper))))|(?:k(?:aufen|ddi|(?:e(?:(?:r(?:r(?:y(?:hotels|logistics|properties))))|))|fh|(?:i(?:(?:n(?:d(?:er|le)))|tchen|wi|[am]|))|(?:o(?:eln|matsu|sher))|(?:p(?:mg|n|))|(?:r(?:ed|d|))|uokgroup|(?:y(?:oto|))|[ghmnwz]))|(?:l(?:(?:a(?:caixa|(?:m(?:borghini|er))|(?:n(?:(?:c(?:aster|ia))|(?:d(?:rover|))|xess))|salle|(?:t(?:ino|robe|))|(?:w(?:yer|))|))|ds|(?:e(?:ase|clerc|frak|(?:g(?:al|o))|xus))|gbt|(?:i(?:dl|(?:f(?:e(?:insurance|style|)))|ghting|ke|lly|(?:m(?:ited|o))|(?:n(?:coln|de|k))|psy|(?:v(?:ing|e))|xil|))|l[cp]|(?:o(?:(?:a(?:n(?:s|)))|(?:c(?:ker|us))|ft|ndon|tt[eo]|ve|l))|(?:p(?:l(?:financial|)))|(?:t(?:(?:d(?:a|))|))|(?:u(?:ndbeck|pin|(?:x(?:ury|e))|))|[bckrsvy]))|(?:m(?:(?:a(?:cys|drid|(?:i(?:son|f))|keup|(?:n(?:agement|go|))|(?:r(?:(?:k(?:e(?:t(?:ing|s|))))|riott|shalls))|serati|ttel|p|))|ba|(?:c(?:kinsey|))|(?:e(?:(?:d(?:ia|))|et|lbourne|(?:m(?:orial|e))|(?:n(?:u|))|rckmsd|))|(?:i(?:ami|crosoft|n[it]|(?:t(?:subishi|))|l))|(?:l(?:[bs]|))|(?:m(?:a|))|(?:o(?:(?:b(?:i(?:le|)))|da|(?:n(?:ash|ey|ster))|(?:r(?:mon|tgage))|scow|(?:t(?:o(?:rcycles|)))|(?:v(?:ie|))|[eim]|))|(?:s(?:d|))|(?:t(?:[nr]|))|(?:u(?:seum|tual|))|[dghknpqrvwxyz]))|(?:n(?:(?:a(?:goya|me|(?:t(?:ionwide|ura))|vy|b|))|ba|(?:e(?:(?:t(?:bank|flix|work|))|ustar|(?:w(?:holland|s|))|(?:x(?:(?:t(?:direct|))|us))|c|))|(?:f(?:l|))|(?:g(?:o|))|hk|(?:i(?:co|(?:k(?:on|e))|nja|ssa[ny]|))|(?:o(?:kia|(?:r(?:t(?:hwesternmutual|on)))|(?:w(?:ruz|tv|))|))|(?:r(?:[aw]|))|tt|yc|[clpuz]))|(?:o(?:(?:b(?:server|i))|(?:f(?:f(?:ice|)))|kinawa|(?:l(?:(?:a(?:y(?:a(?:n(?:group|)))))|dnavy|lo))|(?:m(?:ega|))|(?:n(?:(?:l(?:ine|))|yourside|[eg]))|oo|pen|(?:r(?:(?:a(?:cle|nge))|(?:g(?:anic|))|igins))|saka|(?:t(?:suka|t))|vh))|(?:p(?:(?:a(?:ge|nasonic|(?:r(?:is|(?:t(?:ners|[sy]))|s))|ssagens|y|))|ccw|(?:e(?:t|))|(?:f(?:izer|))|(?:h(?:armacy|ilips|(?:o(?:ne|(?:t(?:o(?:graphy|s|)))))|ysio|d|))|(?:i(?:(?:c(?:(?:t(?:et|ures))|s))|(?:n(?:[gk]|))|oneer|zza|d))|(?:l(?:(?:a(?:ce|(?:y(?:station|))))|(?:u(?:mbing|s))|))|(?:n(?:c|))|(?:o(?:hl|ker|litie|rn|st))|(?:r(?:(?:a(?:merica|xi))|ess|ime|(?:o(?:(?:d(?:uctions|))|gressive|mo|(?:p(?:e(?:r(?:t(?:ies|y)))))|tection|f|))|(?:u(?:dential|))|))|ub|(?:w(?:c|))|[gkmsty]))|(?:q(?:pon|(?:u(?:e(?:bec|st)))|vc|a))|(?:r(?:(?:a(?:cing|dio|id))|(?:e(?:(?:a(?:(?:l(?:estate|(?:t(?:or|y))))|d))|cipes|(?:d(?:stone|umbrella|))|hab|(?:i(?:(?:s(?:e(?:n|)))|t))|liance|(?:n(?:(?:t(?:als|))|))|(?:p(?:air|ort|ublican))|(?:s(?:t(?:aurant|)))|(?:v(?:i(?:e(?:w(?:s|)))))|xroth|))|(?:i(?:(?:c(?:(?:h(?:ardli|))|oh))|[lop]))|mit|(?:o(?:(?:c(?:her|ks))|deo|gers|om|))|(?:s(?:vp|))|(?:u(?:gby|hr|n|))|(?:w(?:e|))|yukyu))|(?:s(?:(?:a(?:arland|(?:f(?:e(?:ty|)))|kura|(?:l(?:on|e))|(?:m(?:s(?:club|ung)))|(?:n(?:(?:d(?:v(?:i(?:k(?:coromant|)))))|ofi))|rl|ve|xo|[ps]|))|(?:b(?:[is]|))|(?:c(?:(?:h(?:aeffler|midt|(?:o(?:larships|ol))|ule|warz))|ience|johnson|ot|[ab]|))|(?:e(?:(?:a(?:rch|t))|(?:c(?:u(?:r(?:ity|e))))|ek|lect|ner|rvices|ven|(?:x(?:y|))|[sw]|))|fr|(?:h(?:(?:a(?:ngrila|rp|w))|ell|(?:i(?:ksha|a))|(?:o(?:es|(?:p(?:ping|))|uji|(?:w(?:time|))))|riram|))|(?:i(?:lk|(?:n(?:gles|a))|te|))|(?:k(?:(?:i(?:n|))|(?:y(?:pe|))|))|(?:l(?:ing|))|(?:m(?:art|ile|))|(?:n(?:cf|))|(?:o(?:(?:c(?:cer|ial))|(?:f(?:t(?:bank|ware)))|hu|(?:l(?:ar|utions))|n[gy]|y|))|(?:p(?:ace|(?:o(?:rt|t))|readbetting))|(?:r(?:l|))|(?:t(?:(?:a(?:da|ples|(?:t(?:e(?:bank|farm)))|r))|(?:c(?:group|))|(?:o(?:ckholm|(?:r(?:age|e))))|ream|(?:u(?:d(?:io|y)))|yle|))|(?:u(?:cks|(?:p(?:p(?:(?:l(?:ies|y))|ort)))|(?:r(?:gery|f))|zuki|))|(?:w(?:atch|(?:i(?:ftcover|ss))))|(?:y(?:dney|stems|))|[dgjsvxz]))|(?:t(?:(?:a(?:ipei|lk|obao|rget|(?:t(?:(?:a(?:motors|r))|too))|(?:x(?:i|))|b))|(?:c(?:i|))|(?:d(?:k|))|(?:e(?:am|(?:c(?:h(?:nology|)))|masek|nnis|va|l))|(?:h(?:(?:e(?:a(?:t(?:er|re))))|d|))|(?:i(?:aa|ckets|enda|ffany|ps|(?:r(?:es|ol))))|(?:j(?:maxx|x|))|(?:k(?:maxx|))|(?:m(?:all|))|(?:o(?:day|kyo|ols|ray|shiba|tal|urs|wn|(?:y(?:ota|s))|p|))|(?:r(?:(?:a(?:(?:d(?:ing|e))|ining|(?:v(?:e(?:l(?:channel|(?:e(?:r(?:s(?:insurance|))))|))))))|ust|v|))|(?:u(?:be|nes|shu|i))|(?:v(?:s|))|[fglntwz]))|(?:u(?:(?:b(?:ank|s))|(?:n(?:(?:i(?:com|versity))|o))|ol|ps|[agksyz]))|(?:v(?:(?:a(?:cations|(?:n(?:guard|a))|))|(?:e(?:gas|ntures|(?:r(?:isign|sicherung))|t|))|(?:i(?:ajes|deo|king|llas|rgin|(?:s(?:ion|a))|v[ao]|[gnp]|))|laanderen|(?:o(?:dka|(?:l(?:kswagen|vo))|(?:t(?:ing|[eo]))|yage))|(?:u(?:elos|))|[cgn]))|(?:w(?:(?:a(?:(?:l(?:es|mart|ter))|(?:n(?:g(?:gou|)))|(?:t(?:c(?:h(?:es|))))))|(?:e(?:(?:a(?:t(?:h(?:e(?:r(?:channel|))))))|(?:b(?:cam|er|site))|(?:d(?:ding|))|(?:i(?:bo|r))))|hoswho|(?:i(?:en|ki|lliamhill|(?:n(?:dows|ners|e|))))|me|(?:o(?:lterskluwer|odside|(?:r(?:(?:k(?:s|))|ld))|w))|t[cf]|[fs]))|(?:x(?:box|erox|finity|(?:i(?:huan|n))|(?:n(?:-(?:-(?:(?:1(?:1b4c3d|ck2e1b|qqw23a))|2scrj9c|(?:3(?:0rr7y|bst00m|ds443g|e0b707e|hcrj9c|oq18vl8pn36a|pxu8k))|(?:4(?:2c2d9a|(?:5(?:(?:b(?:r(?:5cyl|j9c)))|q11c))|gbrim))|(?:5(?:4b7fta0cc|(?:5(?:q(?:w42g|x5d)))|su34j936bgsg|tzm5g))|(?:6(?:frz82g|qq986b3xl))|(?:8(?:(?:0(?:a(?:dxhks|o21a|qecdr1a|(?:s(?:ehdb|wg)))))|y0a063a))|(?:9(?:(?:0(?:a(?:3ac|is|e)))|dbq2a|et52u|krt00a))|(?:b(?:4w605ferd|ck1b9a5dre4c))|(?:c(?:1avg|2br7g|(?:c(?:k(?:2b3b|wcxetd)))|g4bki|lchc0ea0b2g2a9gcd|(?:z(?:r(?:694b|s0t|u2d)))))|(?:d(?:1(?:a(?:cj3b|lf))))|(?:e(?:1a4c|ckvdtc9d|fvy88h))|(?:f(?:ct429k|hbei|(?:i(?:q(?:228c5hs|64b|s8s|z9s)))|jq720a|lw351e|pcrj9c3d|(?:z(?:c2c9e2c|ys8d69uvgm))))|(?:g(?:2xx48c|ckr3f0f|ecrj9c|k3at1e))|(?:h(?:(?:2(?:b(?:r(?:eg3eve|(?:j(?:9(?:c(?:8c|))))))))|xt814e))|(?:i(?:1b6b1a6a2e|mr513n|o0a7i))|(?:j(?:(?:1(?:a(?:ef|mh)))|6w193g|(?:l(?:q(?:480n2rg|61u9w7b)))|vr189m))|(?:k(?:crx77d1x4a|(?:p(?:(?:r(?:w13d|y57d))|ut3i))))|(?:l(?:1acc|gbbat1ad8j))|(?:m(?:(?:g(?:b(?:9awbf|(?:a(?:(?:3(?:a(?:3ejt|4f16a)))|7c0bbn0a|(?:a(?:kc7dvf|m7a8h))|b2bd|h1a3hjkrd|i9azgqp6j|yh7gpa))|(?:b(?:h(?:1(?:a(?:71e|)))))|(?:c(?:0a9azcg|a7dzdo|pq6gpa1a))|erp4a5d4ar|gu82a|i4ecexp|pl2fh|(?:t(?:3dhd|x2b))|x4cd0ab)))|ix891f|k1bu44c|xtq1m))|(?:n(?:(?:g(?:b(?:c5azd|e9e0a|rx)))|ode|(?:q(?:v(?:7(?:f(?:s00ema|)))))|yqy26a))|(?:o(?:3cw4h|gbpf8fl|tu796d))|(?:p(?:(?:1(?:a(?:cf|i)))|gbs0dh|ssy2u))|(?:q(?:7ce6a|9jyb4c|cka1pmc|(?:x(?:a(?:6a|m)))))|(?:r(?:hqv96g|ovu88b|vc1e0am3e))|(?:s(?:9brj9c|es554g))|(?:t(?:60b56a|ckwe|iq49xqyj))|unup4y|(?:v(?:(?:e(?:r(?:m(?:g(?:e(?:n(?:s(?:b(?:e(?:r(?:a(?:t(?:er-ctb|ung-pwb)))))))))))))|hquv|uq861b))|(?:w(?:(?:4(?:r(?:85el8fhu5dnra|s40l)))|(?:g(?:b(?:h1c|l6a)))))|(?:x(?:hq521b|(?:k(?:c(?:2(?:al3hye2a|dl3a5ee0h))))))|(?:y(?:9a3aq|fro4i67o|gbi2ammx))|zfr164b))))|xx|yz))|(?:y(?:(?:a(?:chts|hoo|maxun|ndex))|(?:o(?:dobashi|ga|kohama|(?:u(?:tube|))))|un|[et]))|(?:z(?:(?:a(?:ppos|ra|))|ero|ip|one|uerich|[mw]))|(?:c\s*(?:(?:o\s*(?:m|))|(?:0\s*(?:m|))))|(?:o\s*(?:r\s*g|m))|(?:0\s*(?:r\s*g|m))|(?:n\s*(?:e\s*t|3\s*t|f\s*[o0]))|(?:i\s*n\s*f\s*[o0])|(?:1\s*(?:n\s*f\s*[o0]|y))|(?:l\s*y)|(?:b[e3]\s*)|(?:r\s*g)|(?:e\s*t)|(?:3\s*t))(?:\:\d{1,5})?)(?:\b|$)|((?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9]))|(bitcoin(?:|cash)|c(?:allto|ontent)|ed2k|f(?:acetime|eed)|git|i(?:ntent|rc(?:|6|s))|jar|m(?:a(?:gnet|ilto|ps|rket)|ms)|payto|s(?:ip(?:|s)|kype|potify|team)|te(?:amspeak|l)|webcal|xmpp):((?:\/\/)?(?:(?:[a-zA-Z0-9\;\?\:\@\&\=\#\~\-\.\+\!\*\\\'\(\)\,\_])|(?:\%[a-fA-F0-9]{2}))+)+(?:\b|$)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Regular expression that is used for getting the number of symbols in a string.
        /// Example: ^^^^#%#%#^#^##*#
        /// </summary>
        private readonly Regex _symbolsRegex = new Regex(@"[-!$%#^&*()_+|~=`{}\[\]:'<>?,.\/\\;""]", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used to getting the number of repeating words in a string.
        /// Example: hi hi hi hi hi hi hi
        /// </summary>
        private readonly Regex _wordRepetitionRegex = new Regex(@"(\S+\s)\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the number of zalgo/boxed/disruptive symbols in a string.
        /// </summary>
        private readonly Regex _zalgoRegex = new Regex(@"[^\uD83C-\uDBFF\uDC00-\uDFFF\u0401\u0451\u0410-\u044f\u0009-\u02b7\u2000-\u20bf\u2122\u0308]", RegexOptions.Compiled);

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Method that will check the message for any blocklisted phrases.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnBlocklistCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            string toMatch;

            foreach (BlocklistDocument blocklist in (await cursor.FirstAsync().ConfigureAwait(false)).Blocklist)
            {
                Regex regex = (blocklist.IsRegex ? blocklist.RegexPhrase : new Regex(Regex.Escape(blocklist.Phrase)));
                toMatch = (blocklist.MatchOn == BlocklistMatchTypes.Message ? e.ChatMessage.Message :
                    (blocklist.MatchOn == BlocklistMatchTypes.Username ? e.ChatMessage.Username : e.ChatMessage.Username + " " + e.ChatMessage.Message));

                if (regex.IsMatch(toMatch))
                {
                    moderationResult.Punishment = blocklist.Punishment;
                    moderationResult.TimeoutSeconds = blocklist.TimeoutTimeSeconds;
                    moderationResult.ModerationReason = blocklist.TimeoutReason;
                    moderationResult.ModerationMessage = blocklist.ChatMessage;
                    break;
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of caps.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnCapsCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            string message = this.RemoveEmotesFromMessage(e.ChatMessage.Message, e.ChatMessage.EmoteSet);

            // Check if the filter is enabled.
            if (document.CapStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.CapExcludedLevels).ConfigureAwait(false))
                {
                    if (this.GetMessageLength(message) >= document.CapMinimumMessageLength)
                    {
                        if (((this.GetNumberOfCaps(message) / this.GetMessageLength(message)) * 100.0) >= document.CapMaximumPercentage)
                        {
                            if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                            {
                                moderationResult.Punishment = document.CapTimeoutPunishment;
                                moderationResult.TimeoutSeconds = document.CapTimeoutTimeSeconds;
                                moderationResult.ModerationReason = document.CapTimeoutReason;
                                moderationResult.ModerationMessage = document.CapTimeoutMessage;
                            }
                            else
                            {
                                moderationResult.Punishment = document.CapWarningPunishment;
                                moderationResult.TimeoutSeconds = document.CapWarningTimeSeconds;
                                moderationResult.ModerationReason = document.CapWarningReason;
                                moderationResult.ModerationMessage = document.CapWarningMessage;
                            }
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for the use of the /me command on Twitch.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnColouredMessageCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.ActionMessageStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.ActionMessageExcludedLevels).ConfigureAwait(false))
                {
                    if (this.HasTwitchAction(e.ChatMessage.Message))
                    {
                        if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                        {
                            moderationResult.Punishment = document.ActionMessageTimeoutPunishment;
                            moderationResult.TimeoutSeconds = document.ActionMessageTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.ActionMessageTimeoutReason;
                            moderationResult.ModerationMessage = document.ActionMessageTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.Punishment = document.ActionMessageWarningPunishment;
                            moderationResult.TimeoutSeconds = document.ActionMessageWarningTimeSeconds;
                            moderationResult.ModerationReason = document.ActionMessageWarningReason;
                            moderationResult.ModerationMessage = document.ActionMessageWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of emotes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnEmotesCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.EmoteStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.EmoteExcludedLevels).ConfigureAwait(false))
                {
                    if ((this.GetNumberOfEmotes(e.ChatMessage.EmoteSet) >= document.EmoteMaximumAllowed) || (document.EmoteRemoveOnlyEmotes && this.RemoveEmotesFromMessage(e.ChatMessage.Message, e.ChatMessage.EmoteSet).Trim().Length == 0))
                    {
                        if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                        {
                            moderationResult.Punishment = document.EmoteTimeoutPunishment;
                            moderationResult.TimeoutSeconds = document.EmoteTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.EmoteTimeoutReason;
                            moderationResult.ModerationMessage = document.EmoteTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.Punishment = document.EmoteWarningPunishment;
                            moderationResult.TimeoutSeconds = document.EmoteWarningTimeSeconds;
                            moderationResult.ModerationReason = document.EmoteWarningReason;
                            moderationResult.ModerationMessage = document.EmoteWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for the use of a fake purge (<message-deleted>) variations.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnFakePurgeCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.FakePurgeStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.FakePurgeExcludedLevels).ConfigureAwait(false))
                {
                    if (this.HasFakePurge(e.ChatMessage.Message))
                    {
                        if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                        {
                            moderationResult.Punishment = document.FakePurgeTimeoutPunishment;
                            moderationResult.TimeoutSeconds = document.FakePurgeTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.FakePurgeTimeoutReason;
                            moderationResult.ModerationMessage = document.FakePurgeTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.Punishment = document.FakePurgeWarningPunishment;
                            moderationResult.TimeoutSeconds = document.FakePurgeWarningTimeSeconds;
                            moderationResult.ModerationReason = document.FakePurgeWarningReason;
                            moderationResult.ModerationMessage = document.FakePurgeWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for use of links.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnLinksCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.LinkStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.LinkExcludedLevels).ConfigureAwait(false))
                {
                    if (this.HasUrl(e.ChatMessage.Message) && !await this.HasAllowlist(e.ChatMessage.Message, e.ChatMessage.RoomId).ConfigureAwait(false))
                    {
                        // TODO: Remove permit from user/check for permit.
                        if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                        {
                            moderationResult.Punishment = document.LinkTimeoutPunishment;
                            moderationResult.TimeoutSeconds = document.LinkTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.LinkTimeoutReason;
                            moderationResult.ModerationMessage = document.LinkTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.Punishment = document.LinkWarningPunishment;
                            moderationResult.TimeoutSeconds = document.LinkWarningTimeSeconds;
                            moderationResult.ModerationReason = document.LinkWarningReason;
                            moderationResult.ModerationMessage = document.LinkWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for lengthy messages.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnLongMessageCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.LengthyMessageStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.LengthyMessageExcludedLevels).ConfigureAwait(false))
                {
                    if (this.GetMessageLength(e.ChatMessage.Message) > document.LengthyMessageMaximumLength)
                    {
                        if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                        {
                            moderationResult.Punishment = document.LengthyMessageTimeoutPunishment;
                            moderationResult.TimeoutSeconds = document.LengthyMessageTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.LengthyMessageTimeoutReason;
                            moderationResult.ModerationMessage = document.LengthyMessageTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.Punishment = document.LengthyMessageWarningPunishment;
                            moderationResult.TimeoutSeconds = document.LengthyMessageWarningTimeSeconds;
                            moderationResult.ModerationReason = document.LengthyMessageWarningReason;
                            moderationResult.ModerationMessage = document.LengthyMessageWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Event method called before all moderation events.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments</param>
        private async Task ChatModerator_OnMessagePreModeration(object sender, OnMessageReceivedArgs e)
        {
            // Add the message to our cache to moderation purposes.
            TwitchMessageCache.Instance.Consume(e.ChatMessage);

            // Set the last time the user sent a message.
            UserDocument document;

            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>(UserDocument.CollectionName);

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Eq(u => u.Id, e.ChatMessage.UserId);

            // Make sure the user exists.
            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                document = new UserDocument
                {
                    DisplayName = e.ChatMessage.DisplayName,
                    Id = e.ChatMessage.UserId,
                    Login = e.ChatMessage.Username
                };

                _ = collection.InsertOneAsync(document).ConfigureAwait(false);
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            document = (await cursor.SingleAsync().ConfigureAwait(false));

            // We only need to get the first badge.
            // Twitch orders badges from higher level to lower.
            // Broadcaster, Twitch Staff, Twitch Admin, (Moderator | VIP) Subscriber, (Prime | Turbo | Others)
            if (e.ChatMessage.Badges.Count > 0)
            {
                switch (e.ChatMessage.Badges[0].Key.ToLowerInvariant())
                {
                    case "staff":
                        document.StaffType = TwitchStaff.Staff;
                        document.UserLevel[e.ChatMessage.RoomId] = UserLevels.TwitchStaff;
                        break;

                    case "admin":
                        document.StaffType = TwitchStaff.Admin;
                        document.UserLevel[e.ChatMessage.RoomId] = UserLevels.TwitchAdmin;
                        break;

                    case "broadcaster":
                        document.UserLevel[e.ChatMessage.RoomId] = UserLevels.Broadcaster;
                        break;

                    case "moderator":
                        document.UserLevel[e.ChatMessage.RoomId] = UserLevels.Moderator;
                        break;

                    case "subscriber":
                        document.UserLevel[e.ChatMessage.RoomId] = UserLevels.Subscriber;
                        break;

                    case "vip":
                        document.UserLevel[e.ChatMessage.RoomId] = UserLevels.VIP;
                        break;
                }
            }
            else
            {
                _ = document.UserLevel.Remove(e.ChatMessage.RoomId);
            }

            if (document.UserModeration.Exists(i => i.ChannelId.Equals(e.ChatMessage.RoomId, StringComparison.Ordinal)))
            {
                document.UserModeration.Find(i => i.ChannelId.Equals(e.ChatMessage.RoomId, StringComparison.Ordinal)).LastMessageSent = DateTime.Now;
            }
            else
            {
                document.UserModeration.Add(new UserModerationDocument
                {
                    LastMessageSent = DateTime.Now
                });
            }

            UpdateDefinition<UserDocument> update = Builders<UserDocument>.Update.Set(i => i, document);

            _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
        }

        /// <summary>
        /// Method that will check the message someone sending too many messages at once or the same message over and over.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnOneManSpamCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.OneManSpamStatus)
            {
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.OneManSpamExcludedLevels).ConfigureAwait(false))
                {
                    if (TwitchMessageCache.Instance.GetNumberOfMessageSentFromUserInPeriod(e.ChatMessage.UserId, e.ChatMessage.RoomId, DateTime.Now.AddSeconds(-document.OneManSpamResetTimeSeconds)) >= document.OneManSpamMaximumMessages)
                    {
                        if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                        {
                            moderationResult.Punishment = document.OneManSpamTimeoutPunishment;
                            moderationResult.TimeoutSeconds = document.OneManSpamTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.OneManSpamTimeoutReason;
                            moderationResult.ModerationMessage = document.OneManSpamTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.Punishment = document.OneManSpamWarningPunishment;
                            moderationResult.TimeoutSeconds = document.OneManSpamWarningTimeSeconds;
                            moderationResult.ModerationReason = document.OneManSpamWarningReason;
                            moderationResult.ModerationMessage = document.OneManSpamWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of repeating characters in a message.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnRepetitionCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.RepetitionStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.RepetitionExcludedLevels).ConfigureAwait(false))
                {
                    if (e.ChatMessage.Message.Length >= document.RepetitionMinimumMessageLength)
                    {
                        if (this.GetLongestSequenceOfRepeatingCharacters(e.ChatMessage.Message) >= document.RepetionMaximumRepeatingCharacters || this.GetLongestSequenceOfRepeatingWords(e.ChatMessage.Message) >= document.RepetionMaximumRepeatingWords)
                        {
                            if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                            {
                                moderationResult.Punishment = document.RepetitionTimeoutPunishment;
                                moderationResult.TimeoutSeconds = document.RepetitionTimeoutTimeSeconds;
                                moderationResult.ModerationReason = document.RepetitionTimeoutReason;
                                moderationResult.ModerationMessage = document.RepetitionTimeoutMessage;
                            }
                            else
                            {
                                moderationResult.Punishment = document.RepetitionWarningPunishment;
                                moderationResult.TimeoutSeconds = document.RepetitionWarningTimeSeconds;
                                moderationResult.ModerationReason = document.RepetitionWarningReason;
                                moderationResult.ModerationMessage = document.RepetitionWarningMessage;
                            }
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of symbols.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnSymbolsCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.SymbolStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.SymbolExcludedLevels).ConfigureAwait(false))
                {
                    if (e.ChatMessage.Message.Length >= document.SymbolMinimumMessageLength)
                    {
                        if (((this.GetNumberOfSymbols(e.ChatMessage.Message) / e.ChatMessage.Message.Length) * 100) >= document.SymbolMaximumPercent || this.GetLongestSequenceOfRepeatingSymbols(e.ChatMessage.Message) >= document.SymbolMaximumGrouped)
                        {
                            if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                            {
                                moderationResult.Punishment = document.SymbolTimeoutPunishment;
                                moderationResult.TimeoutSeconds = document.SymbolTimeoutTimeSeconds;
                                moderationResult.ModerationReason = document.SymbolTimeoutReason;
                                moderationResult.ModerationMessage = document.SymbolTimeoutMessage;
                            }
                            else
                            {
                                moderationResult.Punishment = document.SymbolWarningPunishment;
                                moderationResult.TimeoutSeconds = document.SymbolWarningTimeSeconds;
                                moderationResult.ModerationReason = document.SymbolWarningReason;
                                moderationResult.ModerationMessage = document.SymbolWarningMessage;
                            }
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for disruptive characters.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnZalgoCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // Get moderation document settings.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, e.ChatMessage.RoomId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            ModerationDocument document = (await cursor.FirstAsync().ConfigureAwait(false));

            // Check if the filter is enabled.
            if (document.ZalgoStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage, UserLevels.Broadcaster | UserLevels.Moderator | document.ZalgoExcludedLevels).ConfigureAwait(false))
                {
                    if (this.HasZalgo(e.ChatMessage.Message))
                    {
                        if (await this.UserHasWarning(e.ChatMessage.RoomId, e.ChatMessage.UserId).ConfigureAwait(false))
                        {
                            moderationResult.Punishment = document.ZalgoTimeoutPunishment;
                            moderationResult.TimeoutSeconds = document.ZalgoTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.ZalgoTimeoutReason;
                            moderationResult.ModerationMessage = document.ZalgoTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.Punishment = document.ZalgoWarningPunishment;
                            moderationResult.TimeoutSeconds = document.ZalgoWarningTimeSeconds;
                            moderationResult.ModerationReason = document.ZalgoWarningReason;
                            moderationResult.ModerationMessage = document.ZalgoWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will return the longest sequence amount of repeating characters in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating characters in the message.</returns>
        private int GetLongestSequenceOfRepeatingCharacters(string message) => this._characterRepetitionRegex.Matches(message).DefaultIfEmpty().Max(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that will return the longest sequence amount of repeating symbol in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating symbols in the message.</returns>
        private int GetLongestSequenceOfRepeatingSymbols(string message) => this._groupedSymbolsRegex.Matches(message).DefaultIfEmpty().Max(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that will return the longest sequence amount of repeating words in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating words in the message.</returns>
        private int GetLongestSequenceOfRepeatingWords(string message) => this._wordRepetitionRegex.Matches(message).DefaultIfEmpty().Max(m => (m is null ? 0 : m.Value.Split(' ').Length));

        /// <summary>
        /// Method that gets the length of a message
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>The length of the message.</returns>
        private int GetMessageLength(string message) => message.Length;

        /// <summary>
        /// Method that gets the number of caps in a message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Number of caps in the message.</returns>
        private int GetNumberOfCaps(string message) => this._capsRegex.Matches(message).DefaultIfEmpty().Sum(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that gets the number of emotes in a message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Number of emotes in the message.</returns>
        private int GetNumberOfEmotes(EmoteSet emoteSet) => emoteSet.Emotes.Count;

        /// <summary>
        /// Method that gets the number of symbols in a message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Number of symbols in the message.</returns>
        private int GetNumberOfSymbols(string message) => this._symbolsRegex.Matches(message).DefaultIfEmpty().Sum(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that checks if the message contains a allowlisted link.
        /// </summary>
        /// <param name="message">Message to check for the allowlist.</param>
        /// <param name="channelId">The channel ID to get the allowlist from.</param>
        /// <returns>True if a allowlist is found.</returns>
        private async Task<bool> HasAllowlist(string message, string channelId)
        {
            ModerationDocument document = await GetFilterDocumentForChannel(channelId).ConfigureAwait(false);
            List<Match> matches = this._linkRegex.Matches(message).ToList();

            foreach (Match match in matches)
            {
                string val = (match.Value.IndexOf("?", StringComparison.InvariantCulture) != -1 ?
                    match.Value[match.Value.IndexOf("?", StringComparison.InvariantCulture)..] : match.Value);

                if (document.LinkAllowlist.Exists(w => w.Equals(val, StringComparison.InvariantCultureIgnoreCase)))
                {
                    _ = matches.Remove(match);
                }
            }

            return matches.Any();
        }

        /// <summary>
        /// Method that checks if the message has a fake purge.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>If the message has a fake purge.</returns>
        private bool HasFakePurge(string message) =>
            message.Equals("<message deleted>", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("<deleted message>", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("message deleted by a moderator.", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("message removed by a moderator.", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Method that is used to check if a message is coloured, meaning it starts with the command /me on Twitch.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>True if the message starts with /me.</returns>
        private bool HasTwitchAction(string message) => message.StartsWith("/me", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Method that is used to check if the message contains a URL.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>True if the message has a URL.</returns>
        private bool HasUrl(string message) => this._linkRegex.IsMatch(message);

        /// <summary>
        /// Method that is used to check if a message contains zalgo characters.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>True if the message has zalgo characters.</returns>
        private bool HasZalgo(string message) => this._zalgoRegex.IsMatch(message);

        /// <summary>
        /// Method that removes Twitch emotes from a string.
        /// </summary>
        /// <param name="message">Main message.</param>
        /// <param name="emoteSet">Emotes in the message.</param>
        /// <returns>The message without any emotes.</returns>
        private string RemoveEmotesFromMessage(string message, EmoteSet emoteSet)
        {
            List<Emote> emotes = emoteSet.Emotes;

            for (int i = emotes.Count - 1; i >= 0; i--)
            {
                message = message.Remove(emotes[i].StartIndex, (emotes[i].EndIndex - emotes[i].StartIndex));
            }

            return message;
        }

        /// <summary>
        /// If a user currently has a warning.
        /// </summary>
        /// <param name="channelId">ID of the channel.</param>
        /// <param name="userId">User id to check for warnings</param>
        /// <returns>True if the user has a warning.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1307:Specify StringComparison", Justification = "Channel Id is stored as a string.")]
        private async Task<bool> UserHasWarning(string channelId, string userId)
        {
            // Get moderation warning seconds.
            IMongoCollection<ModerationDocument> modCollection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> modFilter = Builders<ModerationDocument>.Filter.Eq(c => c.ChannelId, channelId);

            using IAsyncCursor<ModerationDocument> modCursor = (await modCollection.FindAsync(modFilter).ConfigureAwait(false));

            uint moderationWarningTimeSeconds = (await modCursor.FirstAsync().ConfigureAwait(false)).ModerationWarningTimeSeconds;

            // Get user document.
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>(UserDocument.CollectionName);

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Eq(u => u.Id, userId);

            using IAsyncCursor<UserDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            UserModerationDocument userModerationDocument = (await cursor.FirstAsync().ConfigureAwait(false)).UserModeration.FirstOrDefault(m => m.ChannelId.Equals(channelId));

            return userModerationDocument.LastModerationWarning.AddSeconds(moderationWarningTimeSeconds) >= DateTime.Now;
        }

        #endregion Private Methods
    }
}
