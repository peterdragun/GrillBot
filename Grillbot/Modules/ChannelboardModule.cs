﻿using Discord;
using Discord.Commands;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grillbot.Helpers;
using Grillbot.Services.Statistics;
using Grillbot.Services.Preconditions;
using Discord.WebSocket;
using Grillbot.Exceptions;
using Newtonsoft.Json;
using Grillbot.Extensions.Discord;
using Grillbot.Services.Channelboard;
using System.Diagnostics.CodeAnalysis;

namespace Grillbot.Modules
{
    [RequirePermissions]
    [Name("Channel leaderboards")]
    public class ChannelboardModule : BotModuleBase
    {
        private ChannelStats Stats { get; }
        private ChannelboardWeb ChannelboardWeb { get; }

        public ChannelboardModule(ChannelStats channelStats, ChannelboardWeb channelboardWeb)
        {
            Stats = channelStats;
            ChannelboardWeb = channelboardWeb;
        }

        [Command("channelboard")]
        public async Task ChannelboardAsync() => await ChannelboardAsync(ChannelStats.ChannelboardTakeTop).ConfigureAwait(false);

        [Command("channelboard")]
        [Remarks("Možnost zvolit TOP N kanálů.")]
        public async Task ChannelboardAsync(int takeTop)
        {
            var data = await Stats.GetChannelboardDataAsync(Context.Guild, Context.User);

            if (data.Count == 0)
                await Context.Message.Author.SendPrivateMessageAsync("Ještě nejsou zaznamenány žádné kanály pro tento server.");

            var messageBuilder = new StringBuilder()
                .AppendLine("=======================")
                .AppendLine("|\tCHANNEL LEADERBOARD\t|")
                .AppendLine("=======================");

            var channelboardData = data.Take(takeTop).ToList();
            for (int i = 0; i < channelboardData.Count; i++)
            {
                var channelBoardItem = channelboardData[i];

                messageBuilder
                    .Append(i + 1).Append(": ").Append(channelBoardItem.ChannelName)
                    .Append(" - ").AppendLine(FormatHelper.FormatWithSpaces(channelBoardItem.Count));
            }

            await Context.Message.Author.SendPrivateMessageAsync(messageBuilder.ToString());
        }

        [Command("channelboardweb")]
        [Summary("Webový leaderboard.")]
        public async Task ChannelboardWebAsync()
        {
            var url = ChannelboardWeb.GetWebUrl(Context);

            var message = $"Tady máš odkaz na channelboard serveru **{Context.Guild.Name}**: {url}.";
            await Context.Message.Author.SendPrivateMessageAsync(message).ConfigureAwait(false);
        }

        [DisabledPM]
        [Command("channelboard", true)]
        [Summary("Počet zpráv v místnosti.")]
        public async Task ChannelboardForRoomAsync()
        {
            await DoAsync(async () =>
            {
                if (Context.Message.Tags.Count == 0)
                    throw new ArgumentException("Nic jsi netagnul.");

                var channelMention = Context.Message.Tags.FirstOrDefault(o => o.Type == TagType.ChannelMention);
                if (channelMention == null)
                    throw new ArgumentException("Netagnul jsi žádný kanál");

                if (!(channelMention.Value is ISocketMessageChannel channel))
                    throw new BotException($"Discord.NET uznal, že je to ChannelMention, ale nepovedlo se mi to načíst jako kanál. Prověřte to někdo pls. (Message: {Context.Message.Content}, Tags: {JsonConvert.SerializeObject(Context.Message.Tags)})");

                var value = await Stats.GetValueAsync(Context.Guild, channel.Id, Context.User);

                if(value == null)
                    throw new ArgumentException("Do této místnosti nemáš dostatečná práva.");

                var formatedMessageCount = FormatHelper.FormatWithSpaces(value.Item2);
                var message = $"Aktuální počet zpráv v místnosti **{channel.Name}** je **{formatedMessageCount}** a v příčce se drží na **{value.Item1}**. pozici.";

                await Context.Message.Author.SendPrivateMessageAsync(message);
                await Context.Message.DeleteAsync(new RequestOptions() { AuditLogReason = "Channelboard security" }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [Command("cleanOldChannels")]
        public async Task CleanOldChannels()
        {
            await DoAsync(async () =>
            {
                var clearedChannels = await Stats.CleanOldChannels(Context.Guild);

                await ReplyChunkedAsync(clearedChannels, 10);
                await ReplyAsync("Čištění dokončeno.").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
