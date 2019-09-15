using BotCoreNET.CommandHandling;
using BotCoreNET.BotVars;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using BotCoreNET;
using Discord;
using BotCoreNET.Helpers;
using JSON;

namespace OrcaBot.Commands
{
    class SetMacroCommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.GuildOnly;
        public override HandledContexts ExecutionMethod => HandledContexts.GuildOnly;
        public override string Summary => "Add or overwrite a macro";
        public override Argument[] Arguments => new Argument[]
        {
            new Argument("Identifier", "A basic string to identify the macro"),
            new Argument("JSON or Message Link", "An message JSON or a link to a message. `remove` to instead delete an existing embed")
        };
        public override Precondition[] ExecutePreconditions => new Precondition[] { new HasRolePrecondition("podrole") };
        public override Precondition[] ViewPreconditions => new Precondition[] { new HasRolePrecondition("podrole") };

        public SetMacroCommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        private bool Delete;
        private string MacroIdentifier;
        private Macro SelectedMacro;

        protected override async Task<ArgumentParseResult> ParseArgumentsGuildAsync(IGuildCommandContext context)
        {
            MacroIdentifier = context.Arguments.First;

            if (!StoredMessagesService.IsValidMacroName(MacroIdentifier))
            {
                return new ArgumentParseResult(Arguments[0], "Not a valid macro name!");
            }

            context.Arguments.Index++;

            if (context.Arguments.First.ToLower() == "remove")
            {
                Delete = true;
                SelectedMacro = null;
                return ArgumentParseResult.SuccessfullParse;
            }
            else
            {
                Delete = false;
                JSONContainer json;
                if (context.Arguments.First.StartsWith("http"))
                {
                    string[] argSections = context.Arguments.First.Split("/", StringSplitOptions.RemoveEmptyEntries);
                    if (argSections.Length < 3)
                    {
                        return new ArgumentParseResult(Arguments[1]);
                    }

                    if (!(ulong.TryParse(argSections[argSections.Length - 3], out ulong guildId) && ulong.TryParse(argSections[argSections.Length - 2], out ulong channelId) && ulong.TryParse(argSections[argSections.Length - 1], out ulong messageId)))
                    {
                        return new ArgumentParseResult(Arguments[1]);
                    }

                    SocketGuild guild = BotCore.Client.GetGuild(guildId);
                    if (guild == null)
                    {
                        return new ArgumentParseResult(Arguments[1]);
                    }

                    SocketTextChannel channel = guild.GetTextChannel(channelId);
                    if (channel == null)
                    {
                        return new ArgumentParseResult(Arguments[1]);
                    }

                    IMessage message = await channel.GetMessageAsync(messageId);

                    if (message == null)
                    {
                        return new ArgumentParseResult(Arguments[1]);
                    }

                    EmbedHelper.GetJSONFromUserMessage(message, out json);
                    SelectedMacro = new Macro(MacroIdentifier, json);
                }
                else
                {
                    string embedText = context.RemoveArgumentsFront(1).Replace("[3`]", "```");
                    if (!JSONContainer.TryParse(embedText, out json, out string error))
                    {
                        return new ArgumentParseResult(Arguments[1]);
                    }

                    SelectedMacro = new Macro(MacroIdentifier, json);
                    if (!SelectedMacro.Build(out _, out _, out error))
                    {
                        return new ArgumentParseResult(Arguments[1], error);
                    }
                }

                return ArgumentParseResult.SuccessfullParse;
            }
        }

        protected override Task ExecuteGuild(IGuildCommandContext context)
        {
            StoredMessagesService messagesService = StoredMessagesService.GetMessagesService(context.Guild.Id);
            if (Delete)
            {
                messagesService.RemoveMacro(MacroIdentifier);
                return context.Channel.SendEmbedAsync($"Deleted macro `{MacroIdentifier}`");
            }
            else
            {
                messagesService.SetMacro(SelectedMacro);
                SelectedMacro.Build(out EmbedBuilder embed, out string messagecontent, out _);
                return context.Channel.SendMessageAsync(text: messagecontent, embed: embed.Build());
            }
        }
    }

    class ListMacrosCommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.None;
        public override HandledContexts ExecutionMethod => HandledContexts.GuildOnly;
        public override string Summary => "Lists all available macros";

        public ListMacrosCommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        protected override Task ExecuteGuild(IGuildCommandContext context)
        {
            StoredMessagesService messagesService = StoredMessagesService.GetMessagesService(context.Guild.Id);

            if (messagesService.Macros.Count == 0)
            {
                return context.Channel.SendEmbedAsync("No macros stored for this guild!");
            }

            return context.Channel.SendEmbedAsync(new EmbedBuilder()
            {
                Title = $"Macros - {messagesService.Macros.Count}",
                Color = BotCore.EmbedColor,
                Description = messagesService.Macros.OperationJoinReadonly(", ", macro => { return $"`{macro.Identifier}`"; })
            });
        }
    }
}
