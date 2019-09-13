using BotCoreNET;
using BotCoreNET.BotVars;
using BotCoreNET.CommandHandling;
using BotCoreNET.Helpers;
using Discord;
using Discord.WebSocket;
using JSON;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OrcaBot
{
    class Macro : IGenericBotVar
    {
        #region static

        internal static void OnBotVarSetup()
        {
            BotVarManager.SubscribeToBotVarUpdateEvent(OnPrefixBotVarUpdate, "macroprefix");
        }

        private static void OnPrefixBotVarUpdate(BotVar var)
        {
            if (var.IsString)
            {
                Prefix = var.String;
            }
        }

        internal static string Prefix { get; private set; } = "?";

        internal static async Task Client_MessageReceived(SocketMessage arg)
        {
            SocketUserMessage userMessage = arg as SocketUserMessage;

            if (userMessage != null)
            {
                SocketTextChannel guildChannel = userMessage.Channel as SocketTextChannel;

                if (userMessage.Content.StartsWith(Prefix) && userMessage.Content.Length > Prefix.Length && guildChannel != null)
                {
                    IGuildMessageContext context = new GuildMessageContext(userMessage, guildChannel.Guild);

                    if (context.IsDefined)
                    {
                        GuildBotVarCollection guildBotVars = BotVarManager.GetGuildBotVarCollection(context.Guild.Id);

                        string macroId = context.Content.Substring(Prefix.Length);
                        string macroKey = $"macro.{macroId}";

                        if (guildBotVars.TryGetBotVar(macroKey, out Macro macro))
                        {
                            if (macro.Build(out EmbedBuilder embed, out string messagecontent, out string error))
                            {
                                await context.Channel.SendMessageAsync(messagecontent, embed: embed.Build());
                            }
                            else
                            {
                                await context.Channel.SendEmbedAsync($"Macro `{macroId}` is corrupted!", true);
                            }
                        }
                        else
                        {
                            await context.Message.AddReactionAsync(UnicodeEmoteService.Question);
                        }
                    }
                }
            }
        }

        #endregion

        public string Identifier;
        public JSONContainer JSON;
        public Macro()
        {

        }

        public Macro(string identifier, JSONContainer json)
        {
            Identifier = identifier;
            JSON = json;
        }

        private const string JSON_ID = "Id";

        public bool ApplyJSON(JSONContainer json)
        {
            JSON = json;
            return json.TryGetField(JSON_ID, out Identifier) && (json.HasField(EmbedHelper.MESSAGECONTENT) || json.HasField(EmbedHelper.EMBED));
        }

        public JSONContainer ToJSON()
        {
            JSON.TryAddField(JSON_ID, Identifier);
            return JSON;
        }

        public bool Build(out EmbedBuilder embed, out string messageContent, out string error)
        {
            ArgumentParseResult parseResult = EmbedHelper.TryParseEmbedFromJSONObject(JSON, out embed, out messageContent);
            error = parseResult.Message;
            return parseResult.Success;
        }
    }
}
