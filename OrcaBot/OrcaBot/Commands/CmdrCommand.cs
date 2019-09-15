using BotCoreNET;
using BotCoreNET.CommandHandling;
using BotCoreNET.Helpers;
using Discord;
using JSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrcaBot.Commands
{
    class CmdrCommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.DMOnly;
        public override HandledContexts ExecutionMethod => HandledContexts.DMOnly;
        public override string Summary => "Lists information about a commander";
        public override string Remarks => "This command uses [Inara](https://inara.cz/inara-api/) and [EDSM](https://www.edsm.net/en/api-v1) api";
        public override Argument[] Arguments => new Argument[]
        {
            new Argument("Commander Name", "A commander name to search by"),
        };
        public override bool RunInAsyncMode => true;

        public CmdrCommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        private string cmdrName;
        private bool printJson;

        protected override Task<ArgumentParseResult> ParseArguments(IDMCommandContext context)
        {

            cmdrName = context.Arguments.First;

            if (context.Arguments.TotalCount == 1)
            {
                printJson = false;
                return Task.FromResult(ArgumentParseResult.SuccessfullParse);
            }

            context.Arguments.Index++;

            printJson = context.Arguments.First.Contains("json");

            return Task.FromResult(ArgumentParseResult.SuccessfullParse);
        }

        private const string INARAREQUESTFAILURE = "Inara Request Failed";
        private const string EDSMREQUESTFAILURE = "EDSM Request Failed";

        protected override async Task Execute(IDMCommandContext context)
        {
            EmbedBuilder inaraEmbed;
            if (!WebRequestService.CanMakeInaraRequests)
            {
                inaraEmbed = new EmbedBuilder()
                {
                    Color = BotCore.ErrorColor,
                    Title = INARAREQUESTFAILURE,
                    Description = "Can not make inara requests, as appname and/or apikey config variables are not set!"
                };
            }
            else
            {
                JSONContainer inaraRequestcontent = WebRequestService.Inara_CMDR_Profile(cmdrName);
                if (printJson)
                {
                    await context.Channel.SendEmbedAsync(new EmbedBuilder()
                    {
                        Title = "Request JSON sending to Inara",
                        Color = BotCore.EmbedColor,
                        Description = $"```json\n{inaraRequestcontent.Build(true).MaxLength(EmbedHelper.EMBEDDESCRIPTION_MAX - 11)}```"
                    });
                }
                RequestJSONResult requestResultInara = await WebRequestService.GetWebJSONAsync("https://inara.cz/inapi/v1/", inaraRequestcontent);
                if (requestResultInara.IsSuccess && string.IsNullOrEmpty(requestResultInara.jsonParseError))
                {
                    inaraEmbed = GetInaraCMDREmbed(requestResultInara.JSON, cmdrName);
                    if (printJson)
                    {
                        await context.Channel.SendEmbedAsync(new EmbedBuilder()
                        {
                            Title = "Result JSON from Inara",
                            Color = BotCore.EmbedColor,
                            Description = $"```json\n{requestResultInara.JSON.Build(true).MaxLength(EmbedHelper.EMBEDDESCRIPTION_MAX - 11)}```"
                        });
                    }
                }
                else if (!string.IsNullOrEmpty(requestResultInara.jsonParseError))
                {
                    inaraEmbed = new EmbedBuilder()
                    {
                        Color = BotCore.ErrorColor,
                        Title = INARAREQUESTFAILURE,
                        Description = $"JSON parse error: `{requestResultInara.jsonParseError}`!"
                    };
                }
                else if (requestResultInara.IsException)
                {
                    inaraEmbed = new EmbedBuilder()
                    {
                        Color = BotCore.ErrorColor,
                        Title = INARAREQUESTFAILURE,
                        Description = $"Could not connect to Inaras services. Exception Message: `{requestResultInara.ThrownException.Message}`"
                    };
                }
                else
                {
                    inaraEmbed = new EmbedBuilder()
                    {
                        Color = BotCore.ErrorColor,
                        Title = INARAREQUESTFAILURE,
                        Description = $"Could not connect to Inaras services. HTTP Error Message: `{(int)requestResultInara.Status} {requestResultInara.Status}`"
                    };
                }
            }
            EmbedBuilder edsmEmbed;
            RequestJSONResult requestResultEDSM = await WebRequestService.GetWebJSONAsync(WebRequestService.EDSM_Commander_Location(cmdrName, true, false));
            if (requestResultEDSM.IsSuccess && string.IsNullOrEmpty(requestResultEDSM.jsonParseError))
            {
                edsmEmbed = GetEDSMCMDREmbed(requestResultEDSM.JSON, cmdrName);
                if (printJson)
                {
                    await context.Channel.SendEmbedAsync(new EmbedBuilder()
                    {
                        Title = "Result JSON from EDSM",
                        Color = BotCore.EmbedColor,
                        Description = $"```json\n{requestResultEDSM.JSON.Build(true).MaxLength(EmbedHelper.EMBEDDESCRIPTION_MAX - 11)}```"
                    });
                }

            }
            else if (!string.IsNullOrEmpty(requestResultEDSM.jsonParseError))
            {
                edsmEmbed = new EmbedBuilder()
                {
                    Color = BotCore.ErrorColor,
                    Title = EDSMREQUESTFAILURE,
                    Description = $"JSON parse error: `{requestResultEDSM.jsonParseError}`!"
                };
            }
            else if (requestResultEDSM.IsException)
            {
                edsmEmbed = new EmbedBuilder()
                {
                    Color = BotCore.ErrorColor,
                    Title = EDSMREQUESTFAILURE,
                    Description = $"Could not connect to Inaras services. Exception Message: `{requestResultEDSM.ThrownException.Message}`"
                };
            }
            else
            {
                edsmEmbed = new EmbedBuilder()
                {
                    Color = BotCore.ErrorColor,
                    Title = EDSMREQUESTFAILURE,
                    Description = $"Could not connect to Inaras services. HTTP Error Message: `{(int)requestResultEDSM.Status} {requestResultEDSM.Status}`"
                };
            }
            inaraEmbed.Footer = new EmbedFooterBuilder()
            {
                Text = "Inara"
            };
            edsmEmbed.Footer = new EmbedFooterBuilder()
            {
                Text = "EDSM"
            };
            await context.Channel.SendEmbedAsync(inaraEmbed);
            await context.Channel.SendEmbedAsync(edsmEmbed);
        }

        private EmbedBuilder GetEDSMCMDREmbed(JSONContainer json, string backupName)
        {
            EmbedBuilder result;

            if (json.TryGetField("msg", out string msg))
            {
                if (msg == "OK")
                {
                    string system_link = null;
                    bool system_id_found = json.TryGetField("systemId", out uint system_id, 0);
                    if (json.TryGetField("system", out string system_name, null) && system_id_found)
                    {
                        system_link = $"https://www.edsm.net/en/system/id/{system_id}/name/{system_name.Replace(' ', '+')}";
                    }
                    string user_name = backupName;
                    if (json.TryGetField("url", out string profile_url, "https://www.edsm.net"))
                    {
                        string[] urlsections = profile_url.Split('/');
                        if (urlsections.Length > 1)
                        {
                            user_name = urlsections[urlsections.Length - 1];
                        }
                    }
                    json.TryGetField("shipType", out string shipType, null);
                    string station_name = null;
                    string station_link = null;
                    if (json.TryGetField("isDocked", out bool isDocked, false))
                    {
                        json.TryGetField("station", out station_name, "Unknown Station");
                        if (json.TryGetField("stationId", out uint station_id) && system_link != null)
                        {
                            station_link = $"https://www.edsm.net/en/system/stations/id/{system_id}/name/{system_name.Replace(' ', '+')}/details/idS/{station_id}/nameS/{station_name.Replace(' ', '+')}";
                        }
                    }

                    result = new EmbedBuilder()
                    {
                        Author = new EmbedAuthorBuilder()
                        {
                            Url = profile_url,
                            Name = $"{user_name}'s EDSM Profile"
                        },
                        Color = BotCore.EmbedColor
                    };
                    if (system_name != null)
                    {
                        if (system_link != null)
                        {
                            result.AddField("System", $"[{system_name}]({system_link})");
                        }
                        else
                        {
                            result.AddField("System", system_name);
                        }
                    }
                    if (shipType != null)
                    {
                        result.AddField("Ship", shipType);
                    }
                    if (isDocked)
                    {
                        if (station_link != null)
                        {
                            result.AddField("Docked At", $"[{station_name}]({station_link})");
                        }
                        else
                        {
                            result.AddField("Docked At", station_name);
                        }
                    }
                }
                else
                {
                    result = new EmbedBuilder()
                    {
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = backupName
                        },
                        Color = BotCore.ErrorColor,
                        Description = msg
                    };
                }
            }
            else
            {
                result = new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder()
                    {
                        Name = backupName
                    },
                    Color = BotCore.ErrorColor,
                    Description = $"Internal Error - {Macros.GetCodeLocation()}"
                };
            }
            return result;
        }

        private EmbedBuilder GetInaraCMDREmbed(JSONContainer json, string backupName)
        {
            EmbedBuilder result;
            if (json.TryGetArrayField("events", out JSONContainer arrayJSON))
            {
                if (arrayJSON.Array.Count >= 1 && arrayJSON.Array[0].IsObject)
                {
                    JSONContainer eventJSON = arrayJSON.Array[0].Container;
                    if (eventJSON.TryGetField("eventStatus", out uint eventStatusId))
                    {
                        eventJSON.TryGetField("eventStatusText", out string eventStatusText);
                        if (eventStatusId == 204)
                        {
                            result = new EmbedBuilder()
                            {
                                Author = new EmbedAuthorBuilder()
                                {
                                    Name = backupName
                                },
                                Color = BotCore.ErrorColor,
                                Description = eventStatusText
                            };
                        }
                        else if (eventJSON.TryGetObjectField("eventData", out JSONContainer cmdrJSON))
                        {
                            if (cmdrJSON.TryGetField("userName", out string user_name))
                            {
                                if (eventStatusId == 202 && !user_name.Equals(backupName, StringComparison.OrdinalIgnoreCase) && cmdrJSON.TryGetArrayField("otherNamesFound", out JSONContainer otherNamesArray))
                                {
                                    result = new EmbedBuilder()
                                    {
                                        Title = "Multiple Results found!",
                                        Description = $"{user_name}\n{otherNamesArray.Array.OperationJoinReadonly("\n", field => { return field.String; })}"
                                    };
                                }
                                else
                                {
                                    cmdrJSON.TryGetField("inaraURL", out string cmdr_url);
                                    result = new EmbedBuilder()
                                    {
                                        Author = new EmbedAuthorBuilder()
                                        {
                                            Name = $"{user_name}'s Inara Profile",
                                            Url = cmdr_url
                                        },
                                        Color = BotCore.EmbedColor
                                    };
                                    if (cmdrJSON.TryGetField("preferredGameRole", out string cmdr_gamerole))
                                    {
                                        result.AddField("Game Role", cmdr_gamerole, true);
                                    }
                                    if (cmdrJSON.TryGetField("preferredAllegianceName", out string cmdr_allegiance))
                                    {
                                        result.AddField("Allegiance", cmdr_allegiance, true);
                                    }
                                    if (cmdrJSON.TryGetObjectField("commanderSquadron", out JSONContainer squadronJSON))
                                    {
                                        squadronJSON.TryGetField("squadronName", out string squadron_name);
                                        squadronJSON.TryGetField("squadronMemberRank", out string squadron_rank);
                                        squadronJSON.TryGetField("squadronMembersCount", out uint squadron_membercount);
                                        if (squadronJSON.TryGetField("inaraURL", out string squadron_link))
                                        {
                                            result.AddField("Squadron", $"[{squadron_name}]({squadron_link}) ({squadron_membercount} Members): Rank `{squadron_rank}`", true);
                                        }
                                        else
                                        {
                                            result.AddField("Squadron", $"{squadron_name} ({squadron_membercount} Members): Rank `{squadron_rank}`", true);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = InaraErrorEmbed(backupName);
                            }
                        }
                        else
                        {
                            result = InaraErrorEmbed(backupName);
                        }
                    }
                    else
                    {
                        result = InaraErrorEmbed(backupName);
                    }
                }
                else
                {
                    result = InaraErrorEmbed(backupName);
                }
            }
            else
            {
                result = InaraErrorEmbed(backupName);
            }

            return result;
        }

        private static EmbedBuilder InaraErrorEmbed(string backupName)
        {
            return new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = backupName
                },
                Color = BotCore.ErrorColor,
                Description = "Internal Error"
            };
        }
    }
}
