﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GrilBot.Exceptions;
using GrilBot.Modules;
using GrilBot.Services;
using GrilBot.Services.Statistics;

namespace GrilBot
{
    public class MessageHandler : IConfigChangeable
    {
        private DiscordSocketClient Client { get; }
        private CommandService Commands { get; }
        private IServiceProvider Services { get; }
        private Statistics Statistics { get; }
        private AutoReplyModule AutoReply { get; }
        private EmoteChain EmoteChain { get; }

        private IConfigurationRoot Config { get; set; }

        public MessageHandler(DiscordSocketClient client, CommandService commands, IConfigurationRoot config, IServiceProvider services,
            Statistics statistics, AutoReplyModule autoReply, EmoteChain emoteChain)
        {
            Client = client;
            Commands = commands;
            Services = services;
            Statistics = statistics;
            AutoReply = autoReply;
            EmoteChain = emoteChain;
            Config = config;

            Client.MessageReceived += OnMessageReceivedAsync;
            Client.MessageDeleted += OnMessageDeletedAsync;
            Client.UserJoined += OnUserJoinedOnServerAsync;
        }

        private async Task OnUserJoinedOnServerAsync(SocketGuildUser user)
        {
            var message = Config["Discord:UserJoinedMessage"];

            if (!string.IsNullOrEmpty(message))
                await user.SendMessageAsync(message);
        }

        private async Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            if (message.HasValue && (message.Value.Content.StartsWith(Config["CommandPrefix"]) || message.Value.Author.IsBot)) return;

            await Statistics.ChannelStats.DecrementCounter(channel.Id);
        }

        private async Task OnMessageReceivedAsync(SocketMessage message)
        {
            if (!(message is SocketUserMessage userMessage) || userMessage.Author.IsBot) return;

            var messageStopwatch = new Stopwatch();
            messageStopwatch.Start();

            try
            {
                var commandStopwatch = new Stopwatch();
                var context = new SocketCommandContext(Client, userMessage);

                int argPos = 0;
                if (userMessage.HasStringPrefix(Config["CommandPrefix"], ref argPos))
                {
                    commandStopwatch.Start();
                    var result = await Commands.ExecuteAsync(context, argPos, Services);
                    commandStopwatch.Stop();

                    if (!result.IsSuccess && result.Error != null)
                    {
                        switch (result.Error.Value)
                        {
                            case CommandError.UnknownCommand: return;
                            case CommandError.UnmetPrecondition:
                                await context.Channel.SendMessageAsync($"Na tento příkaz nemáš dostatečná práva.");
                                break;
                            case CommandError.BadArgCount:
                                await context.Channel.SendMessageAsync($"Nedostatečný počet parametrů.");
                                break;
                            default:
                                throw new BotException(result);
                        }
                    }

                    var command = message.Content.Split(' ')[0];

                    Statistics.LogCall(command, commandStopwatch.ElapsedMilliseconds);
                    EmoteChain.Cleanup(context.Channel);
                }
                else
                {
                    await Statistics.ChannelStats.IncrementCounter(userMessage.Channel.Id);
                    await AutoReply.TryReply(userMessage);
                    await EmoteChain.ProcessChain(context);
                }
            }
            finally
            {
                messageStopwatch.Stop();
                Statistics.ComputeAvgReact(messageStopwatch.ElapsedMilliseconds);
            }
        }

        public void ConfigChanged(IConfigurationRoot newConfig)
        {
            Config = newConfig;
        }
    }
}