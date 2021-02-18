#region License (GPL v3)
/*
    DESCRIPTION
    Copyright (c) 2020 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v3)
//#define DEBUG
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("CCTVControl", "RFC1920", "1.0.10")]
    [Description("Allows players to add CCTV cameras to a Computer Station and control them remotely")]
    class CCTVControl : RustPlugin
    {
        #region vars
        ConfigData configData;
        [PluginReference]
        private Plugin Clans, Friends, RustIO;

        private const string permCCTV = "cctvcontrol.use";
        private const string permCCTVAdmin = "cctvcontrol.admin";
        private const string permCCTVList = "cctvcontrol.list";
        private readonly string moveSound = "assets/prefabs/deployable/playerioents/detectors/hbhfsensor/effects/detect_up.prefab";
        private readonly int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Default", "Trigger", "Deployed", "AI", "Deployable");

        float mapSize = 0f;
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region init
        void Init()
        {
            AddCovalenceCommand("cctv", "cmdCCTV");
            AddCovalenceCommand("cctvlist", "cmdCCTVList");

            permission.RegisterPermission(permCCTV, this);
            permission.RegisterPermission(permCCTVAdmin, this);
            permission.RegisterPermission(permCCTVList, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notauthorized"] = "You are not authorized to use this command!",
                ["foundCamera"] = "Found Camera {0} owned by {1}",
                ["foundDrone"] = "Found Drone {0} owned by {1}",
                ["foundCameras"] = "Found Cameras:",
                ["foundDrones"] = "Found Drones:",
                ["cameraexists"] = "Camera {0} already in station",
                ["ownedby"] = " owned by ",
                ["server"] = "Server",
                ["unknown"] = "Unknown",
                ["foundStation"] = "Found ComputerStation...",
                ["noStation"] = "No ComputerStation found...",
                ["helptext1"] = "CCTV Control Instructions:",
                ["helptext2"] = "  type /cctv to add your local cameras",
            }, this);
        }

        void Loaded()
        {
            mapSize = ConVar.Server.worldsize > 0 ? ConVar.Server.worldsize : 4000f;
            LoadVariables();
        }
        #endregion

        #region Config
        public class ConfigData
        {
            public float userRange = 200f;
            public float adminRange = 4000f;
            public bool useFriends = false;
            public bool useClans = false;
            public bool useTeams = false;
            public bool userMapWide = false;
            public bool blockServerCams = false;
            public bool playSound = true;
            public bool playAtCamera = true;

            public VersionNumber Version;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData
            {
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
#if DEBUG
            Puts("Loading configuration...");
#endif
            configData = Config.ReadObject<ConfigData>();
            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        #endregion

        #region Main
        void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            var station = mountable.GetComponentInParent<ComputerStation>() ?? null;
            if(station != null && (configData.blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
            {
#if DEBUG
                Puts("OnEntityMounted: player mounted CS!");
#endif
                List<string> toremove = new List<string>();
                foreach(KeyValuePair<string, uint> bm in station.controlBookmarks)
                {
                    var cament = BaseNetworkable.serverEntities.Find(bm.Value);
                    var realcam = cament as IRemoteControllable;
                    var ent = realcam.GetEnt();
                    if(ent == null) continue;
                    var cname = realcam.GetIdentifier();
                    if(cname == null) continue;
                    if(ent.OwnerID == 0)
                    {
                        toremove.Add(cname);
                    }
                }
                foreach(string cn in toremove)
                {
                    station.controlBookmarks.Remove(cn);
                }
                station.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                station.SendControlBookmarks(player);
            }
        }

        void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            var station = mountable.GetComponentInParent<ComputerStation>() ?? null;
            if(station != null)
            {
#if DEBUG
                Puts("OnEntityMounted: player dismounted CS!");
#endif
            }
        }

        [Command("cctv")]
        void cmdCCTV(IPlayer iplayer, string command, string[] args)
        {
            if((iplayer.Object as BasePlayer) == null) return;
            if(!iplayer.HasPermission(permCCTV)) { Message(iplayer, "notauthorized"); return; }
            var player = iplayer.Object as BasePlayer;

            List<ComputerStation> stations = new List<ComputerStation>();
            Vis.Entities(player.transform.position, 2f, stations);
            bool foundS = false;

            string cmd = null;
            if(args.Length == 0) cmd = null;
            else cmd = args[0];

            switch(cmd)
            {
                case "clear":
                    foreach(var station in stations)
                    {
                        station.controlBookmarks.Clear();
                        station.SendControlBookmarks(player);
                    }
                    break;
//                case "rename":
//                    {
//                        List<CCTV_RC> cameras = new List<CCTV_RC>();
//                        Vis.Entities(player.transform.position, 2f, cameras);
//                        foreach(var cam in cameras)
//                        {
//                            cam.UpdateIdentifier(args[1]);
//                            break;
//                        }
//                    }
//                    break;
                case "add":
                    if(args.Length > 1)
                    {
                        List<string> cameras = new List<string>();
                        if(args[1].Contains(","))
                        {
                            string newargs = string.Join("", args);
                            newargs = newargs.Replace("add", "");
                            cameras = newargs.Split(',').ToList();
                        }
                        else
                        {
                            cameras.Add(args[1].Trim());
                        }

                        foreach(var station in stations)
                        {
                            foreach(var cname in cameras)
                            {
                                string cam = cname.Trim();
                                if(station.controlBookmarks.ContainsKey(cam))
                                {
                                    Message(iplayer, "cameraexists", cam);
                                    continue;
                                }
                                AddCamera(player, station, cam);
                            }
                        }
                    }
                    break;
                case "drones":
                    foreach(var station in stations)
                    {
                        foundS = true;
                        Message(iplayer, "foundStation");
                        List<Drone> drones = new List<Drone>();

                        float range = configData.userRange;
                        if(configData.userMapWide || iplayer.HasPermission(permCCTVAdmin)) range = mapSize;

                        Vis.Entities(player.transform.position, range, drones, targetLayer);
                        List<string> foundCameras = new List<string>();

                        foreach(var camera in drones)
                        {
                            var realcam = camera as IRemoteControllable;
                            if(realcam == null) continue;
                            var ent = realcam.GetEnt();
                            if(ent == null) continue;
                            var cname = realcam.GetIdentifier();
                            if(cname == null) continue;
                            if(foundCameras.Contains(cname)) continue;
#if DEBUG
                            Puts($"Found drone {cname} at {ent.transform.position.ToString()}.");
#endif
                            if((ent.OwnerID.ToString() == iplayer.Id || IsFriend(player.userID, ent.OwnerID)) || iplayer.HasPermission(permCCTVAdmin))
                            {
                                if(station.controlBookmarks.ContainsKey(cname))
                                {
                                    Message(iplayer, "cameraexists", cname);
                                    continue;
                                }
                                foundCameras.Add(cname);

                                string displayName = Lang("unknown");
                                if((ent.OwnerID == 0) && (configData.blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
                                {
#if DEBUG
                                    Puts($"Disabling server-owned drone {cname} {ent.OwnerID.ToString()}.");
#endif
                                    continue;
                                }
                                else if(ent.OwnerID == 0)
                                {
                                    displayName = Lang("server");
                                }
                                else
                                {
                                    var pl = BasePlayer.Find(ent.OwnerID.ToString());
                                    if(pl != null) displayName = pl.displayName;
                                }

                                Message(iplayer, "foundDrone", cname, displayName);
                                AddCamera(player, station, cname);
                            }
                        }
                        break;
                    }
                    if(!foundS)
                    {
                        Message(iplayer, "noStation");
                    }
                    break;
                case "local":
                default:
                    foreach(var station in stations)
                    {
                        foundS = true;
                        Message(iplayer, "foundStation");
                        List<CCTV_RC> cameras = new List<CCTV_RC>();

                        float range = configData.userRange;
                        if(configData.userMapWide || iplayer.HasPermission(permCCTVAdmin)) range = mapSize;

                        Vis.Entities(player.transform.position, range, cameras, targetLayer);
                        List<string> foundCameras = new List<string>();
#if DEBUG
                        Puts($"Searching for cameras over a {range.ToString()}m radius.");
#endif
                        foreach(var camera in cameras)
                        {
                            var realcam = camera as IRemoteControllable;
                            if(realcam == null) continue;
                            var ent = realcam.GetEnt();
                            if(ent == null) continue;
                            var cname = realcam.GetIdentifier();
                            if(cname == null) continue;
                            if(foundCameras.Contains(cname)) continue;
#if DEBUG
                            Puts($"Found camera {cname} at {ent.transform.position.ToString()}.");
#endif
                            if((ent.OwnerID.ToString() == iplayer.Id || IsFriend(player.userID, ent.OwnerID)) || iplayer.HasPermission(permCCTVAdmin))
                            {
                                if(station.controlBookmarks.ContainsKey(cname))
                                {
                                    Message(iplayer, "cameraexists", cname);
                                    continue;
                                }
                                foundCameras.Add(cname);

                                string displayName = Lang("unknown");
                                if((ent.OwnerID == 0) && (configData.blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
                                {
#if DEBUG
                                    Puts($"Disabling server-owned camera {cname} {ent.OwnerID.ToString()}.");
#endif
                                    continue;
                                }
                                else if(ent.OwnerID == 0)
                                {
                                    displayName = Lang("server");
                                }
                                else
                                {
                                    var pl = BasePlayer.Find(ent.OwnerID.ToString());
                                    if(pl != null) displayName = pl.displayName;
                                }

                                Message(iplayer, "foundCamera", cname, displayName);
                                AddCamera(player, station, cname);
                            }
                        }
                        break;
                    }
                    if(!foundS)
                    {
                        Message(iplayer, "noStation");
                    }
                    break;
            }
        }

        [Command("cctvlist")]
        void cmdCCTVList(IPlayer iplayer, string command, string[] args)
        {
            List<CCTV_RC> cameras = new List<CCTV_RC>();
            List<Drone> drones = new List<Drone>();
            List<string> foundCameras = new List<string>();
            string msg = null;

            Vis.Entities(Vector3.zero, mapSize, cameras);
            Vis.Entities(Vector3.zero, mapSize, drones);

            msg += Lang("foundCameras") + "\n";
            foreach (var camera in cameras)
            {
                var realcam = camera as IRemoteControllable;
                if (realcam == null) continue;
                var loc = realcam.GetEyes();
                var ent = realcam.GetEnt();
                if (ent == null) continue;
                var cname = realcam.GetIdentifier();
                if (cname == null) continue;
                if (foundCameras.Contains(cname)) continue;
                foundCameras.Add(cname);
                msg += cname + " @ " + loc.position.ToString() + Lang("ownedby") + ent.OwnerID.ToString() + "\n";
            }
            msg += Lang("foundDrones") + "\n";
            foreach (var camera in drones)
            {
                var realcam = camera as IRemoteControllable;
                if (realcam == null) continue;
                var loc = realcam.GetEyes();
                var ent = realcam.GetEnt();
                if (ent == null) continue;
                var cname = realcam.GetIdentifier();
                if (cname == null) continue;
                if (foundCameras.Contains(cname)) continue;
                foundCameras.Add(cname);
                msg += cname + " @ " + loc.position.ToString() + Lang("ownedby") + ent.OwnerID.ToString() + "\n";
            }

            if ((iplayer.Object as BasePlayer) == null)
            {
                Puts(msg);
            }
            else
            {
                Message(iplayer, msg);
            }
        }

        void AddCamera(BasePlayer basePlayer, ComputerStation station, string str)
        {
#if DEBUG
            Puts($"Trying to add camera {str}");
#endif
            uint d = 0;
            BaseNetworkable baseNetworkable;
            IRemoteControllable component;
            foreach(IRemoteControllable allControllable in RemoteControlEntity.allControllables)
            {
                var curr = allControllable.GetIdentifier();
                if(allControllable == null)
                {
#if DEBUG
                    Puts($"  skipping null camera {curr}");
#endif
                    continue;
                }
                if(curr != str) continue;

                if(allControllable.GetEnt() != null)
                {
                    d = allControllable.GetEnt().net.ID;
                    baseNetworkable = BaseNetworkable.serverEntities.Find(d);

                    if(baseNetworkable == null)
                    {
#if DEBUG
                        Puts("  baseNetworkable null");
#endif
                        return;
                    }
                    component = baseNetworkable.GetComponent<IRemoteControllable>();
                    if(component == null)
                    {
#if DEBUG
                        Puts("  component null");
#endif
                        return;
                    }
                    if(str == component.GetIdentifier())
                    {
#if DEBUG
                        Puts("  adding to station...");
#endif
                        station.controlBookmarks.Add(str, d);
                    }
                    station.SendControlBookmarks(basePlayer);
                    return;
                }
                else
                {
#if DEBUG
                    Puts("Computer station added bookmark with missing ent, likely a static CCTV (wipe the server)");
#endif
                    return;
                }
            }
#if DEBUG
            Puts($"  {str} cannot be controlled.  Check power!");
#endif
        }

        // playerid = active player, ownerid = owner of camera, who may be offline
        bool IsFriend(ulong playerid, ulong ownerid)
        {
            if(configData.useFriends && Friends != null)
            {
                var fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if(fr != null && (bool)fr)
                {
                    return true;
                }
            }
            if(configData.useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan  = (string)Clans?.CallHook("GetClanOf", ownerid);
                if(playerclan == ownerclan && playerclan != null && ownerclan != null)
                {
                    return true;
                }
            }
            if(configData.useTeams)
            {
                BasePlayer player = BasePlayer.FindByID(playerid);
                if(player.currentTeam != (long)0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);
                    if(playerTeam == null) return false;
                    if(playerTeam.members.Contains(ownerid))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if(player == null || input == null) return;

            if(input.IsDown(BUTTON.FORWARD) || input.IsDown(BUTTON.BACKWARD) || input.IsDown(BUTTON.LEFT) || input.IsDown(BUTTON.RIGHT))
            {
                try
                {
                    var activeCamera = player.GetMounted().GetComponentInParent<ComputerStation>() ?? null;
                    if(activeCamera != null)
                    {
                        var cctv = activeCamera.currentlyControllingEnt.Get(true).GetComponent<CCTV_RC>();
                        if(cctv == null) return;
                        if(cctv.IsStatic() && !player.IPlayer.HasPermission(permCCTVAdmin)) return;
                        cctv.hasPTZ = true;

                        float x = input.IsDown(BUTTON.RIGHT) ? 1f : (input.IsDown(BUTTON.LEFT) ? -1f : 0f);
                        float y = input.IsDown(BUTTON.FORWARD) ? 1f : (input.IsDown(BUTTON.BACKWARD) ? -1f : 0f);
#if DEBUG
                        string lr = input.IsDown(BUTTON.RIGHT) ? "right" : (input.IsDown(BUTTON.LEFT) ? "left" : "");
                        string ud = input.IsDown(BUTTON.FORWARD) ? "up" : (input.IsDown(BUTTON.BACKWARD) ? "down" : "");
                        string udlr = ud + lr;
                        Puts($"Trying to move camera {udlr}.");
#endif
                        float speed = 0.1f;
                        if(input.IsDown(BUTTON.SPRINT)) speed *= 3;

                        InputState inputState = new InputState();
                        inputState.current.mouseDelta.y = y * speed;
                        inputState.current.mouseDelta.x = x * speed;

                        cctv.UserInput(inputState, player);

                        if(configData.playSound)
                        {
                            Effect effect = new Effect(moveSound, new Vector3(0, 0, 0), Vector3.forward);
                            if(configData.playAtCamera)
                            {
                                effect.worldPos = cctv.transform.position;
                                effect.origin   = cctv.transform.position;
                            }
                            else
                            {
                                effect.worldPos = player.transform.position;
                                effect.origin   = player.transform.position;
                            }
                            EffectNetwork.Send(effect);
                        }
                    }
                }
                catch {}
            }
        }
        #endregion
    }
}
