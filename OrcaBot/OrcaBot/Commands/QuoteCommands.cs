using BotCoreNET;
using BotCoreNET.CommandHandling;
using BotCoreNET.BotVars;
using BotCoreNET.Helpers;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OrcaBot.Commands
{
    class AddQuoteCommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.GuildOnly;
        public override HandledContexts ExecutionMethod => HandledContexts.GuildOnly;
        public override string Summary => "Adds an new Quote";
        public override bool IsShitposting => true;
        public override Argument[] Arguments => new Argument[]
        {
            new Argument("Message Link", "A message link used to find the message")
        };
        public override Precondition[] ExecutePreconditions => new Precondition[] { new HasRolePrecondition("podrole") };
        public override Precondition[] ViewPreconditions => new Precondition[] { new HasRolePrecondition("podrole") };

        public AddQuoteCommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        private Quote NewQuote;

        protected override async Task<ArgumentParseResult> ParseArgumentsGuildAsync(IGuildCommandContext context)
        {
            string[] argSections = context.Argument.Split("/", StringSplitOptions.RemoveEmptyEntries);
            if (argSections.Length < 3)
            {
                return new ArgumentParseResult(Arguments[0]);
            }

            if (!(ulong.TryParse(argSections[argSections.Length - 3], out ulong guildId) && ulong.TryParse(argSections[argSections.Length - 2], out ulong channelId) && ulong.TryParse(argSections[argSections.Length - 1], out ulong messageId)))
            {
                return new ArgumentParseResult(Arguments[0]);
            }

            SocketGuild guild = BotCore.Client.GetGuild(guildId);
            if (guild == null)
            {
                return new ArgumentParseResult(Arguments[0]);
            }
            if (guild.Id != context.Guild.Id)
            {
                return new ArgumentParseResult(Arguments[0], "Can only add quotes from this guild!");
            }

            SocketTextChannel channel = guild.GetTextChannel(channelId);
            if (channel == null)
            {
                return new ArgumentParseResult(Arguments[0]);
            }

            IMessage message = await channel.GetMessageAsync(messageId);

            if (message == null)
            {
                return new ArgumentParseResult(Arguments[0]);
            }

            NewQuote = new Quote(guildId, message);
            GuildBotVarCollection guildConfig = BotVarManager.GetGuildBotVarCollection(guildId);
            guildConfig.SetBotVar(NewQuote.GetBotVar());

            return ArgumentParseResult.SuccessfullParse;
        }

        protected override Task ExecuteGuild(IGuildCommandContext context)
        {
            return context.Channel.SendEmbedAsync(NewQuote);
        }
    }
    class GetQuotecommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.GuildOnly;

        public override HandledContexts ExecutionMethod => HandledContexts.GuildOnly;

        public override string Summary => "Retrieves a quote";
        public override bool IsShitposting => true;
        public override Argument[] Arguments => new Argument[]
        {
            new Argument("Quote Id", "Id to find the quote by. Leave empty for a random quote.", optional:true)
        };

        public GetQuotecommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        private Quote SelectedQuote;

        protected override Task<ArgumentParseResult> ParseArgumentsGuildAsync(IGuildCommandContext context)
        {
            GuildBotVarCollection guildConfig = BotVarManager.GetGuildBotVarCollection(context.Guild.Id);
            if (!guildConfig.TryGetBotVar("nextquoteid", out ulong quoteCount))
            {
                return Task.FromResult(new ArgumentParseResult("No quotes saved for this guild!"));
            }

            ulong quoteId;
            if (context.ArgumentCount > 0)
            {
                if (!ulong.TryParse(context.Argument, out quoteId))
                {
                    return Task.FromResult(new ArgumentParseResult(Arguments[0]));
                }

                if (quoteId >= quoteCount)
                {
                    return Task.FromResult(new ArgumentParseResult(Arguments[0], $"Out of range! Only `{quoteCount}` quotes stored!"));
                }

            }
            else
            {
                quoteId = (ulong)Macros.Rand.Next((int)quoteCount);
            }
            if (!guildConfig.TryGetBotVar(Quote.GetBotVarId(quoteId), out SelectedQuote))
            {
                if (context.ArgumentCount > 0)
                {
                    return Task.FromResult(new ArgumentParseResult($"Internal Error: Could not locate randomly selected Quote `{quoteId}`!"));
                }
                else
                {
                    return Task.FromResult(new ArgumentParseResult($"Could not locate a Quote with Id `{quoteId}`!"));
                }
            }
            return Task.FromResult(ArgumentParseResult.SuccessfullParse);
        }

        protected override Task ExecuteGuild(IGuildCommandContext context)
        {
            return context.Channel.SendEmbedAsync(SelectedQuote);
        }
    }
}
