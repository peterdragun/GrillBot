﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Grillbot.Exceptions;
using Grillbot.Extensions;
using Grillbot.Extensions.Discord;
using Grillbot.Models.Embed;
using Grillbot.Models.PaginatedEmbed;
using Grillbot.Services;
using Grillbot.Services.Preconditions;
using Grillbot.Services.TempUnverify;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grillbot.Modules
{
    [Group("unverify")]
    [RequirePermissions]
    [Name("Odebrání přístupu")]
    public class TempUnverifyModule : BotModuleBase
    {
        private TempUnverifyService UnverifyService { get; }

        public TempUnverifyModule(TempUnverifyService unverifyService, PaginationService paginationService) : base(paginationService: paginationService)
        {
            UnverifyService = unverifyService;
        }

        [Command("")]
        [Summary("Dočasné odebrání rolí.")]
        [Remarks("Parametr time je ve formátu {cas}{m/h/d}. Např.: 30m.\nPopis: m: minuty, h: hodiny, d: dny.\n" +
            "Dále je důvod, proč daná osoba přišla o role. A nakonec seznam (mentions) uživatelů.\n" +
            "Celý příkaz je pak vypadá např.:\n{prefix}unverify 30m Přišel jsi o role @User1#1234 @User2#1354 ...")]
        public async Task SetUnverifyAsync(string time, [Remainder] string reasonAndUserMentions = null)
        {
            // Simply hack, because command routing cannot distinguish between a parameter and a function.
            switch (time)
            {
                case "list":
                    await ListUnverifyAsync().ConfigureAwait(false);
                    return;
                case "remove":
                    if (string.IsNullOrEmpty(reasonAndUserMentions)) throw new ThrowHelpException();
                    await RemoveUnverifyAsync(Convert.ToInt32(reasonAndUserMentions.Split(' ')[0])).ConfigureAwait(false);
                    return;
                case "update":
                    if (string.IsNullOrEmpty(reasonAndUserMentions)) throw new ThrowHelpException();
                    var fields = reasonAndUserMentions.Split(' ');
                    if (fields.Length < 2)
                    {
                        await ReplyAsync("Chybí parametry.").ConfigureAwait(false);
                        return;
                    }
                    await UpdateUnverifyAsync(Convert.ToInt32(fields[0]), fields[1]).ConfigureAwait(false);
                    return;
            }

            var usersToUnverify = Context.Message.MentionedUsers.OfType<SocketGuildUser>().ToList();

            if (usersToUnverify.Count > 0)
            {
                var message = await UnverifyService.RemoveAccessAsync(usersToUnverify, time,
                    reasonAndUserMentions, Context.Guild, Context.User).ConfigureAwait(false);
                await ReplyAsync(message).ConfigureAwait(false);
            }
        }

        [Command("remove")]
        [Summary("Předčasné vrácení rolí.")]
        public async Task RemoveUnverifyAsync(int id)
        {
            var message = await UnverifyService.ReturnAccessAsync(id, Context.User).ConfigureAwait(false);
            await ReplyAsync(message).ConfigureAwait(false);
        }

        [Command("list")]
        [Summary("Seznam všech lidí, co má dočasně odebrané role.")]
        public async Task ListUnverifyAsync()
        {
            var users = await UnverifyService.ListPersonsAsync(Context.Guild);
            var pages = new List<PaginatedEmbedPage>();

            foreach (var user in users)
            {
                var page = new PaginatedEmbedPage($"**{user.Username}**");

                page.AddField(new EmbedFieldBuilder().WithName("ID").WithValue(user.ID));
                page.AddField(new EmbedFieldBuilder().WithName("Do kdy").WithValue(user.EndDateTime.ToLocaleDatetime()));
                page.AddField(new EmbedFieldBuilder().WithName("Role").WithValue(string.Join(", ", user.Roles)));
                page.AddField(new EmbedFieldBuilder().WithName("Extra kanály").WithValue(user.ChannelOverrideList));
                page.AddField(new EmbedFieldBuilder().WithName("Důvod").WithValue(user.Reason));

                pages.Add(page);
            }

            var embed = new PaginatedEmbed()
            {
                Title = "Seznam osob s odebraným přístupem",
                Pages = pages,
                ResponseFor = Context.User,
                Thumbnail = Context.Client.CurrentUser.GetUserAvatarUrl()
            };

            await SendPaginatedEmbedAsync(embed);
        }

        [Command("update")]
        [Summary("Aktualizace času u záznamu o dočasném odebrání rolí.")]
        public async Task UpdateUnverifyAsync(int id, string time)
        {
            var message = await UnverifyService.UpdateUnverifyAsync(id, time, Context.User).ConfigureAwait(false);
            await ReplyAsync(message).ConfigureAwait(false);
        }
    }
}
