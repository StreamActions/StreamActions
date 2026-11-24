/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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

using FluentAssertions;
using StreamActions.Core.Migration;
using StreamActions.Core.Models;
using StreamActions.Core.Permissions;
using StreamActions.Core.Templates;
using Xunit;

namespace StreamActions.Core.Tests.Migration;

/// <summary>
/// Unit tests for <see cref="LegacyTagMigrationConverter"/> covering all 4 pipeline stages,
/// all 7 legacy bot syntaxes, security exclusions, Action Block upgrades, and AST validation.
/// </summary>
public sealed class LegacyTagMigrationConverterTests
{
    #region Convert

    [Fact]
    [Trait("Member", "Convert")]
    public void ConvertWhenRequestIsNullThrowsArgumentNullException()
    {
        LegacyTagMigrationConverter converter = new();
        Action act = () => converter.Convert(null!);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    [Trait("Member", "Convert")]
    public void ConvertWhenTemplateIsEmptyReturnsValidEmptyResult()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest("empty", string.Empty));

        _ = result.ConvertedTemplate.Should().BeEmpty();
        _ = result.ParseResult.IsValid.Should().BeTrue();
        _ = result.LockModeratorEdits.Should().BeFalse();
        _ = result.AllowedUsers.Should().BeEmpty();
        _ = result.StreamStatusRequirement.Should().Be(StreamStatusRequirement.Both);
        _ = result.AllowedGames.Should().BeEmpty();
        _ = result.MinRequiredArgs.Should().BeNull();
        _ = result.DiscoveredSecrets.Should().BeEmpty();
        _ = result.ReferencedVirtualFiles.Should().BeEmpty();
        _ = result.RequiresActionBlockUpgrade.Should().BeFalse();
        _ = result.UpgradedActions.Should().BeEmpty();
        _ = result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "Convert")]
    public void ConvertWhenDocumentationSection4ExamplesProvidedProducesExpectedConvertedTemplates()
    {
        LegacyTagMigrationConverter converter = new();

        LegacyCommandMigrationResult ex1 = converter.Convert(new LegacyCommandMigrationRequest(
            "dodgeball",
            "(sender) throws a dodgeball at (1=random)!",
            LegacyBotSource.PhantomBot));
        _ = ex1.ConvertedTemplate.Should().Be("{sender} throws a dodgeball at {or {1} {random_chatter}}!");
        _ = ex1.ParseResult.IsValid.Should().BeTrue();

        LegacyCommandMigrationResult ex2 = converter.Convert(new LegacyCommandMigrationRequest(
            "fact",
            "(unescape (readfilerand facts.txt))",
            LegacyBotSource.PhantomBot));
        _ = ex2.ConvertedTemplate.Should().Be("{kv_rand \"files\" \"facts.txt\"}");
        _ = ex2.ReferencedVirtualFiles.Should().Equal("facts.txt");
        _ = ex2.Diagnostics.Should().Contain(d => d.Code == MigrationDiagnosticCode.StrippedUnescapeTag && d.Severity == MigrationDiagnosticSeverity.Warning);
        _ = ex2.Diagnostics.Should().Contain(d => d.Code == MigrationDiagnosticCode.ReplacedFileTag);
        _ = ex2.ParseResult.IsValid.Should().BeTrue();

        LegacyCommandMigrationResult ex3 = converter.Convert(new LegacyCommandMigrationRequest(
            "!cmdname",
            "(customapi https://api.example.com/lookup?key=(token)&user=(encodeurlparam (1)))",
            LegacyBotSource.PhantomBot));
        _ = ex3.ConvertedTemplate.Should().Be("{api \"https://api.example.com/lookup?key=$secret.CMDNAME_TOKEN&user={url_encode {1}}\"}");
        _ = ex3.DiscoveredSecrets.Should().ContainSingle();
        _ = ex3.DiscoveredSecrets[0].SecretName.Should().Be("CMDNAME_TOKEN");
        _ = ex3.DiscoveredSecrets[0].AllowedHostPatterns.Should().Equal("api.example.com");
        _ = ex3.ParseResult.IsValid.Should().BeTrue();

        LegacyCommandMigrationResult ex4 = converter.Convert(new LegacyCommandMigrationRequest(
            "weather",
            "$(user), weather for $(touser): $(urlfetch https://wttr.in/$(querystring)?format=3)",
            LegacyBotSource.Nightbot));
        _ = ex4.ConvertedTemplate.Should().Be("{sender}, weather for {target}: {api \"https://wttr.in/{url_encode {args}}?format=3\"}");
        _ = ex4.ParseResult.IsValid.Should().BeTrue();

        LegacyCommandMigrationResult ex5 = converter.Convert(new LegacyCommandMigrationRequest(
            "hug",
            "${user} has hugged ${touser} ${count} times!",
            LegacyBotSource.StreamElements));
        _ = ex5.ConvertedTemplate.Should().Be("{sender} has hugged {target} {count} times!");
        _ = ex5.ParseResult.IsValid.Should().BeTrue();

        LegacyCommandMigrationResult ex6Firebot = converter.Convert(new LegacyCommandMigrationRequest(
            "deaths",
            "$user has seen $streamer die $counter[deaths] times in $game!",
            LegacyBotSource.Firebot));
        _ = ex6Firebot.ConvertedTemplate.Should().Be("{sender} has seen {channel} die {counter_get \"deaths\"} times in {game}!");
        _ = ex6Firebot.ParseResult.IsValid.Should().BeTrue();

        LegacyCommandMigrationResult ex6StreamerBot = converter.Convert(new LegacyCommandMigrationRequest(
            "so",
            "%user% shouted out %targetUserName% playing %targetUserGame%!",
            LegacyBotSource.StreamerBot));
        _ = ex6StreamerBot.ConvertedTemplate.Should().Be("{sender} shouted out {target} playing {game {target}}!");
        _ = ex6StreamerBot.ParseResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region Stage1MetaGuards

    [Fact]
    [Trait("Member", "Stage1MetaGuards")]
    public void ConvertWhenMetaGuardTagsPresentExtractsAndStripsAllGuardsWithDiagnostics()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationRequest request = new(
            "guarded",
            "(adminonlyedit) (useronly @Alice, Bob) (onlineonly) (gameonly Minecraft, Elden Ring) (requireargs 2) Hi (sender)!");

        LegacyCommandMigrationResult result = converter.Convert(request);

        _ = result.ConvertedTemplate.Should().Be("Hi {sender}!");
        _ = result.LockModeratorEdits.Should().BeTrue();
        _ = result.AllowedUsers.Should().Equal("Alice", "Bob");
        _ = result.StreamStatusRequirement.Should().Be(StreamStatusRequirement.OnlineOnly);
        _ = result.AllowedGames.Should().Equal("Minecraft", "Elden Ring");
        _ = result.MinRequiredArgs.Should().Be(2);
        _ = result.Diagnostics.Should().HaveCount(5);
        _ = result.Diagnostics.Should().OnlyContain(d =>
            d.Severity == MigrationDiagnosticSeverity.Info
            && d.Code == MigrationDiagnosticCode.ExtractedMetaGuard);
    }

    [Fact]
    [Trait("Member", "Stage1MetaGuards")]
    public void ConvertWhenOfflineOnlyMetaGuardPresentSetsOfflineOnlyRequirement()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "offline",
            "(offlineonly) Stream is currently offline."));

        _ = result.ConvertedTemplate.Should().Be("Stream is currently offline.");
        _ = result.StreamStatusRequirement.Should().Be(StreamStatusRequirement.OfflineOnly);
        _ = result.Diagnostics.Should().ContainSingle(d => d.Code == MigrationDiagnosticCode.ExtractedMetaGuard);
    }

    #endregion

    #region Stage2SecurityExclusions

    [Theory]
    [Trait("Member", "Stage2SecurityExclusions")]
    [InlineData("(unescape (readfile secret.txt))", "{kv_get \"files\" \"default\" \"secret.txt\"}", "secret.txt")]
    [InlineData("(unescape (readfileall notes.txt))", "{kv_get \"files\" \"default\" \"notes.txt\"}", "notes.txt")]
    [InlineData("(unescape (readfilerand quotes.txt))", "{kv_rand \"files\" \"quotes.txt\"}", "quotes.txt")]
    public void ConvertWhenUnescapeWrapsFileTagStripsUnescapeWithSecurityWarning(
        string rawTemplate,
        string expectedConverted,
        string expectedFile)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest("file", rawTemplate));

        _ = result.ConvertedTemplate.Should().Be(expectedConverted);
        _ = result.ReferencedVirtualFiles.Should().Equal(expectedFile);
        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.StrippedUnescapeTag
            && d.Severity == MigrationDiagnosticSeverity.Warning);
        _ = result.Diagnostics.Should().Contain(d => d.Code == MigrationDiagnosticCode.ReplacedFileTag);
    }

    [Theory]
    [Trait("Member", "Stage2SecurityExclusions")]
    [InlineData("(unescape (customapi https://example.com/data))", "{api https://example.com/data}")]
    [InlineData("(unescape (api GET https://example.com/data))", "{api GET https://example.com/data}")]
    public void ConvertWhenUnescapeWrapsExternalApiTagStripsUnescapeWithSecurityWarning(
        string rawTemplate,
        string expectedConverted)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest("api", rawTemplate));

        _ = result.ConvertedTemplate.Should().Be(expectedConverted);
        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.StrippedUnescapeTag
            && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    [Trait("Member", "Stage2SecurityExclusions")]
    public void ConvertWhenUnescapeWrapsNestedExpressionUnwrapsAndTranspilesToNativeAst()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "hug",
            "(unescape (1=sender))"));

        _ = result.ConvertedTemplate.Should().Be("{or {1} {sender}}");
        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.StrippedUnescapeTag
            && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Theory]
    [Trait("Member", "Stage2SecurityExclusions")]
    [InlineData("(escape (1))", "{1}")]
    [InlineData("(1!)", "{1}")]
    [InlineData("(9!)", "{9}")]
    [InlineData("(echo!)", "{args}")]
    [InlineData("(cpinput!)", "{cp_input}")]
    [InlineData("(cpinputraw)", "{cp_input}")]
    [InlineData("(cpinputraw!)", "{cp_input}")]
    public void ConvertWhenEscapeOrStripEscapeModifiersPresentStripsModifiers(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest("esc", rawTemplate));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.Diagnostics.Should().Contain(d => d.Code == MigrationDiagnosticCode.StrippedEscapeModifier);
    }

    [Theory]
    [Trait("Member", "Stage2SecurityExclusions")]
    [InlineData("(writefile log.txt, true, (sender) joined)", "{kv_append \"files\" \"default\" \"log.txt\" \"{sender} joined\"}", "log.txt")]
    [InlineData("(writefile status.txt, false, online)", "{kv_set \"files\" \"default\" \"status.txt\" online}", "status.txt")]
    public void ConvertWhenPhantomBotWriteFilePresentConvertsToScopedKvTag(
        string rawTemplate,
        string expected,
        string expectedFile)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest("wf", rawTemplate));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.ReferencedVirtualFiles.Should().Equal(expectedFile);
        _ = result.Diagnostics.Should().Contain(d => d.Code == MigrationDiagnosticCode.ReplacedFileTag);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Stage2SecurityExclusions")]
    public void ConvertWhenTokenIsStandaloneOutsideHttpTagStripsWithWarningAndRegistersNoSecret()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "leak",
            "My secret is (token)!"));

        _ = result.ConvertedTemplate.Should().Be("My secret is !");
        _ = result.DiscoveredSecrets.Should().BeEmpty();
        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.StrippedStandaloneSecretTag
            && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Theory]
    [Trait("Member", "Stage2SecurityExclusions")]
    [InlineData("$(eval ['heads','tails'][Math.floor(Math.random()*2)])", "{random_choice \"heads\" \"tails\"}")]
    [InlineData("$(eval var a = ['yes', 'no', 'maybe']; a[Math.floor(Math.random() * a.length)])", "{random_choice \"yes\" \"no\" \"maybe\"}")]
    [InlineData("$(eval $(1) + 1)", "{math add {1} 1}")]
    [InlineData("$(eval Math.floor(10 / 3))", "{math floor {math div 10 3}}")]
    [InlineData("$(eval '$(1)'.replace('foo','bar'))", "{replace {1} \"foo\" \"bar\"}")]
    [InlineData("$(eval '$(1)' || 'default')", "{or {1} \"default\"}")]
    public void ConvertWhenNightbotEvalIsSafeIdiomTranspilesToTier1Ast(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "evalsafe",
            rawTemplate,
            LegacyBotSource.Nightbot));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.RequiresActionBlockUpgrade.Should().BeFalse();
        _ = result.UpgradedActions.Should().BeEmpty();
        _ = result.Diagnostics.Should().Contain(d => d.Code == MigrationDiagnosticCode.TranspiledEvalExpression);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Stage2SecurityExclusions")]
    public void ConvertWhenNightbotEvalIsComplexScriptPromotesToClearScriptActionBlock()
    {
        LegacyTagMigrationConverter converter = new();
        const string script = "const r = []; for (let i = 0; i < 5; i++) { r.push(i * 2); } r.join(', ');";
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "evalcomplex",
            $"Result: $(eval {script})",
            LegacyBotSource.Nightbot));

        _ = result.ConvertedTemplate.Should().Be("Result:");
        _ = result.RequiresActionBlockUpgrade.Should().BeTrue();
        _ = result.UpgradedActions.Should().ContainSingle();
        MigratedActionBlockDescriptor block = result.UpgradedActions[0];
        _ = block.ActionType.Should().Be("script.clearscript");
        _ = block.RequiresBroadcasterReview.Should().BeTrue();
        _ = block.RequiredPermissionKey.Should().Be(BuiltInPermissionKeys.UiCommandsScriptEdit);
        _ = block.Parameters["script"].Should().Be(script);
        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.PromotedEvalToClearScript
            && d.Severity == MigrationDiagnosticSeverity.Warning);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region Stage3PhantomBotModules

    [Theory]
    [Trait("Member", "Stage3PhantomBotModules")]
    [InlineData("(1) (9) (1>) (9>) (echo)", "{1} {9} {args 1} {args 9} {args}")]
    [InlineData("(1|guest) (2>=sender)", "{or {1} \"guest\"} {or {args 2} {sender}}")]
    [InlineData("(help Usage: !cmd <user>)", "{if {1} \"\" \"Usage: !cmd <user>\"}")]
    [InlineData("(cancel)", "{cancel}")]
    [InlineData("(encodeurlparam (1)) (encodeurl (echo))", "{url_encode {1}} {url_encode {args}}")]
    [InlineData("(nl2br (1)) (nl2x <p> (2)) (url0a2nl (3))", "{replace {1} \"\\n\" \"<br>\"} {replace {2} \"\\n\" <p>} {replace {3} \"%0A\" \"\\n\"}")]
    [InlineData("(code 8) (repeat 3, hi) (nl)", "{random_code 8} {repeat 3 hi} {nl}")]
    [InlineData("(sender) (@sender) (baresender) (touser) (sanitizeuser (1)) (pointtouser)", "{sender} {@sender} {bare_sender} {target} {clean_user {1}} {point_to_user}")]
    [InlineData("(senderrank) (senderrankonly) (age) (age (1)) (random) (randomrank)", "{sender_rank} {rank} {account_age} {account_age {1}} {random_chatter} {random_chatter}")]
    [InlineData("(channelname) (game) (status) (uptime) (titleinfo) (downtime) (playtime) (gameinfo) (gamesplayed)", "{channel} {game} {title} {uptime} {uptime} {downtime} {playtime} {playtime} {games_played}")]
    [InlineData("(viewers) (follows) (subscribers) (followage (1)) (followdate (1)) (hours) (hoursround (1)) (lasttip)", "{viewers} {followers} {subscribers} {followage {1}} {followdate {1}} {hours} {hours {1}} {last_tip}")]
    [InlineData("(team_name myteam) (team_url myteam) (team_members myteam) (team_random_member myteam)", "{team_name myteam} {team_url myteam} {team_members myteam} {team_random_member myteam}")]
    [InlineData("(team_member_game myteam, bob) (team_member_followers myteam, bob) (team_member_url myteam, bob)", "{team_member_game myteam bob} {team_member_followers myteam bob} {team_member_url myteam bob}")]
    [InlineData("(points) (points (1)) (pointname 5) (price !bet) (pay !win)", "{points} {points {1}} {pointname 5} {price !bet} {pay !win}")]
    [InlineData("(addpoints 10, (sender)) (takepoints 5, (1)) (takepointsorcancel 20, (sender)) (transferpoints 15, (1), (sender))", "{points_add 10 {sender}} {points_take 5 {1}} {points_take_or_cancel 20 {sender}} {points_transfer 15 {1} {sender}}")]
    [InlineData("(count) (count +1 deaths) (count reset deaths) (keywordcount gg) (commandslist !)", "{count} {count +1 deaths} {count_reset deaths} {count gg} {commands_list !}")]
    [InlineData("(cptitle) (cpprompt) (cpcost) (cpinput) (cpdisplayname) (cpusername) (cpuserid)", "{cp_title} {cp_prompt} {cp_cost} {cp_input} {cp_user} {cp_username} {cp_user_id}")]
    [InlineData("(cpredemptionid) (cpredeemableid) (cpfulfillstatus) (cpfulfill) (cpcancel)", "{cp_redemption_id} {cp_reward_id} {cp_status} {cp_fulfill} {cp_cancel}")]
    [InlineData("(#) (# 5, 25)", "{random_int 1 100} {random_int 5 25}")]
    [InlineData("(add 1, 2) (subtract 5, 3) (multiply 2, 4) (divide 8, 2) (remainder 7, 3) (exponentiation 2, 5)", "{math add 1 2} {math sub 5 3} {math mul 2 4} {math div 8 2} {math mod 7 3} {math pow 2 5}")]
    [InlineData("(abs -4) (ceil 1.2) (floor 1.8) (round 1.56, 1) (min 3, 1, 2) (max 3, 1, 2)", "{math abs -4} {math ceil 1.2} {math floor 1.8} {math round 1.56 1} {math min 3 1 2} {math max 3 1 2}")]
    [InlineData("(currenttime America/New_York, HH:mm) (countdown 2030-01-01) (countup 2020-01-01) (gettimevar start)", "{time America/New_York HH:mm} {countdown 2030-01-01} {countup 2020-01-01} {countup start}")]
    [InlineData("(command shoutout (1)) (alert boom.gif 5) (alerttext -d 3 Hello!) (playsound horn|50)", "{botcmd shoutout {1}} {alert boom.gif 5} {alerttext -d 3 Hello!} {playsound horn|50}")]
    public void ConvertWhenPhantomBotModuleTagsPresentMapsAllModulesAccurately(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest("pb", rawTemplate));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Stage3PhantomBotModules")]
    public void ConvertWhenPhantomBotHttpVariantsPresentConsolidatesIntoApiTagWithSecrets()
    {
        LegacyTagMigrationConverter converter = new();

        LegacyCommandMigrationResult jsonResult = converter.Convert(new LegacyCommandMigrationRequest(
            "stats",
            "(customapijson https://stats.example.com/v1/user?key=(token) data.0.name data.0.score)"));
        _ = jsonResult.ConvertedTemplate.Should().Be("{api \"https://stats.example.com/v1/user?key=$secret.STATS_TOKEN\" json=\"data.0.name\" json=\"data.0.score\"}");
        _ = jsonResult.DiscoveredSecrets.Should().ContainSingle(s => s.SecretName == "STATS_TOKEN" && s.AllowedHostPatterns.Contains("stats.example.com"));

        LegacyCommandMigrationResult postResult = converter.Convert(new LegacyCommandMigrationRequest(
            "webhook",
            "(customapipost https://hooks.example.com/trigger?auth=(token) {\"user\":\"(sender)\"})"));
        _ = postResult.ConvertedTemplate.Should().Be("{api POST \"https://hooks.example.com/trigger?auth=$secret.WEBHOOK_TOKEN\" body=\"\\{\\\"user\\\":\\\"{sender}\\\"\\}\"}");
        _ = postResult.DiscoveredSecrets.Should().ContainSingle(s => s.SecretName == "WEBHOOK_TOKEN" && s.AllowedHostPatterns.Contains("hooks.example.com"));
        _ = postResult.ParseResult.IsValid.Should().BeTrue();
        _ = postResult.ParseResult.Diagnostics.Should().BeEmpty();

        LegacyCommandMigrationResult apiResult = converter.Convert(new LegacyCommandMigrationRequest(
            "api",
            "(api GET https://api.example.com/items?token=(token))"));
        _ = apiResult.ConvertedTemplate.Should().Be("{api GET \"https://api.example.com/items?token=$secret.API_TOKEN\"}");
        _ = apiResult.DiscoveredSecrets.Should().ContainSingle(s => s.SecretName == "API_TOKEN" && s.AllowedHostPatterns.Contains("api.example.com"));
    }

    #endregion

    #region Stage3OtherBots

    [Theory]
    [Trait("Member", "Stage3OtherBots")]
    [InlineData("$(user) $(userid) $(touser) $(channel) $(1) $(9) $(querystring) $(querystring hello world)", "{sender} {sender_id} {target} {channel} {1} {9} {args} {url_encode \"hello world\"}")]
    [InlineData("$(urlfetch json https://api.example.com/v1?q=$(1))", "{api \"https://api.example.com/v1?q={1}\"}")]
    [InlineData("$(twitch $(channel) \"{{game}} | {{title}} | {{uptimeLength}} | {{viewers}} | {{followers}} | {{subscriberCount}}\")", "{game} | {title} | {uptime} | {viewers} | {followers} | {subscribers}")]
    [InlineData("$(twitch $(user) \"{{followAge}} {{followedAt}} {{accountAge}}\")", "{followage {sender}} {followdate {sender}} {account_age {sender}}")]
    [InlineData("$(time UTC \"HH:mm\") $(countdown 2030-01-01) $(countup 2020-01-01) $(count) $(commands)", "{time UTC HH:mm} {countdown 2030-01-01} {countup 2020-01-01} {count} {commands_list}")]
    public void ConvertWhenNightbotTagsPresentMapsAllTagsAccurately(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "nb",
            rawTemplate,
            LegacyBotSource.Nightbot));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [Trait("Member", "Stage3OtherBots")]
    [InlineData("${user} ${sender} ${user.name} ${user.id} ${touser} ${channel} ${1} ${1:} ${args} ${1|'nobody'}", "{sender} {sender} {bare_sender} {sender_id} {target} {channel} {1} {args 1} {args} {or {1} \"nobody\"}")]
    [InlineData("${toupper ${1}} ${tolower ${2}} ${repeat 3 'ha'}", "{upper {1}} {lower {2}} {repeat 3 ha}")]
    [InlineData("${urlfetch https://example.com/${1}} ${urlfetch.encode ${args}}", "{api \"https://example.com/{1}\"} {url_encode {args}}")]
    [InlineData("${customapi.https://api.example.com/stats} ${customapi.https://api.example.com/stats.kills.total}", "{api https://api.example.com/stats} {api https://api.example.com/stats json=\"kills.total\"}")]
    [InlineData("${random.1-100} ${random.5-10} ${random.pick 'a' 'b'} ${random.chatter} ${math 2 * ${1} + 1}", "{random_int 1 100} {random_int 5 10} {random_choice \"a\" \"b\"} {random_chatter} {math add {math mul 2 {1}} 1}")]
    [InlineData("${time.UTC} ${countdown 2030-01-01} ${countup 2020-01-01} ${count} ${getcount deaths}", "{time UTC} {countdown 2030-01-01} {countup 2020-01-01} {count} {counter_get \"deaths\"}")]
    [InlineData("${points} ${pointsname} ${cost} ${game} ${title} ${uptime} ${viewers} ${followers} ${subscribers} ${followage} ${time_online} ${level} ${accountage} ${lasttip}", "{points} {pointname} {price} {game} {title} {uptime} {viewers} {followers} {subscribers} {followage} {hours} {rank} {account_age} {last_tip}")]
    public void ConvertWhenStreamElementsTagsPresentMapsAllTagsAccurately(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "se",
            rawTemplate,
            LegacyBotSource.StreamElements));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [Trait("Member", "Stage3OtherBots")]
    [InlineData("$user $username $userid $touserid $target $mychannel $1 $num2 $msg Won $100!", "{sender} {sender} {bare_sender} {target} {target} {channel} {1} {2} {args} Won $100!")]
    [InlineData("$game $mygame $status $mystatus $uptime $viewers $subcount $followage $followdate $hours", "{game} {game} {title} {title} {uptime} {viewers} {subscribers} {followage} {followdate} {hours}")]
    [InlineData("$points $currencyname $cost $addpoints($user, 25) $removepoints(10, $target) $count $deathcounter $randuser $randnum(1, 20) $math(5 + $1) $time $rank", "{points} {pointname} {price} {points_add 25 {sender}} {points_take 10 {target}} {count} {count} {random_chatter} {random_int 1 20} {math add 5 {1}} {time} {rank}")]
    [InlineData("$readapi(https://api.example.com/$1)", "{api \"https://api.example.com/{1}\"}")]
    public void ConvertWhenStreamlabsTagsPresentMapsAllTagsAccurately(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "sl",
            rawTemplate,
            LegacyBotSource.Streamlabs));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Stage3OtherBots")]
    public void ConvertWhenStreamlabsFileTagsPresentConvertsToKvAndTracksVirtualFiles()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult readResult = converter.Convert(new LegacyCommandMigrationRequest(
            "slread",
            "$readfile(notes.txt)",
            LegacyBotSource.Streamlabs));
        _ = readResult.ConvertedTemplate.Should().Be("{kv_get \"files\" \"default\" \"notes.txt\"}");
        _ = readResult.ReferencedVirtualFiles.Should().Equal("notes.txt");
        _ = readResult.Diagnostics.Should().Contain(d => d.Code == MigrationDiagnosticCode.ReplacedFileTag);

        LegacyCommandMigrationResult writeResult = converter.Convert(new LegacyCommandMigrationRequest(
            "slwrite",
            "$overwritefile(log.txt, hello world, true)",
            LegacyBotSource.Streamlabs));
        _ = writeResult.ConvertedTemplate.Should().Be("{kv_append \"files\" \"default\" \"log.txt\" \"hello world\"}");
        _ = writeResult.ReferencedVirtualFiles.Should().Equal("log.txt");
    }

    [Theory]
    [Trait("Member", "Stage3OtherBots")]
    [InlineData("$user $streamer $arg[1] $arg[all] $target $counter[deaths] $game $streamTitle $uptime $viewers $followCount $subCount", "{sender} {channel} {1} {args} {target} {counter_get \"deaths\"} {game} {title} {uptime} {viewers} {followers} {subscribers}")]
    [InlineData("$readApi[https://api.example.com/$arg[1]] $points[$user] $math[$arg[1] * 3] $randomNumber[10, 50] $time[HH:mm]", "{api \"https://api.example.com/{1}\"} {points {sender}} {math mul {1} 3} {random_int 10 50} {time \"UTC\" \"HH:mm\"}")]
    public void ConvertWhenFirebotTagsPresentMapsAllTagsAccurately(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "fb",
            rawTemplate,
            LegacyBotSource.Firebot));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [Trait("Member", "Stage3OtherBots")]
    [InlineData("%user% %userName% %userId% %rawInput% %input0% %input8% %targetUser% %targetUserName% %targetUserId% %targetUserGame% %broadcastUser% %broadcastUserName% %game% %title% %counter%", "{sender} {sender} {sender_id} {args} {1} {9} {target} {target} {target} {game {target}} {channel} {channel} {game} {title} {count}")]
    public void ConvertWhenStreamerBotVariablesPresentMapsAllVariablesAccurately(string rawTemplate, string expected)
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "sb",
            rawTemplate,
            LegacyBotSource.StreamerBot));

        _ = result.ConvertedTemplate.Should().Be(expected);
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Stage3OtherBots")]
    public void ConvertWhenMixItUpVariablesPresentMapsDollarAndPercentVariables()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "miu",
            "$username $userid $arg1text $arg9text $allargs $targetusername $targetusergame $streamerusername $game $streamtitle %user%",
            LegacyBotSource.MixItUp));

        _ = result.ConvertedTemplate.Should().Be("{sender} {sender_id} {1} {9} {args} {target} {game {target}} {channel} {game} {title} {sender}");
        _ = result.ParseResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region Stage4ActionBlocksAndValidation

    [Fact]
    [Trait("Member", "Stage4ActionBlocksAndValidation")]
    public void ConvertWhenDiscordAndChannelPointSideEffectsPresentUpgradesToPlatformInvokeBlocks()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "modtools",
            "(setrole (touser), VIP) (removerole (1), Guest) (setdiscordplaying Elden Ring) (cpsetenabled reward-1, true) (cpsetpaused reward-2, false) Done!"));

        _ = result.ConvertedTemplate.Should().Be("Done!");
        _ = result.RequiresActionBlockUpgrade.Should().BeTrue();
        _ = result.UpgradedActions.Should().HaveCount(5);

        _ = result.UpgradedActions[0].ActionType.Should().Be("platform.invoke");
        _ = result.UpgradedActions[0].Parameters["operation"].Should().Be("discord.set_role");
        _ = result.UpgradedActions[0].Parameters["user"].Should().Be("{target}");
        _ = result.UpgradedActions[0].Parameters["role"].Should().Be("VIP");
        _ = result.UpgradedActions[0].RequiredPermissionKey.Should().Be(BuiltInPermissionKeys.UiCommandsActionsDiscordRoles);

        _ = result.UpgradedActions[1].ActionType.Should().Be("platform.invoke");
        _ = result.UpgradedActions[1].Parameters["operation"].Should().Be("discord.remove_role");
        _ = result.UpgradedActions[1].RequiredPermissionKey.Should().Be(BuiltInPermissionKeys.UiCommandsActionsDiscordRoles);

        _ = result.UpgradedActions[2].ActionType.Should().Be("platform.invoke");
        _ = result.UpgradedActions[2].Parameters["operation"].Should().Be("discord.set_presence");
        _ = result.UpgradedActions[2].Parameters["activity"].Should().Be("Elden Ring");

        _ = result.UpgradedActions[3].Parameters["operation"].Should().Be("twitch.channel_points.set_enabled");
        _ = result.UpgradedActions[3].Parameters["rewardId"].Should().Be("reward-1");
        _ = result.UpgradedActions[3].Parameters["enabled"].Should().Be("true");

        _ = result.UpgradedActions[4].Parameters["operation"].Should().Be("twitch.channel_points.set_paused");
        _ = result.UpgradedActions[4].Parameters["rewardId"].Should().Be("reward-2");
        _ = result.UpgradedActions[4].Parameters["paused"].Should().Be("false");

        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks
            && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    [Trait("Member", "Stage4ActionBlocksAndValidation")]
    public void ConvertWhenMassLoyaltyAndDelayAndSayTagsPresentUpgradesToActionBlocks()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "event",
            "(addpointstoall 100) (takepointsfromall 50) (delaycommand 5 shoutout (1)) (delaysay 10 Hi (sender)!) (twitchsay Twitch hello) (discordsay #general Discord hello)"));

        _ = result.ConvertedTemplate.Should().BeEmpty();
        _ = result.RequiresActionBlockUpgrade.Should().BeTrue();
        _ = result.UpgradedActions.Should().HaveCount(8);

        _ = result.UpgradedActions[0].ActionType.Should().Be("loyalty.adjust_all");
        _ = result.UpgradedActions[0].Parameters["operation"].Should().Be("add");
        _ = result.UpgradedActions[0].Parameters["amount"].Should().Be("100");
        _ = result.UpgradedActions[0].RequiresBroadcasterReview.Should().BeTrue();

        _ = result.UpgradedActions[1].ActionType.Should().Be("loyalty.adjust_all");
        _ = result.UpgradedActions[1].Parameters["operation"].Should().Be("take");
        _ = result.UpgradedActions[1].RequiresBroadcasterReview.Should().BeTrue();

        _ = result.UpgradedActions[2].ActionType.Should().Be("flow.delay");
        _ = result.UpgradedActions[2].Parameters["seconds"].Should().Be("5");
        _ = result.UpgradedActions[3].ActionType.Should().Be("command.run");
        _ = result.UpgradedActions[3].Parameters["command"].Should().Be("shoutout");
        _ = result.UpgradedActions[3].Parameters["arguments"].Should().Be("{1}");

        _ = result.UpgradedActions[4].ActionType.Should().Be("flow.delay");
        _ = result.UpgradedActions[4].Parameters["seconds"].Should().Be("10");
        _ = result.UpgradedActions[5].ActionType.Should().Be("chat.send");
        _ = result.UpgradedActions[5].Parameters["message"].Should().Be("Hi {sender}!");

        _ = result.UpgradedActions[6].ActionType.Should().Be("chat.send");
        _ = result.UpgradedActions[6].Parameters["targetPlatformId"].Should().Be("twitch");
        _ = result.UpgradedActions[6].Parameters["message"].Should().Be("Twitch hello");

        _ = result.UpgradedActions[7].ActionType.Should().Be("chat.send");
        _ = result.UpgradedActions[7].Parameters["targetPlatformId"].Should().Be("discord");
        _ = result.UpgradedActions[7].Parameters["targetChannel"].Should().Be("#general");
        _ = result.UpgradedActions[7].Parameters["message"].Should().Be("Discord hello");
    }

    [Fact]
    [Trait("Member", "Stage4ActionBlocksAndValidation")]
    public void ConvertWhenUnclosedBraceOrUnknownTagInOutputForwardsParseDiagnostics()
    {
        LegacyTagMigrationConverter converter = new();
        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "broken",
            "Hello ${unknown_se_tag} and {unclosed",
            LegacyBotSource.StreamElements));

        _ = result.ParseResult.IsValid.Should().BeFalse();
        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.TemplateParseWarning
            && d.Severity == MigrationDiagnosticSeverity.Warning);
        _ = result.Diagnostics.Should().Contain(d =>
            d.Code == MigrationDiagnosticCode.TemplateParseError
            && d.Severity == MigrationDiagnosticSeverity.Error);
    }

    [Fact]
    [Trait("Member", "Stage4ActionBlocksAndValidation")]
    public void ConvertWhenCustomTagRegistryIncludesPlatformTagsProducesZeroWarnings()
    {
        TagRegistry registry = CoreTagRegistryFactory.CreateDefaultRegistry();
        registry.Register(new StubTagHandler("game"));
        LegacyTagMigrationConverter converter = new(registry);

        LegacyCommandMigrationResult result = converter.Convert(new LegacyCommandMigrationRequest(
            "game",
            "Currently playing (game)!",
            LegacyBotSource.PhantomBot,
            "twitch"));

        _ = result.ConvertedTemplate.Should().Be("Currently playing {game}!");
        _ = result.ParseResult.IsValid.Should().BeTrue();
        _ = result.ParseResult.Diagnostics.Should().BeEmpty();
        _ = result.Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Minimal stub <see cref="ICommandTagHandler"/> used to verify custom registry forwarding in Stage 4.
    /// </summary>
    /// <param name="tagName">The tag name to register.</param>
    private sealed class StubTagHandler(string tagName) : ICommandTagHandler
    {
        /// <inheritdoc />
        public string TagName { get; } = tagName;

        /// <inheritdoc />
        public ValueTask<TagResult> EvaluateAsync(
            ITagEvaluationContext context,
            IReadOnlyList<TemplateNode> unevaluatedArgs,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(TagResult.Complete(string.Empty));
    }

    #endregion
}
