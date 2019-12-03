﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Aurora.Devices.Omen
{
    class HueInfo
    {
        public string HueBridgeAddress { get; set; }
        public string UserName { get; set; }
    }

    class BridgetInfo
    {
        public string id { get; set; }
        public string internalipaddress { get; set; }
    }

    struct HSL
    {
        public int H { get; set; }
        public float S { get; set; }
        public float L { get; set; }

        public static HSL fromColor(Color c)
        {
            HSL hsl = new HSL();

            float r = (c.R / 255.0f);
            float g = (c.G / 255.0f);
            float b = (c.B / 255.0f);

            float min = Math.Min(Math.Min(r, g), b);
            float max = Math.Max(Math.Max(r, g), b);
            float delta = max - min;

            hsl.L = (max + min) / 2;

            if (delta == 0)
            {
                hsl.H = 0;
                hsl.S = 0.0f;
            }
            else
            {
                hsl.S = (hsl.L <= 0.5) ? (delta / (max + min)) : (delta / (2 - max - min));

                float hue;

                if (r == max)
                {
                    hue = ((g - b) / 6) / delta;
                }
                else if (g == max)
                {
                    hue = (1.0f / 3) + ((b - r) / 6) / delta;
                }
                else
                {
                    hue = (2.0f / 3) + ((r - g) / 6) / delta;
                }

                if (hue < 0)
                    hue += 1;
                if (hue > 1)
                    hue -= 1;

                hsl.H = (int)(hue * 360);
            }

            return hsl;
        }
    }

    public class PhilipseHue
    {
        HueInfo info;
        private IRestClient restClient;
        private List<string> lightingIds;
        static private string HueConfigFIleName = "Philips_hue_ligting_config.json";
        private UInt64 index = 0;

        private PhilipseHue(HueInfo info)
        {
            this.info = info;
            lightingIds = new List<string>();
            restClient = new RestClient(@"http://" + info.HueBridgeAddress + @"/api/" + info.UserName);
            var request = new RestRequest("lights", Method.GET);
            var response = restClient.Get(request);
            Global.logger.Info(response.Content);
            
            using (TextReader sr = new StringReader(response.Content))
            {
                var reader = new JsonTextReader(sr);
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return;
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    string id = reader.Value as string;
                    if(reader.Read() && reader.TokenType == JsonToken.StartObject)
                    {
                        lightingIds.Add(id);
                        ParseLightingObject(reader);
                    }
                }
            }

            TurnOn();
            SetLighting(Color.Black);
        }

        private void ParseLightingObject(JsonTextReader reader)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    ParseLightingObject(reader);
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    return;
                }
            }
        }

        public static PhilipseHue GetPhilipseHue()
        {
            HueInfo info;
            try
            {
                string config = System.IO.File.ReadAllText(System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase + HueConfigFIleName);
                if (!string.IsNullOrEmpty(config))
                {
                    info = JsonConvert.DeserializeObject<HueInfo>(config);
                    if (info == null) return null;

                    var client = new RestClient(@"https://discovery.meethue.com");
                    client.Timeout = 750;
                    var request = new RestRequest(Method.GET);
                    var response = client.Execute(request);
                    if (response.ErrorException == null)
                    {
                        Global.logger.Info(response.Content);
                        BridgetInfo[] bridgeInfos = JsonConvert.DeserializeObject<BridgetInfo[]>(response.Content);
                        if (bridgeInfos.Length > 0)
                        {
                            info.HueBridgeAddress = bridgeInfos[0].internalipaddress;
                        }
                    }

                    Ping pingSender = new Ping();
                    var pingReply = pingSender.Send(info.HueBridgeAddress, 120);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        return new PhilipseHue(info);
                    }
                }
            }
            catch (Exception e)
            {
                Global.logger.Error("Create philips lighting fail. Message: " + e);
            }

            return null;
        }

        private void TurnOn()
        {
            foreach(var id in lightingIds)
            {
                var request = new RestRequest($"lights/{id}/state", Method.PUT);
                request.AddHeader("Content-Type", "application/json");

                request.AddJsonBody(new
                {
                    on = true
                });

                var response = restClient.Execute(request);
            }
        }

        private void SetLighting(Color color)
        {
            if (index++ % 3 != 0) return;

            HSL hsl = HSL.fromColor(color);

            lightingIds.Reverse();
            foreach (var id in lightingIds)
            {
                var request = new RestRequest($"lights/{id}/state", Method.PUT);
                request.AddHeader("Content-Type", "application/json");

                request.AddJsonBody(new
                {
                    on = true,
                    sat = (byte)(hsl.S * 254 + 0.5),
                    bri = (byte)(hsl.L * 254 + 0.5),
                    hue = (UInt16)(hsl.H * 65535.0 / 360.0 + 0.5)
                });

                var response = restClient.Execute(request);
            }
        }

        public void SetKeys(Dictionary<DeviceKeys, Color> keyColors)
        {
            if (keyColors.ContainsKey(DeviceKeys.SPACE))
            {
                SetLighting(keyColors[DeviceKeys.SPACE]);
            }
        }
    }
}
