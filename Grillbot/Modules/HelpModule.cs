﻿using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grillbot.Services;
using Discord.WebSocket;

namespace Grillbot.Modules
{
    [Name("Nápověda")]
    public class HelpModule : BotModuleBase
    {
        private CommandService CommandService { get; }
        private IConfiguration Config { get; }
        private IServiceProvider Services { get; }

        public HelpModule(CommandService commandService, IConfiguration config, IServiceProvider services)
        {
            CommandService = commandService;
            Config = config;
            Services = services;
        }

        [Command("grillhelp")]
        [DisabledCheck(RoleGroupName = "Help")]
        [RequireRoleOrAdmin(RoleGroupName = "Help")]
        public async Task HelpAsync()
        {
            var guildUser = Context.Guild.GetUser(Context.User.Id);

            var embed = new EmbedBuilder()
            {
                Color = Color.Blue,
                Title = $"Nápověda pro uživatele {GetUsersFullName(guildUser)} ({GetBotBestPermissions(guildUser)})"
            };

            foreach(var module in CommandService.Modules)
            {
                var descBuilder = new StringBuilder();

                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context, Services);

                    if (result.IsSuccess)
                    {
                        descBuilder
                            .Append(Config["CommandPrefix"])
                            .Append(cmd.Name).Append(' ')
                            .Append(string.Join(" ", cmd.Parameters.Select(o => "{" + o.Name + "}")));

                        if(!string.IsNullOrEmpty(cmd.Summary))
                            descBuilder.Append(" - ").AppendLine(cmd.Summary);
                        else
                            descBuilder.AppendLine();
                    }
                }

                if (descBuilder.Length > 0)
                    embed.AddField(x => x.WithName(module.Name).WithValue(descBuilder.ToString()));
            }

            await ReplyAsync("", embed: embed.Build());
        }

        [Command("grillhelp")]
        [DisabledCheck(RoleGroupName = "Help")]
        [RequireRoleOrAdmin(RoleGroupName = "Help")]
        public async Task HelpAsync(string command)
        {
            var result = CommandService.Search(Context, command);

            if (!result.IsSuccess)
            {
                await ReplyAsync($"Je mi to líto, ale příkaz **{command}** neznám.");
                return;
            }

            var embedBuilder = new EmbedBuilder()
            {
                Color = Color.Blue,
                Title = $"Tady máš různé varianty příkazů na **{command}**"
            };

            foreach (var cmd in result.Commands.Select(o => o.Command))
            {
                var haveAccess = await cmd.CheckPreconditionsAsync(Context, Services);

                if (haveAccess.IsSuccess)
                {
                    var valueBuilder = new StringBuilder();

                    if (cmd.Parameters.Count > 0)
                    {
                        valueBuilder
                            .Append("Parametry: ")
                            .AppendLine(string.Join(", ", cmd.Parameters.Select(p => p.Name)));
                    }

                    if(!string.IsNullOrEmpty(cmd.Summary))
                        valueBuilder.AppendLine(cmd.Summary);

                    if (!string.IsNullOrEmpty(cmd.Remarks))
                        valueBuilder.Append("Poznámka: ").AppendLine(cmd.Remarks);

                    string commandDesc = valueBuilder.ToString();
                    embedBuilder.AddField(x =>
                    {
                        x.WithValue(string.IsNullOrEmpty(commandDesc) ? "Bez parametrů a popisu" : commandDesc)
                         .WithName(string.Join(", ", cmd.Aliases));
                    });
                }
            }

            if(embedBuilder.Fields.Count == 0)
                embedBuilder.Description = $"Na metodu **{command}** nemáš potřebná oprávnění";

            await ReplyAsync(embed: embedBuilder.Build());
        }

        private string GetBotBestPermissions(SocketGuildUser user)
        {
            var serverAdmins = Config.GetSection("Discord:Administrators").GetChildren().Select(o => Convert.ToUInt64(o.Value));

            if (serverAdmins.Any(o => o == user.Id))
                return "BotAdmin";

            return user.Roles.FirstOrDefault(o => o.Position == user.Roles.Max(x => x.Position)).Name;
        }
    }
}
