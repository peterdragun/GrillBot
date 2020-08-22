using Discord.Commands;
using Discord.WebSocket;
using Grillbot.Attributes;
using Grillbot.Exceptions;
using Grillbot.Extensions;
using Grillbot.Extensions.Discord;
using Grillbot.Models.Embed;
using Grillbot.Models.Embed.PaginatedEmbed;
using Grillbot.Services;
using Grillbot.Services.Unverify;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Grillbot.Modules
{
    [Group("unverify")]
    [Name("Odebrání přístupu")]
    [ModuleID("UnverifyModule")]
    public class UnverifyModule : BotModuleBase
    {
        private UnverifyService Service { get; }

        public UnverifyModule(PaginationService paginationService, UnverifyService service) : base(paginationService: paginationService)
        {
            Service = service;
        }

        [Command("")]
        [Summary("Dočasné odebrání přístupu.")]
        [Remarks("Parametr time je ve formátu {cas}{m/h/d/M/y}, případně v ISO 8601. Např.: 30m, nebo `2020-08-17T23:59:59`.\nPopis: **m**: minuty, **h**: hodiny, **d**: dny, **M**: měsíce, **y**: roky.\n" +
            "Dále je důvod, proč daná osoba přišla o přístup\n\nCelý příkaz je pak vypadá např.:\n`{prefix}unverify 30m Přišel jsi o přístup @User1#1234 @User2#1354 ...`")]
        public async Task SetUnverifyAsync(string time, [Remainder] string reasonAndUserMentions = null)
        {
            try
            {
                if (await SetUnverifyRoutingAsync(time))
                    return;

                var usersToUnverify = Context.Message.MentionedUsers.OfType<SocketGuildUser>().ToList();

                if (usersToUnverify.Count == 0)
                    return;

                var messages = await Service.SetUnverifyAsync(usersToUnverify, time, reasonAndUserMentions, Context.Guild, Context.User);
                await ReplyChunkedAsync(messages, 1);
            }
            catch (Exception ex)
            {
                if (ex is ValidationException || ex is FormatException || ex is ArgumentException)
                {
                    await ReplyAsync(ex.Message);
                    return;
                }

                throw;
            }
        }

        private async Task<bool> SetUnverifyRoutingAsync(string route)
        {
            // Simply hack, because command routing cannot distinguish between a parameter and a function.
            switch (route)
            {
                case "list":
                    await ListUnverifyAsync();
                    return true;
                case "stats":
                    await StatsAsync();
                    return true;
                case "update":
                case "remove":
                    throw new ThrowHelpException();
            }

            return false;
        }

        [Command("remove")]
        [Summary("Předčasné vrácení přístupu.")]
        [Remarks("Zadává se identifikace uživatele. To znamená ID uživatele, tag, nebo jméno (username, nebo alias).\n\nCelý příkaz je pak vypadá např.:\n`{prefix}unverify remove @GrillBot`")]
        public async Task RemoveUnverifyAsync(SocketGuildUser user)
        {
            var message = await Service.RemoveUnverifyAsync(Context.Guild, user, Context.User);
            await ReplyAsync(message);
        }

        [Command("list")]
        [Summary("Seznam všech lidí, co má dočasně odebraný přístup.")]
        public async Task ListUnverifyAsync()
        {
            var profiles = await Service.GetCurrentUnverifies(Context.Guild);

            if (profiles.Count == 0)
            {
                await ReplyAsync("Nikdo zatím nemá odebraný přístup.");
                return;
            }

            var pages = new List<PaginatedEmbedPage>();

            foreach (var profile in profiles.Select(o => o.Profile))
            {
                var page = new PaginatedEmbedPage($"**{profile.DestinationUser.GetFullName()}**", thumbnail: profile.DestinationUser.GetUserAvatarUrl());

                page.AddField("ID", profile.DestinationUser.Id.ToString());
                page.AddField("Začátek", profile.StartDateTime.ToLocaleDatetime(), true);
                page.AddField("Konec", profile.EndDateTime.ToLocaleDatetime(), true);
                page.AddField("Končí za", (profile.EndDateTime - DateTime.Now).ToFullCzechTimeString(), true);

                if (profile.RolesToKeep.Count > 0)
                    page.AddField("Ponechané role", string.Join(", ", profile.RolesToKeep.Select(o => o.Mention)));

                if (profile.RolesToRemove.Count > 0)
                    page.AddField("Odebrané role", string.Join(", ", profile.RolesToRemove.Select(o => o.Mention)));

                if (profile.ChannelsToKeep.Count > 0)
                    page.AddField("Ponechané kanály", string.Join(", ", profile.ChannelsToKeep.Select(o => $"<#{o.Channel.Id}>")));

                if (profile.ChannelsToRemove.Count > 0)
                    page.AddField("Odebrané kanály", string.Join(", ", profile.ChannelsToRemove.Select(o => $"<#{o.Channel.Id}>")));

                page.AddField("Důvod", profile.Reason);
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
        [Summary("Aktualizace času u záznamu o dočasném odebrání přístupu.")]
        [Remarks("Parametr time je ve formátu {cas}{m/h/d/M/y}, případně v ISO 8601. Např.: 30m, nebo `2020-08-17T23:59:59`.\nPopis: **m**: minuty, **h**: hodiny, **d**: dny, **M**: měsíce, **y**: roky.\n" +
            "Zadává se identifikace uživatele. To znamená ID uživatele, tag, nebo jméno (username, nebo alias).\n\nCelý příkaz je pak vypadá např.:\n`{prefix}unverify update @GrillBot 45m`")]
        public async Task UpdateUnverifyAsync(SocketGuildUser user, string time)
        {
            try
            {
                var message = await Service.UpdateUnverifyAsync(user, Context.Guild, time, Context.User);
                await ReplyAsync(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is NotFoundException || ex is ValidationException)
                {
                    await ReplyAsync(ex.Message);
                    return;
                }

                throw;
            }
        }

        [Command("stats")]
        [Summary("Statistiky unverify")]
        public async Task StatsAsync()
        {
            var unverifies = await Service.GetCurrentUnverifies(Context.Guild);

            var embed = new BotEmbed(Context.User, title: "Statistiky unverify")
                .AddField("SelfUnverify", unverifies.Count(o => o.Profile.IsSelfUnverify).FormatWithSpaces(), true)
                .AddField("Unverify", unverifies.Count(o => !o.Profile.IsSelfUnverify).FormatWithSpaces(), true)
                .AddField("Celkem", unverifies.Count.FormatWithSpaces(), true);

            await ReplyAsync(embed: embed.Build());
        }

        protected override void AfterExecute(CommandInfo command)
        {
            Service.Dispose();

            base.AfterExecute(command);
        }
    }
}