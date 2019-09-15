using BotCoreNET.BotVars;
using BotCoreNET.CommandHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrcaBot
{
    class CustomCommandParser : BuiltInCommandParser
    {
        public override string CommandSyntax(string commandidentifier)
        {
            return $"{Prefix}{commandidentifier}";
        }

        public override string CommandSyntax(string commandidentifier, Argument[] arguments)
        {
            if (arguments.Length == 0)
            {
                return $"{Prefix}{commandidentifier}";
            }
            else
            {
                return $"{Prefix}{commandidentifier}: {string.Join(", ", arguments, 0, arguments.Length)}";
            }
        }

        public override ICommandContext ParseCommand(IMessageContext dmContext)
        {
            string commandIdentifier = null;
            string argumentSection = null;
            IndexArray<string> arguments;
            Command interpretedCommand = null;
            CommandSearchResult commandSearch;

            string message = dmContext.Content.Substring(Prefix.Length);
            string expectedCommandIdentifierSubsection = message.Substring(0, message.Length > 50 ? 50 : message.Length); // Generate a section of max 50 characters to parse the best command identifier match from
            List<string> possibleCommandIdentifiers = new List<string>(new string[] { expectedCommandIdentifierSubsection });

            for (int i = expectedCommandIdentifierSubsection.Length - 1; i >= 0; i--)
            {
                if (expectedCommandIdentifierSubsection[i] == ' ')
                {
                    possibleCommandIdentifiers.Add(expectedCommandIdentifierSubsection.Substring(0, i));
                }
            }

            if (possibleCommandIdentifiers.Count == 0)
            {
                return new CommandContext(null, CommandSearchResult.NoMatch, message, new IndexArray<string>(0));
            }

            bool foundCommand = false;
            foreach (string possibleCommandIdentifier in possibleCommandIdentifiers)
            {
                if (CommandCollection.TryFindCommand(possibleCommandIdentifier, out interpretedCommand))
                {
                    foundCommand = true;
                    commandIdentifier = possibleCommandIdentifier;
                    if (commandIdentifier.Length < message.Length)
                    {
                        argumentSection = message.Substring(commandIdentifier.Length);
                    }
                    else
                    {
                        argumentSection = string.Empty;
                    }
                    break;
                }
            }

            if (!foundCommand)
            {
                return new CommandContext(null, CommandSearchResult.NoMatch, message, new IndexArray<string>(0));
            }

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

            commandSearch = CommandCollection.TryFindCommand(commandIdentifier, argcnt, out _);

            return new CommandContext(interpretedCommand, commandSearch, argumentSection, arguments);
        }

        public override ICommandContext ParseCommand(IGuildMessageContext guildContext)
        {
            return ParseCommand(guildContext as IMessageContext);
        }
    }
}
