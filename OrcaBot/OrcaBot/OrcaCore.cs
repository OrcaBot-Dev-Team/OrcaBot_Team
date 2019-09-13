using System;
using BotCoreNET;
using BotCoreNET.CommandHandling;
using Discord;
using OrcaBot.Commands;

namespace OrcaBot
{
    static class OrcaCore
    {
        static void Main(string[] args)
        {
            BotCore.OnBotVarDefaultSetup += WebRequestService.OnBotVarSetup;
            BotCore.OnBotVarDefaultSetup += Macro.OnBotVarSetup;
            CommandCollection elitecollection = new CommandCollection("Elite", "Commands to retrieve info on Elite:Dangerous");
            new SystemCommand("system", elitecollection);
            new CmdrCommand("cmdr", elitecollection);
            new DistanceCommand("distance", elitecollection);
            //new BaseCommand("base", elitecollection);
            CommandCollection quotecollection = new CommandCollection("Quotes", "Commands for adding and viewing quotes");
            new AddQuoteCommand("quote add", quotecollection);
            new GetQuotecommand("quote", quotecollection);
            CommandCollection macrocollection = new CommandCollection("Macros", "Commands for adding and listing macros");
            new SetMacroCommand("macro", macrocollection);
            new ListMacrosCommand("macrolist", macrocollection);

            BotCore.Client.MessageReceived += Macro.Client_MessageReceived;

            EmbedBuilder aboutEmbed = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = "OrcaBot V1.0",
                    IconUrl = "https://cdn.discordapp.com/attachments/475335264068173824/620998619574829056/thepod2.jpg",
                },
                Color = new Color(80,127,160),
                Description = $"**Programming**\n" +
                $"\u23F5 BrainProtest#1394 (<@117260771200598019>)" +
                $"\n" +
                $"\n" +
                $"**External Tools and Dependencies**\n" +
                $"\u23F5 [Discord.NET](https://github.com/discord-net/Discord.Net) Discord API Wrapper\n" +
                $"\u23F5 [EDSM API](https://www.edsm.net/en/api-v1) for Elite:Dangerous data network access\n" +
                $"\u23F5 [Inara API](https://inara.cz/inara-api/) for Elite:Dangerous data network access" +
                $"\u23F5 [EDAssets](https://edassets.org/#/) for Station Icons"
            };

            BotCore.Run(Environment.CurrentDirectory + "/BotCore/", aboutEmbed:aboutEmbed);
        }
    }
}
