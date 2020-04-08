﻿using Discord.WebSocket;
using Grillbot.Extensions.Discord;
using Grillbot.Models.Config;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Grillbot.Services.Permissions
{
    public class WebAuthenticationService
    {
        private ILogger<WebAuthenticationService> Logger { get; }
        private DiscordSocketClient DiscordClient { get; }
        private Configuration Config { get; }

        public WebAuthenticationService(ILogger<WebAuthenticationService> logger, DiscordSocketClient client, IOptions<Configuration> options)
        {
            Logger = logger;
            DiscordClient = client;
            Config = options.Value;
        }

        public async Task<ClaimsIdentity> Authorize(string username, string password, ulong guildID)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || guildID == default)
                return null;

            SocketGuildUser user;
            try
            {
                var guild = DiscordClient.GetGuild(guildID);

                if (guild == null)
                    return null;

                var usernameFields = username.Split('#');
                if (usernameFields.Length != 2)
                    return null;

                user = await guild.GetUserFromGuildAsync(usernameFields[0], usernameFields[1]);

                if (user == null || user.Id.ToString() != password)
                    return null;

                if (!Config.IsUserBotAdmin(user.Id))
                    return null;

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.FindHighestRole().Name)
                };

                return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "");
                return null;
            }
        }
    }
}
