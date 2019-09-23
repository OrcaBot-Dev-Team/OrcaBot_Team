using BotCoreNET.CommandHandling;

namespace OrcaBot
{
    class CustomCommandParser : BuiltInCommandParser
    {
        public override string CommandSyntax(string commandidentifier)
        {
            return $"{Prefix}{commandidentifier}";
        }

        public override string CommandSyntax(string commandidentifier, params Argument[] arguments)
        {
            if (arguments.Length == 0)
            {
                return $"{Prefix}{commandidentifier}";
            }
            else
            {
                return $"{Prefix}{commandidentifier}: {string.Join(", ", arguments as object[])}";
            }
        }

        public override string CommandSyntax(string commandidentifier, params string[] arguments)
        {
            if (arguments.Length == 0)
            {
                return $"{Prefix}{commandidentifier}";
            }
            else
            {
                return $"{Prefix}{commandidentifier}: {string.Join(", ", arguments as object[])}";
            }
        }

        public override ICommandContext ParseCommand(IMessageContext dmContext)
        {
            string commandIdentifier = null;
            string argumentSection;
            IndexArray<string> arguments;
            Command interpretedCommand = null;
            CommandSearchResult commandSearch;

            string message = dmContext.Content.Substring(Prefix.Length).TrimStart();

            int firstSpace = message.IndexOf(' ');

            if (firstSpace == -1)
            {
                commandIdentifier = message.Trim();
                commandSearch = CommandCollection.TryFindCommand(commandIdentifier, 0, out interpretedCommand);
                return new CommandContext(interpretedCommand, commandSearch, string.Empty, new IndexArray<string>(0));
            }
            
            commandIdentifier = message.Substring(0, firstSpace).Trim();
            argumentSection = message.Substring(firstSpace + 1);

            int argcnt;
            if (string.IsNullOrEmpty(argumentSection))
            {
                arguments = new IndexArray<string>(0);
                argcnt = 0;
            }
            else
            {
                argcnt = 1;
                for (int i = 0; i < argumentSection.Length; i++)
                {
                    bool isUnescapedComma = argumentSection[i] == ',';
                    if (i > 0 && isUnescapedComma)
                    {
                        isUnescapedComma = argumentSection[i - 1] != '\\';
                    }
                    if (isUnescapedComma)
                    {
                        argcnt++;
                    }
                }
                arguments = new IndexArray<string>(argcnt);
                int argindex = 0;
                int lastindex = 0;
                for (int i = 0; i < argumentSection.Length; i++)
                {
                    bool isUnescapedComma = argumentSection[i] == ',';
                    if (i > 0 && isUnescapedComma)
                    {
                        isUnescapedComma = argumentSection[i - 1] != '\\';
                    }
                    if (isUnescapedComma)
                    {
                        if (lastindex < i)
                        {
                            arguments[argindex] = argumentSection.Substring(lastindex, i - lastindex).Trim().Replace("\\,", ",");
                        }
                        else
                        {
                            arguments[argindex] = string.Empty;
                        }
                        argindex++;
                        lastindex = i + 1;
                    }
                }
                if (lastindex <= argumentSection.Length - 1)
                {
                    arguments[argindex] = argumentSection.Substring(lastindex, argumentSection.Length - lastindex).Trim().Replace("\\,", ",");
                }
                else
                {
                    arguments[argindex] = string.Empty;
                }
            }

            commandSearch = CommandCollection.TryFindCommand(commandIdentifier, argcnt, out interpretedCommand);

            return new CommandContext(interpretedCommand, commandSearch, argumentSection, arguments);
        }

        public override ICommandContext ParseCommand(IGuildMessageContext guildContext)
        {
            return ParseCommand(guildContext as IMessageContext);
        }
    }
}
