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
    class BaseCommand : Command
    {
        public override HandledContexts ArgumentParserMethod => HandledContexts.DMOnly;
        public override HandledContexts ExecutionMethod => HandledContexts.DMOnly;
        public override string Summary => "List possible bases given some criteria";
        public override bool RunInAsyncMode => true;
        public override Argument[] Arguments => new Argument[]
        {
            new Argument("System Name", "Name of the system to find a tempbase for"),
            new Argument("Mode", "`Default` = Does not check minor faction\n`Crime` = Hides stations owned by the minor faction controlling the target system", optional:true),
            new Argument("Jumprange", "Jumprange of your ship in lightyears, used for ETTA calculations. Assumes `20` ly", optional:true)
        };

        public BaseCommand(string identifier, CommandCollection collection)
        {
            Register(identifier, collection);
        }

        private string SystemName;
        private CommandMode Mode;
        private double JumpRange;

        protected override Task<ArgumentParseResult> ParseArguments(IDMCommandContext context)
        {
            SystemName = context.Argument;

            if (context.ArgumentCount == 1)
            {
                Mode = CommandMode.Default;
                JumpRange = 20;
                return Task.FromResult(ArgumentParseResult.SuccessfullParse);
            }

            context.ArgPointer++;

            if (!Enum.TryParse(context.Argument, out Mode))
            {
                return Task.FromResult(new ArgumentParseResult(Arguments[1]));
            }

            if (context.ArgumentCount == 2)
            {
                JumpRange = 20;
                return Task.FromResult(ArgumentParseResult.SuccessfullParse);
            }

            context.ArgPointer++;

            if (!double.TryParse(context.Argument, out JumpRange))
            {
                return Task.FromResult(new ArgumentParseResult(Arguments[2]));
            }

            return Task.FromResult(ArgumentParseResult.SuccessfullParse);
        }

        protected override async Task Execute(IDMCommandContext context)
        {
            string radiusRequest = WebRequestService.EDSM_MultipleSystemsRadius(SystemName, 50);
            RequestJSONResult requestResultRadius = await WebRequestService.GetWebJSONAsync(radiusRequest);

            if (!requestResultRadius.IsSuccess)
            {
                if (requestResultRadius.IsException)
                {
                    await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. Exception Message: `{0}`", requestResultRadius.ThrownException.Message), true);
                }
                else
                {
                    await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. HTTP Error: `{0} {1}`", (int)requestResultRadius.Status, requestResultRadius.Status.ToString()), true);
                }
                return;
            }

            if (requestResultRadius.JSON.IsObject)
            {
                await context.Channel.SendEmbedAsync("System not found in database!", true);
                return;
            }

            Dictionary<string, System> systemInfos = GetNames(requestResultRadius.JSON);

            List<System> allSystems = new List<System>(systemInfos.Values);

            foreach (System system in allSystems)
            {
                system.SetETTA(JumpRange);
            }

            allSystems.Sort(new SystemComparer() { RawDistance = true });

            const int requestCnt = 20;

            System[] requestSystems = new System[requestCnt];

            allSystems.CopyTo(0, requestSystems, 0, requestCnt);

            string infoRequest = WebRequestService.EDSM_MultipleSystemsInfo_URL(requestSystems.Select(system => { return system.Name; }), true, true, true, true);
            RequestJSONResult requestResultInfo = await WebRequestService.GetWebJSONAsync(infoRequest);

            if (!requestResultInfo.IsSuccess)
            {
                if (requestResultInfo.IsException)
                {
                    await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. Exception Message: `{0}`", requestResultInfo.ThrownException.Message), true);
                }
                else
                {
                    await context.Channel.SendEmbedAsync(string.Format("Could not connect to EDSMs services. HTTP Error: `{0} {1}`", (int)requestResultInfo.Status, requestResultInfo.Status.ToString()), true);
                }
                return;
            }

            System targetSystem = default;

            if (requestResultInfo.JSON.IsArray)
            {
                foreach (JSONField systemField in requestResultInfo.JSON.Array)
                {
                    if (systemField.IsObject)
                    {
                        if (systemField.Container.TryGetField("name", out string name))
                        {
                            if (systemInfos.TryGetValue(name, out System system))
                            {
                                system.FromJSON(systemField.Container);
                                if (system.Distance == 0)
                                {
                                    targetSystem = system;
                                }
                            }
                        }
                    }
                }
            }

            if (targetSystem.Name == null)
            {
                await context.Channel.SendEmbedAsync(string.Format("Internal Error! " + Macros.GetCodeLocation(), true));
                return;
            }

            string hideFaction = Mode == CommandMode.Crime ? targetSystem.Name : null;

            List<System> validSystems = new List<System>();
            foreach (System system in requestSystems)
            {
                if (await system.GetBestStation())
                {
                    system.SetETTA(JumpRange);
                    validSystems.Add(system);
                }
            }

            validSystems.Sort(new SystemComparer());

            EmbedBuilder embed = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Url = targetSystem.GetEDSM_URL(),
                    Name = $"Suggestions for a temporary base near {targetSystem.Name}"
                },
                Color = BotCore.EmbedColor
            };
            for (int i = 0; i < 5 && i < validSystems.Count; i++)
            {
                System system = validSystems[i];
                embed.AddField($"\"{system.Name}\" - \"{system.BestStation.Name}\"", $"{(system.RequirePermit ? $"{UnicodeEmoteService.Warning} Permit: `{system.PermitName}` " : string.Empty)} {system.BestStation.Services_Link}");
            }

            await context.Channel.SendEmbedAsync(embed);
        }

        private Dictionary<string, System> GetNames(JSONContainer json)
        {
            Dictionary<string, System> result = new Dictionary<string, System>(json.Array.Count);
            foreach (JSONField systemField in json.Array)
            {
                if (systemField.IsObject)
                {
                    JSONContainer systemJSON = systemField.Container;
                    if (systemJSON.TryGetField("name", out string name) && systemJSON.TryGetField("distance", out double distance))
                    {
                        result.Add(name, new System() { Name = name, Distance = distance });
                    }
                }
            }
            return result;
        }

        private class System
        {
            public string Name;
            public uint Id;
            public double Distance;

            public StationInfo BestStation;
            public TimeSpan ETTA;
            public bool HasStation => BestStation != null;

            public string ControllingFaction;
            public string Security;

            public bool RequirePermit;
            public string PermitName;

            public string GetEDSM_URL()
            {
                return $"https://www.edsm.net/en/system/id/{Id}/name/{Name.MakeWebRequestSafe()}";
            }

            public TimeSpan SetETTA(double jumprange)
            {
                if (HasStation)
                {
                    ETTA = EliteHelper.JumpETTA(Distance, jumprange) + EliteHelper.SuperCruiseETTA(BestStation.Distance);
                    return ETTA;
                }
                else
                {
                    ETTA = EliteHelper.JumpETTA(Distance, jumprange);
                    return ETTA;
                }
            }

            public void FromJSON(JSONContainer json)
            {
                json.TryGetField("name", out Name, Name);
                json.TryGetField("id", out Id);
                if (json.TryGetField("requirePermit", out RequirePermit))
                {
                    if (RequirePermit == true)
                    {
                        json.TryGetField("permitName", out PermitName, "Unknown Permit");
                    }
                }

                if (json.TryGetObjectField("information", out JSONContainer infoJSON))
                {
                    json.TryGetField("security", out Security, "Anarchy");
                    json.TryGetField("faction", out ControllingFaction);
                }
            }

            public async Task<bool> GetBestStation(string hideMinorFaction = null)
            {
                string request = WebRequestService.EDSM_SystemStations_URL(Name);
                RequestJSONResult result = await WebRequestService.GetWebJSONAsync(request);
                if (result.IsSuccess)
                {
                    // Setup Stations
                    if ((result.JSON != null) && result.JSON.TryGetArrayField("stations", out JSONContainer stationsJSON))
                    {
                        foreach (JSONField stationField in stationsJSON.Array)
                        {
                            if (stationField.IsObject)
                            {
                                StationInfo info = new StationInfo();
                                if (info.FromJSON(stationField.Container))
                                {
                                    if (string.IsNullOrEmpty(hideMinorFaction) || info.ControllingFaction != hideMinorFaction)
                                    {
                                        if (BestStation == null)
                                        {
                                            BestStation = info;
                                        }
                                        else if (info.IsBetterThan(BestStation))
                                        {
                                            BestStation = info;
                                        }
                                    }
                                }
                            }
                        }
                        return true;
                    }
                }
                return false;
            }
        }

        private class SystemComparer : IComparer<System>
        {
            public bool RawDistance = false;

            public int Compare(System x, System y)
            {
                if (x.HasStation && !y.HasStation) return -100000;
                if (!x.HasStation && y.HasStation) return 100000;

                if (RawDistance)
                {
                    return (int) (x.Distance - y.Distance);
                }
                return (int)(x.ETTA.TotalSeconds - y.ETTA.TotalSeconds);
            }
        }

        private enum CommandMode
        {
            Default,
            Crime
        }
    }
}
