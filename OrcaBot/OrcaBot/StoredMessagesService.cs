using BotCoreNET;
using BotCoreNET.BotVars;
using BotCoreNET.Helpers;
using Discord;
using Discord.WebSocket;
using JSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrcaBot
{
    class StoredMessagesService
    {
        private const string JSON_QUOTES = "Quotes";
        private const string JSON_QUOTEID = "QuoteId";
        private const string JSON_MACROS = "Macros";
        #region static

        private static readonly Dictionary<ulong, StoredMessagesService> storedMessages = new Dictionary<ulong, StoredMessagesService>();

        public static void OnBotVarSetup()
        {
            BotVarManager.SubscribeToBotVarUpdateEvent(OnBotVarUpdated, "storedmsgsprefix");
        }

        public static StoredMessagesService GetMessagesService(ulong guildId)
        {
            if (!storedMessages.TryGetValue(guildId, out StoredMessagesService messagesService))
            {
                messagesService = new StoredMessagesService(guildId);
                messagesService.AttemptLoad();
            }
            return messagesService;
        }

        

        public static bool IsValidMacroName(string name)
        {
            foreach (char c in name)
            {
                if (!AllowedMacroNameCharacters.Contains(c))
                {
                    return false;
                }
            }
            return true;
        }

        private const string AllowedMacroNameCharacters = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyYzZ-_";

        #endregion
        #region messagehandling

        internal static string storedMessagePrefix = "?";

        private static void OnBotVarUpdated(BotVar botvar)
        {
            if (!string.IsNullOrWhiteSpace(botvar.String))
            {
                storedMessagePrefix = botvar.String;
            }
        }

        internal static Task Client_MessageReceived(SocketMessage arg)
        {
            SocketUserMessage userMessage = arg as SocketUserMessage;

            if (userMessage != null)
            {
                SocketTextChannel guildChannel = userMessage.Channel as SocketTextChannel;

                if (userMessage.Content.StartsWith(storedMessagePrefix) && userMessage.Content.Length > storedMessagePrefix.Length && guildChannel != null)
                {
                    StoredMessagesService messagesService = GetMessagesService(guildChannel.Guild.Id);

                    string msg = userMessage.Content.Substring(storedMessagePrefix.Length);

                    if (messagesService.QuoteCount > 0)
                    {
                        if (int.TryParse(msg, out int quoteId))
                        {
                            if (quoteId > 0)
                            {
                                if (messagesService.TryGetQuote(quoteId, out Quote quote))
                                {
                                    return userMessage.Channel.SendEmbedAsync(quote);
                                }
                            }
                        }
                    }

                    if (messagesService.TryGetMacro(msg, out Macro macro))
                    {
                        if (!macro.Build(out EmbedBuilder embed, out string messageContent, out string error))
                        {
                            return userMessage.Channel.SendEmbedAsync($"Macro corrupted! `{error}`", true);
                        }
                        return userMessage.Channel.SendMessageAsync(text: messageContent, embed: embed.Build());
                    }

                    return userMessage.AddReactionAsync(UnicodeEmoteService.Question);
                }
            }
            return Task.CompletedTask;
        }

        #endregion
        #region instance

        private readonly GuildBotVarCollection guildBotVars;

        public StoredMessagesService(ulong guildId)
        {
            guildBotVars = BotVarManager.GetGuildBotVarCollection(guildId);
        }


        private readonly Dictionary<string, Macro> macros = new Dictionary<string, Macro>();
        private readonly Dictionary<int, Quote> quotes = new Dictionary<int, Quote>();

        public IReadOnlyCollection<Macro> Macros => macros.Values;
        public IReadOnlyCollection<Quote> Quotes => quotes.Values;
        public int QuoteCount => quotes.Count;
        public int MacroCount => macros.Count;

        private int QuoteId;

        private int GetNextQuoteId()
        {
            return QuoteId++;
        }

        public bool ApplyJSON(JSONContainer json)
        {
            macros.Clear();
            quotes.Clear();
            if (json.TryGetField(JSON_QUOTEID, out QuoteId) && json.TryGetArrayField(JSON_MACROS, out JSONContainer macroList) && json.TryGetArrayField(JSON_QUOTES, out JSONContainer quoteList))
            {
                foreach (JSONField macroField in macroList.Array)
                {
                    if (macroField.IsObject)
                    {
                        Macro macro = new Macro();
                        if (macro.ApplyJSON(macroField.Container))
                        {
                            macros[macro.Identifier] = macro;
                        }
                    }
                }

                foreach (JSONField quoteField in quoteList.Array)
                {
                    if (quoteField.IsObject)
                    {
                        Quote quote = new Quote();
                        if (quote.ApplyJSON(quoteField.Container))
                        {
                            quotes[quote.QuoteId] = quote;
                        }
                    }
                }

                return true;
            }
            return false;
        }

        public JSONContainer ToJSON()
        {
            JSONContainer result = JSONContainer.NewObject();

            result.TryAddField(JSON_QUOTEID, QuoteId);

            JSONContainer macroList = JSONContainer.NewArray();
            foreach (Macro macro in macros.Values)
            {
                macroList.Add(macro.ToJSON());
            }

            result.TryAddField(JSON_MACROS, macroList);

            JSONContainer quoteList = JSONContainer.NewArray();
            foreach (Quote quote in quotes.Values)
            {
                quoteList.Add(quote.ToJSON());
            }

            result.TryAddField(JSON_QUOTES, quoteList);

            return result;
        }

        public bool TryGetQuote(int quoteId, out Quote quote)
        {
            return quotes.TryGetValue(quoteId, out quote);
        }

        public Quote GetRandomQuote()
        {
            if (quotes.Count == 0)
            {
                return null;
            }
            List<Quote> quoteList = new List<Quote>(quotes.Values);

            return quoteList[BotCoreNET.Helpers.Macros.Rand.Next(quoteList.Count)];
        }

        public void AddQuote(Quote quote)
        {
            quote.QuoteId = GetNextQuoteId();
            quotes.Add(quote.QuoteId, quote);
            Save();
        }

        public bool RemoveQuote(Quote quote)
        {
            return RemoveQuote(quote.QuoteId);
        }

        public bool RemoveQuote(int quoteId)
        {
            if (quotes.Remove(quoteId))
            {
                Save();
                return true;
            }
            return false;
        }

        public bool HasQuote(int quoteId)
        {
            return quotes.ContainsKey(quoteId);
        }

        public bool HasQuote(ulong messageId)
        {
            return quotes.Values.Any(quote => { return quote.MessageId == messageId; });
        }

        public bool TryGetMacro(string macroId, out Macro macro)
        {
            return macros.TryGetValue(macroId, out macro);
        }

        public void SetMacro(Macro macro)
        {
            macros[macro.Identifier] = macro;
            Save();
        }

        public bool RemoveMacro(Macro macro)
        {
            return RemoveMacro(macro.Identifier);
        }

        public bool RemoveMacro(string macroId)
        {
            if (macros.Remove(macroId))
            {
                Save();
                return true;
            }
            return false;
        }

        private void Save()
        {
            guildBotVars.SetBotVar("storedMessages", ToJSON());
        }

        private void AttemptLoad()
        {
            if (guildBotVars.TryGetBotVar("storedMessages", out JSONContainer storedMessagesJSON))
            {
                ApplyJSON(storedMessagesJSON);
            }
        }

        #endregion
    }
}
