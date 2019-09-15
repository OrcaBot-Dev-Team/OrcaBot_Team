using BotCoreNET;
using BotCoreNET.CommandHandling;
using BotCoreNET.Helpers;
using Discord;
using JSON;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OrcaBot.Commands
{
    class DistanceCommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.DMOnly;
        public override HandledContexts ExecutionMethod => HandledContexts.DMOnly;
        public override string Summary => "Retrieves the distance between two systems";
        public override Argument[] Arguments => new Argument[]
        {
            new Argument("System 1", "The starting point of the distance measurement"),
            new Argument("System 2", "The end point of the distance measurement"),
        };
        public override bool RunInAsyncMode => true;

        public DistanceCommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        private string SystemA_name;
        private string SystemB_name;
        private bool printJson;

        protected override Task<ArgumentParseResult> ParseArguments(IDMCommandContext context)
        {
            SystemA_name = context.Arguments.First;

            context.Arguments.Index++;

            SystemB_name = context.Arguments.First;

            if (context.Arguments.TotalCount == 2)
            {
                printJson = false;
            }
            else
            {
                context.Arguments.Index++;

                printJson = context.Arguments.First.ToLower().Contains("json");
            }
            return Task.FromResult(ArgumentParseResult.SuccessfullParse);
        }

        protected override async Task Execute(IDMCommandContext context)
        {
            string webrequest = WebRequestService.EDSM_MultipleSystemsInfo_URL(new string[] { SystemA_name, SystemB_name }, showId: true, showCoords: true);
            RequestJSONResult requestResult = await WebRequestService.GetWebJSONAsync(webrequest);
            if (requestResult.IsSuccess)
            {
                if (requestResult.JSON.IsArray && requestResult.JSON.Array.Count < 1)
                {
                    await context.Channel.SendEmbedAsync("System not found in database!", true);
                }
                else
                {
                    if (printJson)
                    {
                        await context.Channel.SendEmbedAsync("General System Info", string.Format("```json\n{0}```", requestResult.JSON.Build(true).MaxLength(2037)));
                    }
                    EmbedBuilder distanceEmbed = GetDistanceEmbed(requestResult.JSON);
                    await context.Channel.SendEmbedAsync(distanceEmbed);
                }
            }
            else if (requestResult.IsException)
            {
                await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. Exception Message: `{0}`", requestResult.ThrownException.Message), true);
            }
            else
            {
                await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. HTTP Error: `{0} {1}`", (int)requestResult.Status, requestResult.Status.ToString()), true);
            }

        }

        private EmbedBuilder GetDistanceEmbed(JSONContainer json)
        {
            EmbedFooterBuilder footer = new EmbedFooterBuilder()
            {
                Text = "EDSM"
            };
            if (json.IsArray)
            {
                if (json.Array.Count == 2)
                {
                    JSONContainer systemAJSON = json.Array[0].Container;
                    JSONContainer systemBJSON = json.Array[1].Container;
                    if (systemAJSON != null && systemBJSON != null)
                    {
                        if (GetSystemInfo(systemAJSON, out SystemA_name, SystemA_name, out uint systemA_id, out string systemA_url, out Vector3 systemA_pos) &&
                            GetSystemInfo(systemBJSON, out SystemB_name, SystemB_name, out uint systemB_id, out string systemB_url, out Vector3 systemB_pos))
                        {
                            float distance = Vector3.Distance(systemA_pos, systemB_pos);
                            return DistanceEmbed(footer,
                                $"{(systemA_url == null ? SystemA_name : $"[{SystemA_name}]({systemA_url})")} **`<-`   `{distance.ToString("### ###.00", CultureInfo.InvariantCulture)} ly`   ` ->`** " +
                                $"{(systemB_url == null ? SystemB_name : $"[{SystemB_name}]({systemB_url})")}", false);

                        }
                        return DistanceEmbed(footer, "Error " + Macros.GetCodeLocation());
                    }
                    return DistanceEmbed(footer, "Error " + Macros.GetCodeLocation());
                }
                else if (json.Array.Count == 1)
                {
                    JSONContainer systemJSON = json.Array[0].Container;
                    if (systemJSON != null)
                    {
                        if (systemJSON.TryGetField("name", out string name))
                        {
                            return DistanceEmbed(footer, $"Found only one system: {name}");
                        }
                        return DistanceEmbed(footer, "Error " + Macros.GetCodeLocation());

                    }
                    return DistanceEmbed(footer, "Error " + Macros.GetCodeLocation());

                }
                return DistanceEmbed(footer, "Error " + Macros.GetCodeLocation());
            }
            return DistanceEmbed(footer, "Error " + Macros.GetCodeLocation());
        }

        private static bool GetSystemInfo(JSONContainer systemJSON, out string name, string fallback_name, out uint id, out string url, out Vector3 position)
        {
            bool idfound = systemJSON.TryGetField("id", out id);
            if (systemJSON.TryGetField("name", out name, fallback_name) && idfound)
            {
                url = $"https://www.edsm.net/en/system/id/{id}/name/{name.Replace(' ', '+')}";
            }
            else
            {
                url = null;
            }

            if (systemJSON.TryGetObjectField("coords", out JSONContainer coordsJSON))
            {
                if (coordsJSON.TryGetField("x", out float x) && coordsJSON.TryGetField("y", out float y) && coordsJSON.TryGetField("z", out float z))
                {
                    position = new Vector3(x, y, z);
                    return true;
                }
            }
            position = default;
            return false;
        }

        private EmbedBuilder DistanceEmbed(EmbedFooterBuilder footer, string descr, bool errorcolor = true)
        {
            return new EmbedBuilder()
            {
                Title = $"Distance between \"{SystemA_name}\" and \"{SystemB_name}\"",
                Color = errorcolor ? BotCore.ErrorColor : BotCore.EmbedColor,
                Description = descr,
                Footer = footer
            };
        }
    }
}