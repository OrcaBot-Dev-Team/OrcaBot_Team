using BotCoreNET;
using BotCoreNET.CommandHandling;
using Discord;
using JSON;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace OrcaBot.Commands
{
    class SystemCommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.DMOnly;

        public override HandledContexts ExecutionMethod => HandledContexts.DMOnly;

        public override string Summary => "Prints out detailed information about a system";
        public override string Remarks => "This command uses [EDSM](https://www.edsm.net/en/api-v1) api";
        public override Argument[] Arguments => new Argument[]
        {
            new Argument("System Name", "The system you want to pull info about")
        };
        public override bool RunInAsyncMode => true;

        public SystemCommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        string systemName;
        bool webRequests;
        bool printJson;
        bool listAllStations;

        protected override Task<ArgumentParseResult> ParseArguments(IDMCommandContext context)
        {
            systemName = context.Arguments.First;

            if (context.Arguments.TotalCount == 1)
            {
                webRequests = false;
                printJson = false;
                listAllStations = false;
                return Task.FromResult(ArgumentParseResult.SuccessfullParse);
            }

            context.Arguments.Index++;

            webRequests = context.Arguments.First.Contains("webrequests");
            printJson = context.Arguments.First.Contains("json");
            listAllStations = context.Arguments.First.Contains("list");

            return Task.FromResult(ArgumentParseResult.SuccessfullParse);
        }

        protected override async Task Execute(IDMCommandContext context)
        {
            string requestSystem = WebRequestService.EDSM_SystemInfo_URL(systemName, true, true, true, true, true);
            string requestStations = WebRequestService.EDSM_SystemStations_URL(systemName);
            string requestTraffic = WebRequestService.EDSM_SystemTraffic_URL(systemName);
            string requestDeaths = WebRequestService.EDSM_SystemDeaths_URL(systemName);
            if (webRequests)
            {
                EmbedBuilder embed = new EmbedBuilder()
                {
                    Title = "Webrequests",
                    Color = BotCore.EmbedColor,
                    Description = $"[System]({requestSystem}) `{requestSystem}`\n[Stations]({requestStations}) `{requestStations}`\n[Traffic]({requestTraffic}) `{requestTraffic}`\n[Deaths]({requestDeaths}) `{requestDeaths}`"
                };
                await context.Channel.SendEmbedAsync(embed);
            }
            RequestJSONResult requestResultSystem = await WebRequestService.GetWebJSONAsync(requestSystem);
            RequestJSONResult requestResultStations = await WebRequestService.GetWebJSONAsync(requestStations);
            RequestJSONResult requestResultTraffic = await WebRequestService.GetWebJSONAsync(requestTraffic);
            RequestJSONResult requestResultDeaths = await WebRequestService.GetWebJSONAsync(requestDeaths);
            if (requestResultSystem.IsSuccess)
            {
                if (requestResultSystem.JSON.IsArray && requestResultSystem.JSON.Array.Count < 1)
                {
                    await context.Channel.SendEmbedAsync("System not found in database!", true);
                }
                else
                {
                    if (printJson)
                    {
                        await context.Channel.SendEmbedAsync("General System Info", string.Format("```json\n{0}```", requestResultSystem.JSON.Build(true).MaxLength(2037)));
                        await context.Channel.SendEmbedAsync("Station Info", string.Format("```json\n{0}```", requestResultStations.JSON.Build(true).MaxLength(2037)));
                    }
                    EmbedBuilder systemEmbed = GetSystemInfoEmbed(requestResultSystem.JSON, requestResultStations.JSON, requestResultTraffic.JSON, requestResultDeaths.JSON, out List<EmbedFieldBuilder> allStations);
                    await context.Channel.SendEmbedAsync(systemEmbed);
                }
            }
            else if (requestResultSystem.IsException)
            {
                await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. Exception Message: `{0}`", requestResultSystem.ThrownException.Message), true);
            }
            else
            {
                await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. HTTP Error: `{0} {1}`", (int)requestResultSystem.Status, requestResultSystem.Status.ToString()), true);
            }
        }

        private EmbedBuilder GetSystemInfoEmbed(JSONContainer systemJSON, JSONContainer stationsJSON, JSONContainer trafficJSON, JSONContainer deathsJSON, out List<EmbedFieldBuilder> allStations)
        {
            EmbedBuilder systemembed = new EmbedBuilder();
            allStations = new List<EmbedFieldBuilder>();
            bool stationsFound = false;

            if (systemJSON.TryGetField("name", out string system_name, string.Empty) && systemJSON.TryGetField("id", out ulong system_id, 0))
            {
                // Gathering Information

                // Gathering General System information
                string system_name_link = system_name.Replace(' ', '+');
                string security = "Unknown";
                if (systemJSON.TryGetObjectField("information", out JSONContainer system_information))
                {
                    system_information.TryGetField("security", out security, "Anarchy (Unpopulated)");
                }
                string startype = "Not found";
                if (systemJSON.TryGetObjectField("primaryStar", out JSONContainer primaryStar))
                {
                    primaryStar.TryGetField("type", out startype, startype);
                    if (systemJSON.TryGetField("isScoopable", out bool scoopable))
                    {
                        if (!scoopable)
                        {
                            startype += " **(Unscoopable)**";
                        }
                    }
                }
                systemJSON.TryGetField("requirePermit", out bool system_requirepermit, false);
                systemJSON.TryGetField("permitName", out string system_permit, "Unknown Permit");

                // Gathering information about stations
                StationInfo bestOrbitalLarge = null;
                StationInfo bestOrbitalMedium = null;
                StationInfo bestPlanetary = null;
                List<StationInfo> stationInfos = new List<StationInfo>();
                if ((stationsJSON != null) && stationsJSON.TryGetArrayField("stations", out stationsJSON))
                {
                    stationsFound = true;
                    foreach (JSONField station in stationsJSON.Array)
                    {
                        StationInfo info = new StationInfo();
                        if (info.FromJSON(station.Container))
                        {
                            stationInfos.Add(info);
                            if (info.HasLargePadOrbital)
                            {
                                if (bestOrbitalLarge == null)
                                {
                                    bestOrbitalLarge = info;
                                }
                                else
                                    if (info.Distance < bestOrbitalLarge.Distance)
                                {
                                    bestOrbitalLarge = info;
                                }
                            }
                            if (info.HasMedPadOrbital)
                            {
                                if (bestOrbitalMedium == null)
                                {
                                    bestOrbitalMedium = info;
                                }
                                else
                                    if (info.Distance < bestOrbitalMedium.Distance)
                                {
                                    bestOrbitalMedium = info;
                                }
                            }
                            if (info.IsPlanetary)
                            {
                                if (bestPlanetary == null)
                                {
                                    bestPlanetary = info;
                                }
                                else if (info.Distance < bestPlanetary.Distance)
                                {
                                    bestPlanetary = info;
                                }
                            }
                        }
                    }
                }

                // Getting Information about traffic
                int traffic_week = -1;
                int traffic_day = -1;

                if ((trafficJSON != null) && trafficJSON.TryGetObjectField("traffic", out trafficJSON))
                {
                    trafficJSON.TryGetField("week", out traffic_week, -1);
                    trafficJSON.TryGetField("day", out traffic_day, -1);
                }

                // Getting Information about CMDR deaths
                int deaths_week = -1;
                int deaths_day = -1;

                if ((deathsJSON != null) && deathsJSON.TryGetObjectField("deaths", out deathsJSON))
                {
                    deathsJSON.TryGetField("week", out deaths_week, -1);
                    deathsJSON.TryGetField("day", out deaths_day, -1);
                }

                // Constructing message
                systemembed.Color = BotCore.EmbedColor;
                systemembed.Title = $"__**System Info for {system_name}**__";
                systemembed.Url = $"https://www.edsm.net/en/system/id/{system_id}/name/{system_name_link}";
                systemembed.AddField("General Info", $"{(system_requirepermit ? string.Format("**Requires Permit**: {0}\n", system_permit) : string.Empty)}Star Type: {startype}\nSecurity: {security}");

                bool provideInfoOnLarge = bestOrbitalLarge != null;
                bool provideInfoOnMedium = (!provideInfoOnLarge && bestOrbitalMedium != null) || (provideInfoOnLarge && bestOrbitalMedium != null && bestOrbitalLarge.Distance > bestOrbitalMedium.Distance);
                bool provideInfoOnPlanetary = (!provideInfoOnLarge && bestPlanetary != null) || (provideInfoOnLarge && bestPlanetary != null && bestOrbitalLarge.Distance > bestPlanetary.Distance);
                if (provideInfoOnLarge)
                {
                    systemembed.AddField("Closest Orbital Large Pad", bestOrbitalLarge.ToString());
                }
                if (provideInfoOnMedium)
                {
                    systemembed.AddField("Closest Orbital Medium Pad", bestOrbitalMedium.ToString());
                }
                if (provideInfoOnPlanetary)
                {
                    systemembed.AddField("Closest Planetary Large Pad", bestPlanetary.ToString());
                }
                if (!provideInfoOnLarge && !provideInfoOnMedium && !provideInfoOnPlanetary)
                {
                    systemembed.AddField("No Stations in this System!", "- / -");
                }
                if (traffic_day != -1 && traffic_week != -1)
                {
                    systemembed.AddField("Traffic Report", string.Format("Last 7 days: {0} CMDRs, last 24 hours: {1} CMDRs", traffic_week, traffic_day));
                }
                else
                {
                    systemembed.AddField("No Traffic Report Available", "- / -");
                }
                if (deaths_day != -1 && deaths_week != -1)
                {
                    systemembed.AddField("CMDR Deaths Report", string.Format("Last 7 days: {0} CMDRs, last 24 hours: {1} CMDRs", deaths_week, deaths_day));
                }
                else
                {
                    systemembed.AddField("No CMDR Deaths Report Available", "- / -");
                }

                if (stationsFound)
                {
                    foreach (StationInfo stationInfo in stationInfos)
                    {
                        allStations.Add(Macros.EmbedField(stationInfo.Title_NoLink, stationInfo.Services_Link));
                    }
                }
            }
            else
            {
                systemembed.Description = $"Internal Error";
                systemembed.Color = BotCore.ErrorColor;
            }
            return systemembed;
        }

    }

    class StationInfo
    {
        private static readonly CultureInfo culture = new CultureInfo("en-us");

        public string Name;
        public long Id;
        public StationType Type;
        public float Distance;
        public bool HasRestock;
        public bool HasRefuel;
        public bool HasRepair;
        public bool HasShipyard;
        public bool HasOutfitting;
        public bool HasUniversalCartographics;
        private readonly string SystemName;
        private readonly long SystemId;

        public string ControllingFaction;

        public bool HasLargePadOrbital
        {
            get
            {
                return Type == StationType.Asteroid || Type == StationType.Coriolis || Type == StationType.Ocellus || Type == StationType.Orbis || Type == StationType.Megaship;
            }
        }
        public bool IsPlanetary
        {
            get
            {
                return Type == StationType.Planetary;
            }
        }
        public bool HasMedPadOrbital
        {
            get
            {
                return Type == StationType.Outpost || HasLargePadOrbital;
            }
        }

        public string EDSMLink
        {
            get
            {
                return string.Format("https://www.edsm.net/en/system/stations/id/{0}/name/{1}/details/idS/{2}/nameS/{3}", SystemId, SystemName, Id, Name.Replace(' ', '+'));
            }
        }

        public StationInfo(string name, long id, string systemName, long systemId, string type, float distance)
        {
            Name = name;
            Id = id;
            SystemName = systemName;
            SystemId = systemId;
            Type = ParseStationType(type);
            Distance = distance;
        }

        public StationInfo() { }

        public bool FromJSON(JSONContainer station)
        {
            if (station.TryGetField("name", out Name) && station.TryGetField("id", out Id))
            {
                if (station.TryGetField("type", out string stationtype))
                {
                    Type = ParseStationType(stationtype);
                }
                station.TryGetField("distanceToArrival", out Distance);
                if (station.TryGetField("government", out string station_gov))
                {
                    if (station_gov.Equals("Workshop (Engineer)"))
                    {
                        Type = StationType.EngineerBase;
                    }
                }
                station.TryGetField("haveShipyard", out HasShipyard);
                station.TryGetField("haveOutfitting", out HasOutfitting);
                if (station.TryGetArrayField("otherServices", out JSONContainer otherservices))
                {
                    foreach (JSONField service in otherservices.Array)
                    {
                        switch (service.String)
                        {
                            case "Restock":
                                HasRestock = true;
                                break;
                            case "Repair":
                                HasRepair = true;
                                break;
                            case "Refuel":
                                HasRefuel = true;
                                break;
                            case "Universal Cartographics":
                                HasUniversalCartographics = true;
                                break;
                        }
                    }
                }
                if (station.TryGetObjectField("controllingFaction", out JSONContainer faction))
                {
                    faction.TryGetField("name", out ControllingFaction);
                }
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return string.Format("{0}\n     {1}", Title, Services);
        }

        public string Title
        {
            get
            {
                string distanceFormatted = Distance.ToString("### ### ###.00").Trim();
                string result = string.Format("**{0} [{1}]({2})**: {3}, {4} ls", STATIONEMOJI[(int)Type], Name, EDSMLink, STATIONTYPENAMES[(int)Type], distanceFormatted);
                return result;
            }
        }

        public string Title_NoLink
        {
            get
            {
                string distanceFormatted = Distance.ToString("### ### ###.00").Trim();
                string result = string.Format("**{0} {1}**: {2}, {3} ls", STATIONEMOJI[(int)Type], Name, STATIONTYPENAMES[(int)Type], distanceFormatted);
                return result;
            }
        }

        public string Services
        {
            get
            {
                List<string> services = new List<string>();
                if (HasRestock)
                {
                    services.Add("Restock");
                }
                if (HasRefuel)
                {
                    services.Add("Refuel");
                }
                if (HasRepair)
                {
                    services.Add("Repair");
                }
                if (HasShipyard)
                {
                    services.Add("Shipyard");
                }
                if (HasOutfitting)
                {
                    services.Add("Outfitting");
                }
                if (HasUniversalCartographics)
                {
                    services.Add("Universal Cartographics");
                }
                return services.Join(", ");
            }
        }

        public string Services_Link
        {
            get
            {
                return $"[Link]{EDSMLink} - {Services}";
            }
        }

        public static StationType ParseStationType(string input)
        {
            switch (input)
            {
                case "Orbis Starport":
                    return StationType.Orbis;
                case "Coriolis Starport":
                    return StationType.Coriolis;
                case "Ocellus Starport":
                    return StationType.Ocellus;
                case "Asteroid base":
                    return StationType.Asteroid;
                case "Outpost":
                    return StationType.Outpost;
                case "Planetary Port":
                case "Planetary Outpost":
                case "Planetary Settlement":
                    return StationType.Planetary;
                case "Mega ship":
                    return StationType.Megaship;
                case "Planetary Engineer Base":
                    return StationType.Unlandable;
                default:
                    return StationType.Other;
            }
        }

        public bool IsBetterThan(StationInfo other)
        {
            if (HasRefuel && !other.HasRefuel)
            {
                return true;
            }

            if (HasRepair && !other.HasRepair)
            {
                return true;
            }

            if (HasRestock && !other.HasRestock)
            {
                return true;
            }

            if (HasShipyard && !other.HasShipyard)
            {
                return true;
            }

            if (HasOutfitting && !other.HasOutfitting)
            {
                return true;
            }

            if (HasLargePadOrbital && !other.HasLargePadOrbital)
            {
                return true;
            }

            return Distance < other.Distance;
        }

        public enum StationType
        {
            Undefined,
            Orbis,
            Coriolis,
            Ocellus,
            Asteroid,
            Outpost,
            Planetary,
            Megaship,
            Unlandable,
            EngineerBase,
            Other
        }

        public static readonly string[] STATIONEMOJI = new string[] { "UNDEFINED", "<:orbis:553690990964244520>", "<:coriolis:553690991022964749>", "<:ocellus:553690990901460992>", "<:asteroid:553690991245262868>",
            "<:outpost:553690991060844567>", "<:planetary:553690991123496963>", "<:megaship:553690991144599573>", "<:unknown:553690991136342026>", "<:Engineer:554018579050397698>", "<:unknown:553690991136342026>" };
        public static readonly string[] STATIONTYPENAMES = new string[] { "Undefined", "Orbis Starport", "Coriolis Starport", "Ocellus Starport", "Asteroid Base", "Outpost", "Planetary", "Megaship", "Unlandable", "Engineer Base", "Other" };
    }
}
