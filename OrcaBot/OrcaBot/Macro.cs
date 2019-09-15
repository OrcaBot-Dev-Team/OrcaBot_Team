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
