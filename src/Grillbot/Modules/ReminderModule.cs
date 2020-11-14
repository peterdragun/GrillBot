using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Grillbot.Attributes;
using Grillbot.Database.Entity.Users;
using Grillbot.Exceptions;
using Grillbot.Extensions;
using Grillbot.Extensions.Discord;
using Grillbot.Helpers;
using Grillbot.Models.Embed;
using Grillbot.Models.Embed.PaginatedEmbed;
using Grillbot.Services;
using Grillbot.Services.Reminder;

namespace Grillbot.Modules
{
    [ModuleID("ReminderModule")]
    [Name("Upozornění")]
    [Group("remind")]
    public class ReminderModule : BotModuleBase
    {
        private ReminderService Reminder { get; }
        private DiscordSocketClient Discord { get; }

        public ReminderModule(ReminderService reminder, PaginationService pagination, DiscordSocketClient discord) : base(paginationService: pagination)
        {
            Reminder = reminder;
            Discord = discord;
        }

        [Command("get")]
        [Summary("Získej moje upozornění.")]
        public async Task GetRemindsAsync()
        {
            try
            {
                var reminders = await Reminder.GetRemindersAsync(Context.Guild, Context.User);

                if (reminders.Count == 0)
                {
                    await ReplyAsync($"{Context.User.Mention} Nemáš žádné upozornění.");
                    return;
                }

                var embed = await CreatePaginatedEmbedAsync(reminders);
                await SendPaginatedEmbedAsync(embed);
            }
            catch (NotFoundException ex)
            {
                await ReplyAsync(ex.Message);
            }
        }

        [Command("")]
        [Summary("Upozorni uživatele")]
        [Remarks("Pokud chcete vytvořit upozornění pro sebe, tak použijte parametr `me`, nebo tagněte sebe. Pokud si přejete upozornit někoho jiného, tak jej tagněte. Jiná forma identifikace není podporována.\n" +
            "Pokud uživatel má deaktivované PMs, tak notifikace nebudou mít efekt.\nDatum a čas se zadává v následujících formátech:" +
            "\n- *\"dd/MM/yyyy HH:mm\"*,\n- *\"dd/MM/yyyy HH:mm(:ss)\"*,\n- *ISO 8601*,\n- *\"dd. MM. yyyy HH:mm(:ss)\"*\n**Vteřiny jsou nepovinné.\n" +
            "Nezapomínejte na uvozovky, jinak vám to správně nenačte datum a čas.**")]
        public async Task RemindUserAsync(string user, string at, [Remainder] string message)
        {
            try
            {
                var mentionedUser = user == "me" ? Context.User : null;
                if (mentionedUser == null)
                    mentionedUser = Context.Message.MentionedUsers.FirstOrDefault(o => o.Mention == user);

                var dateTimeAt = StringHelper.ParseDateTime(at);

                await Reminder.CreateReminderAsync(Context.Guild, Context.User, mentionedUser, dateTimeAt, message, Context.Message);

                await ReplyAsync($"Upozornění vytvořeno. Pokud si někdo přeje dostat toto upozornění také, tak ať dá na zprávu s příkazem reakci {ReminderDefinitions.CopyRemindEmoji.Name}");
                await Context.Message.AddReactionAsync(ReminderDefinitions.CopyRemindEmoji);
            }
            catch (Exception ex)
            {
                if (ex is ValidationException || ex is FormatException)
                {
                    await ReplyAsync(ex.Message);
                    return;
                }

                throw;
            }
        }

        [Command("all")]
        [Summary("Získej všechny upozornění.")]
        public async Task GetAllRemindsAsync()
        {
            var reminders = Reminder.GetAllReminders();

            if (reminders.Count == 0)
            {
                await ReplyAsync("Nikdo nemá žádné upozornění.");
                return;
            }

            var embed = await CreatePaginatedEmbedAsync(reminders, true);
            await SendPaginatedEmbedAsync(embed);
        }

        [Command("cancel")]
        [Summary("Předčasné ukončení upozornění.")]
        public async Task CancelReminderAsync(long id)
        {
            try
            {
                Reminder.CancelReminderWithoutNotification(id, Context.User as SocketGuildUser);
                await ReplyAsync("Upozornění bylo staženo.");
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    await ReplyAsync(ex.Message);
                    return;
                }

                throw;
            }
        }

        [Command("notify")]
        [Summary("Předčasné upozornění a ukončení upozornění.")]
        [Remarks("Pokud uživatel má deaktivované PMs, tak notifikace nebudou mít efekt.")]
        public async Task NotifyReminderAsync(long id)
        {
            try
            {
                await Reminder.CancelReminderWithNotificationAsync(id, Context.User as SocketGuildUser);
                await ReplyAsync("Notifikace a ukončení bylo dokončeno.");
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    await ReplyAsync(ex.Message);
                    return;
                }

                throw;
            }
        }

        [Command("leaderboard")]
        [Summary("Leaderboard uživatelů, kteří nejvíc odkládají připomenutí.")]
        public async Task RemindPostponeLeaderboardAsync()
        {
            var leaderboard = await Reminder.GetLeaderboard();

            var builder = new StringBuilder();
            for (int i = 0; i < leaderboard.Count; i++)
            {
                var user = leaderboard[i];
                builder.AppendLine($"> {(i + 1)}: *{user.Item1.GetDisplayName()}*: **{user.Item2.FormatWithSpaces()}x**");
            }

            var embed = new BotEmbed(Context.User, title: "Leaderboard nejvíce odkládajících osob.")
                .WithDescription(builder.ToString());

            await ReplyAsync(embed: embed.Build());
        }

        private async Task<PaginatedEmbed> CreatePaginatedEmbedAsync(List<Reminder> reminders, bool full = false)
        {
            var embed = new PaginatedEmbed()
            {
                Pages = new List<PaginatedEmbedPage>(),
                ResponseFor = Context.User,
                Title = full ? "Upozornění" : "Moje upozornění"
            };

            var chunks = reminders.SplitInParts(EmbedBuilder.MaxFieldCount);

            foreach (var chunk in chunks)
            {
                var page = new PaginatedEmbedPage(null);

                foreach (var reminder in chunk)
                {
                    var title = await CreateFieldTitleAsync(reminder, full);
                    if (string.IsNullOrEmpty(title))
                        continue;

                    page.AddField(title, $"ID: {reminder.RemindID}\nZpráva: {reminder.Message}\nZa: {(reminder.At - DateTime.Now).ToFullCzechTimeString()}");
                }

                if (page.AnyField())
                    embed.Pages.Add(page);
            }

            return embed;
        }

        private async Task<string> CreateFieldTitleAsync(Reminder reminder, bool full)
        {
            var guild = Discord.GetGuild(reminder.User.GuildIDSnowflake);

            if (guild == null)
                return null;

            if (reminder.FromUserID == null && !full)
                return "Moje";

            var fromUser = reminder.FromUserID == null ? null : await guild.GetUserFromGuildAsync(reminder.FromUser.UserIDSnowflake);

            if (full)
            {
                var toUser = await guild.GetUserFromGuildAsync(reminder.User.UserIDSnowflake);

                if (reminder.FromUserID == null)
                    return toUser.GetFullName();

                return $"Od uživatele {fromUser.GetFullName()} uživateli {toUser.GetFullName()}";
            }

            return fromUser?.GetFullName();
        }
    }
}
