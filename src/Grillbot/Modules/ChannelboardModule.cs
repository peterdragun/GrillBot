using Discord;
using Discord.Commands;
using System.Text;
using System.Threading.Tasks;
using Grillbot.Extensions.Discord;
using Grillbot.Services.Channelboard;
using Grillbot.Models.Embed;
using Grillbot.Extensions;
using Grillbot.Attributes;
using System;

namespace Grillbot.Modules
{
    [Group("channelboard")]
    [Name("Channel leaderboards")]
    [ModuleID("ChannelboardModule")]
    public class ChannelboardModule : BotModuleBase
    {
        public ChannelboardModule(IServiceProvider provider) : base(provider: provider)
        {
        }

        [Command("")]
        [Summary("Počet zpráv v kanálech na serveru.")]
        [Remarks("Posílá statistiku do PM.")]
        public async Task ChannelboardAsync()
        {
            using var service = GetService<ChannelStats>();

            var data = await service.Service.GetChannelboardDataAsync(Context.Guild, Context.User, ChannelStats.ChannelboardTakeTop);

            if (data.Count == 0)
                await Context.Message.Author.SendPrivateMessageAsync("Ještě nejsou zaznamenány žádné kanály pro tento server.");

            var embed = new BotEmbed(Context.User, null, "Channel leaderboard");

            var messageBuilder = new StringBuilder();
            for (int i = 0; i < data.Count; i++)
            {
                var channelBoardItem = data[i];

                messageBuilder
                    .Append(i + 1).Append(": ").Append(channelBoardItem.Channel.Name)
                    .Append(" - ").AppendLine(channelBoardItem.Count.FormatWithSpaces());
            }

            embed.AddField("=======================", messageBuilder.ToString(), false);
            await Context.Message.Author.SendPrivateMessageAsync(embedBuilder: embed.GetBuilder());
        }

        [Command("web")]
        [Summary("Webový leaderboard.")]
        public async Task ChannelboardWebAsync()
        {
            using var service = GetService<ChannelboardWeb>();
            var url = await service.Service.GetWebUrlAsync(Context);

            var message = $"Tady máš odkaz na channelboard serveru **{Context.Guild.Name}**: {url}.";
            await Context.Message.Author.SendPrivateMessageAsync(message).ConfigureAwait(false);
        }

        [Command("")]
        [Summary("Počet zpráv v místnosti.")]
        public async Task ChannelboardForRoomAsync(IChannel channel)
        {
            using var service = GetService<ChannelStats>();

            var value = await service.Service.GetValueAsync(Context.Guild, channel.Id, Context.User);

            if (value == null)
            {
                await ReplyAsync("Do této místnosti nemáš dostatečná práva.");
                return;
            }

            var formatedMessageCount = value.Item2.FormatWithSpaces();
            var message = $"Aktuální počet zpráv v místnosti **#{channel.Name}** je **{formatedMessageCount}** a v příčce se drží na **{value.Item1}**. pozici.";

            await Context.Message.Author.SendPrivateMessageAsync(message);
            await Context.Message.DeleteAsync(new RequestOptions() { AuditLogReason = "Channelboard security" }).ConfigureAwait(false);
        }

        [Command("clear")]
        [Summary("Úklid starých kanálů.")]
        public async Task CleanOldChannels()
        {
            using var service = GetService<ChannelStats>();

            var clearedChannels = await service.Service.CleanOldChannels(Context.Guild);

            if (clearedChannels == null)
            {
                await ReplyAsync("> Není co čistit.");
                return;
            }

            await ReplyChunkedAsync(clearedChannels, 10);
            await ReplyAsync("> Čištění dokončeno.").ConfigureAwait(false);
        }
    }
}
