using BotCoreNET.BotVars;
using BotCoreNET.Helpers;
using JSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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
            GuildBotVarCollection.SubscribeToBotVarUpdateStaticEvent(OnGuildBotVarUpdated, "quotes");
        }

        private static void OnGuildBotVarUpdated(ulong guildId, BotVar botVar)
        {
            if (botVar.IsGeneric)
            {
                if (storedMessages.TryGetValue(guildId, out StoredMessagesService messagesService))
                {
                    messagesService.ApplyJSON(botVar.Generic);
                }
                else
                {
                    if (botVar.TryConvert(out messagesService))
                    {
                        storedMessages.Add(guildId, messagesService);
                    }
                }
            }
        }

        public static StoredMessagesService GetMessagesService(ulong guildId)
        {
            if (!storedMessages.TryGetValue(guildId, out StoredMessagesService messagesService))
            {
                GuildBotVarCollection guildBotVars = BotVarManager.GetGuildBotVarCollection(guildId);
                if (!guildBotVars.TryGetBotVar("storedMessages", out JSONContainer storedMessagesJSON))
                {
                    messagesService = new StoredMessagesService(guildId);
                    messagesService.ApplyJSON(storedMessagesJSON);
                }
            }
            return messagesService;
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
        }

        public bool RemoveQuote(Quote quote)
        {
            return quotes.Remove(quote.QuoteId);
        }

        public bool RemoveQuote(int quoteId)
        {
            return quotes.Remove(quoteId);
        }

        public bool TryGetMacro(string macroId, out Macro macro)
        {
            return macros.TryGetValue(macroId, out macro);
        }

        public void SetMacro(Macro macro)
        {
            macros[macro.Identifier] = macro;
        }

        public bool RemoveMacro(Macro macro)
        {
            return macros.Remove(macro.Identifier);
        }

        public bool RemoveMacro(string macroId)
        {
            if (macros.Remove(macroId))
            {

            }
        }

        #endregion
    }
}
