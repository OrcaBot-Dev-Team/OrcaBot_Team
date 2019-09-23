using BotCoreNET.BotVars;
using JSON;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OrcaBot
{
    static class WebRequestService
    {
        private static string inara_appname;
        private static string inara_apikey;

        internal static void OnBotVarSetup()
        {
            BotVarManager.GlobalBotVars.SubscribeToBotVarUpdateEvent(OnBotVarUpdated, "inara_appname", "inara_apikey");
        }

        private static void OnBotVarUpdated(ulong guildId, BotVar var)
        {
            if (var.IsString)
            {
                if (var.Identifier == "inara_appname")
                {
                    inara_appname = var.String;
                }
                else if (var.Identifier == "inara_apikey")
                {
                    inara_apikey = var.String;
                }
            }
        }

        public static bool CanMakeInaraRequests
        {
            get
            {
                return inara_appname != null && inara_apikey != null;
            }
        }

        private static readonly HttpClient httpClient;

        static WebRequestService()
        {
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        #region WebRequest Methods

        public static async Task<RequestJSONResult> GetWebJSONAsync(string url, JSONContainer requestinfo)
        {
            RequestJSONResult loadresult = new RequestJSONResult();
            try
            {
                using (HttpRequestMessage requestmessage = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    requestmessage.Version = new Version(1, 1);
                    string requestcontent = requestinfo.Build();
                    requestmessage.Content = new StringContent(requestcontent, Encoding.UTF8, "application/json");
                    using (HttpResponseMessage responsemessage = await httpClient.SendAsync(requestmessage))
                    {
                        loadresult.Status = responsemessage.StatusCode;
                        loadresult.IsSuccess = responsemessage.IsSuccessStatusCode;
                        if (responsemessage.IsSuccessStatusCode)
                        {
                            loadresult.rawData = await responsemessage.Content.ReadAsStringAsync();

                            JSONContainer.TryParse(loadresult.rawData, out loadresult.JSON, out loadresult.jsonParseError);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                loadresult.IsException = true;
                loadresult.ThrownException = e;
            }
            return loadresult;
        }

        public static async Task<RequestJSONResult> GetWebJSONAsync(string url)
        {
            RequestJSONResult loadresult = new RequestJSONResult();
            try
            {
                using (HttpRequestMessage requestmessage = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    requestmessage.Version = new Version(1, 1);
                    using (HttpResponseMessage responsemessage = await httpClient.SendAsync(requestmessage))
                    {
                        loadresult.Status = responsemessage.StatusCode;
                        loadresult.IsSuccess = responsemessage.IsSuccessStatusCode;
                        if (responsemessage.IsSuccessStatusCode)
                        {
                            loadresult.rawData = await responsemessage.Content.ReadAsStringAsync();
                            JSONContainer.TryParse(loadresult.rawData, out loadresult.JSON, out loadresult.jsonParseError);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                loadresult.IsException = true;
                loadresult.ThrownException = e;
            }
            return loadresult;
        }

        #endregion
        #region Macro Methods for generating requests

        public static string EDSM_SystemInfo_URL(string systemName, bool showId = false, bool showCoords = false, bool showPermit = false, bool showInformation = false, bool showPrimaryStar = false)
        {
            StringBuilder result = new StringBuilder();
            result.Append("https://www.edsm.net/api-v1/system?sysname=");
            result.Append(systemName.MakeWebRequestSafe());
            if (showId)
            {
                result.Append("&showId=1");
            }
            if (showCoords)
            {
                result.Append("&showCoordinates=1");
            }
            if (showPermit)
            {
                result.Append("&showPermit=1");
            }
            if (showInformation)
            {
                result.Append("&showInformation=1");
            }
            if (showPrimaryStar)
            {
                result.Append("&showPrimaryStar=1");
            }
            return result.ToString();
        }

        public static string EDSM_MultipleSystemsInfo_URL(IEnumerable<string> systemNames, bool showId = false, bool showCoords = false, bool showPermit = false, bool showInformation = false, bool showPrimaryStar = false)
        {
            StringBuilder result = new StringBuilder();
            result.Append("https://www.edsm.net/api-v1/systems?");
            bool first = true;
            foreach (string systemName in systemNames)
            {
                if (first)
                {
                    first = false;
                    result.Append("systemName[]=");
                }
                else
                {
                    result.Append("&systemName[]=");
                }
                result.Append(systemName.MakeWebRequestSafe());
            }
            if (showId)
            {
                result.Append("&showId=1");
            }
            if (showCoords)
            {
                result.Append("&showCoordinates=1");
            }
            if (showPermit)
            {
                result.Append("&showPermit=1");
            }
            if (showInformation)
            {
                result.Append("&showInformation=1");
            }
            if (showPrimaryStar)
            {
                result.Append("&showPrimaryStar=1");
            }
            return result.ToString();
        }

        public static string EDSM_SystemStations_URL(string systemName)
        {
            StringBuilder result = new StringBuilder();
            result.Append("https://www.edsm.net/api-system-v1/stations?systemName=");
            result.Append(systemName.MakeWebRequestSafe());
            return result.ToString();
        }

        public static string EDSM_MultipleSystemsRadius(string systemName, int radius = 10)
        {
            StringBuilder result = new StringBuilder();
            result.Append("https://www.edsm.net/api-v1/sphere-systems?systemName=");
            result.Append(systemName.MakeWebRequestSafe());
            result.Append($"&radius={radius}");
            return result.ToString();
        }

        public static string EDSM_SystemTraffic_URL(string systemName)
        {
            return "https://www.edsm.net/api-system-v1/traffic?systemName=" + systemName.Replace(' ', '+');
        }

        public static string EDSM_SystemDeaths_URL(string systemName)
        {
            return "https://www.edsm.net/api-system-v1/deaths?systemName=" + systemName.Replace(' ', '+');
        }

        public static string EDSM_Commander_Location(string commanderName, bool showId = false, bool showCoordinates = false)
        {
            StringBuilder result = new StringBuilder();
            result.Append("https://www.edsm.net/api-logs-v1/get-position?commanderName=");
            result.Append(commanderName.MakeWebRequestSafe());
            if (showId)
            {
                result.Append("&showId=1");
            }
            if (showCoordinates)
            {
                result.Append("&showCoordinates=1");
            }
            return result.ToString();
        }

        internal static string BGSBOT_FactionStatus(string factionName)
        {
            return "https://elitebgs.app/api/ebgs/v4/factions?name=" + factionName.MakeWebRequestSafe();
        }

        internal static JSONContainer Inara_CMDR_Profile(string cmdrName)
        {
            if (inara_apikey == null || inara_appname == null)
            {
                return null;
            }
            else
            {
                JSONContainer result = Inara_Base_Request(inara_appname, inara_apikey);

                JSONContainer singleevent = JSONContainer.NewObject();
                singleevent.TryAddField("eventName", "getCommanderProfile");
                singleevent.TryAddField("eventTimestamp", DateTime.UtcNow.ToString("s") + "Z");
                JSONContainer eventdata = JSONContainer.NewObject();
                eventdata.TryAddField("searchName", cmdrName);
                singleevent.TryAddField("eventData", eventdata);
                JSONContainer events = JSONContainer.NewArray();
                events.Add(singleevent);
                result.TryAddField("events", events);
                return result;
            }
        }

        internal static JSONContainer Inara_Base_Request(string inara_appname, string inara_apikey)
        {
            JSONContainer result = JSONContainer.NewObject();
            JSONContainer header = JSONContainer.NewObject();
            header.TryAddField("appName", inara_appname);
            header.TryAddField("appVersion", new Version(1, 8).ToString());
            header.TryAddField("isDeveloped", true);
            header.TryAddField("APIkey", inara_apikey);
            result.TryAddField("header", header);
            return result;
        }

        internal static string MakeWebRequestSafe(this string str)
        {
            return str.Replace("+", "%2B").Replace(' ', '+');
        }

        #endregion
    }

    public class RequestJSONResult
    {
        public JSONContainer JSON = null;
        public string rawData = null;
        public string jsonParseError = null;
        public HttpStatusCode Status = HttpStatusCode.Continue;
        public Exception ThrownException = null;

        public bool IsSuccess = false;
        public bool IsException = false;
    }
}
