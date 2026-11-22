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

using StreamActions.Core.Templates.Handlers;

namespace StreamActions.Core.Templates;

/// <summary>
/// Creates the default set of universal Core <see cref="ICommandTagHandler"/> instances and <see cref="TagRegistry"/>.
/// </summary>
public static class CoreTagRegistryFactory
{
    /// <summary>
    /// Creates all universal Core <see cref="ICommandTagHandler"/> instances.
    /// </summary>
    /// <returns>An immutable read-only list of universal Core tag handlers.</returns>
    public static IReadOnlyList<ICommandTagHandler> CreateDefaultHandlers()
    {
        List<ICommandTagHandler> handlers = new(69);

        for (int index = 1; index <= 9; index++)
        {
            handlers.Add(new PositionalArgumentTagHandler(index));
        }

        handlers.Add(new ArgsTagHandler());
        handlers.Add(new SenderTagHandler());
        handlers.Add(new AtSenderTagHandler());
        handlers.Add(new BareSenderTagHandler());
        handlers.Add(new SenderIdTagHandler());
        handlers.Add(new ChannelTagHandler());
        handlers.Add(new TargetTagHandler());
        handlers.Add(new CleanUserTagHandler());
        handlers.Add(new PointToUserTagHandler());
        handlers.Add(new SenderRankTagHandler());
        handlers.Add(new RankTagHandler());
        handlers.Add(new AccountAgeTagHandler());
        handlers.Add(new RandomChatterTagHandler());
        handlers.Add(new LastTipTagHandler());

        handlers.Add(new OrTagHandler());
        handlers.Add(new IfTagHandler());
        handlers.Add(new ComparisonTagHandler("eq"));
        handlers.Add(new ComparisonTagHandler("ne"));
        handlers.Add(new ComparisonTagHandler("gt"));
        handlers.Add(new ComparisonTagHandler("lt"));
        handlers.Add(new ComparisonTagHandler("contains"));

        handlers.Add(new RandomChoiceTagHandler("random"));
        handlers.Add(new RandomChoiceTagHandler("random_choice"));
        handlers.Add(new RandomIntTagHandler());
        handlers.Add(new RandomCodeTagHandler());

        handlers.Add(new MathTagHandler());

        handlers.Add(new UpperTagHandler());
        handlers.Add(new LowerTagHandler());
        handlers.Add(new ReplaceTagHandler());
        handlers.Add(new UrlEncodeTagHandler());
        handlers.Add(new RepeatTagHandler());
        handlers.Add(new NewLineTagHandler());

        handlers.Add(new TimeTagHandler());
        handlers.Add(new CountdownTagHandler());
        handlers.Add(new CountupTagHandler());

        handlers.Add(new VarTagHandler());
        handlers.Add(new CancelTagHandler());

        handlers.Add(new PointsTagHandler());
        handlers.Add(new PointNameTagHandler());
        handlers.Add(new PriceTagHandler());
        handlers.Add(new PayTagHandler());
        handlers.Add(new PointsAddTagHandler());
        handlers.Add(new PointsTakeTagHandler());
        handlers.Add(new PointsTakeOrCancelTagHandler());
        handlers.Add(new PointsTransferTagHandler());

        handlers.Add(new CountTagHandler());
        handlers.Add(new CounterGetTagHandler());
        handlers.Add(new CounterTotalTagHandler());
        handlers.Add(new CountResetTagHandler());

        handlers.Add(new KvGetTagHandler());
        handlers.Add(new KvSetTagHandler());
        handlers.Add(new KvRandTagHandler());
        handlers.Add(new KvAppendTagHandler());

        handlers.Add(new ApiTagHandler());
        handlers.Add(new JsonGetTagHandler());
        handlers.Add(new BotCmdTagHandler());
        handlers.Add(new CommandsListTagHandler());

        handlers.Add(new DelayCommandTagHandler());
        handlers.Add(new DelaySayTagHandler());
        handlers.Add(new TwitchSayTagHandler());
        handlers.Add(new DiscordSayTagHandler());
        handlers.Add(new AlertTagHandler());
        handlers.Add(new AlertTextTagHandler());
        handlers.Add(new PlaySoundTagHandler());

        return handlers.AsReadOnly();
    }

    /// <summary>
    /// Creates a new <see cref="TagRegistry"/> populated with all universal Core tag handlers.
    /// </summary>
    /// <returns>A configured <see cref="TagRegistry"/>.</returns>
    public static TagRegistry CreateDefaultRegistry() =>
        new(CreateDefaultHandlers());
}
