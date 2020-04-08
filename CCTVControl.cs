//#define DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using System.Text;
using System.Linq;
using Oxide.Core.Plugins;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("CCTVControl", "RFC1920", "1.0.7")]
    [Description("Allows players to add CCTV cameras to a Computer Station and control them remotely")]
    class CCTVControl : RustPlugin
    {
        #region vars
        [PluginReference]
        private Plugin Clans, Friends, RustIO;

        private const string permCCTV = "cctvcontrol.use";
        private const string permCCTVAdmin = "cctvcontrol.admin";
        private const string permCCTVList = "cctvcontrol.list";
        private string moveSound = "assets/prefabs/deployable/playerioents/detectors/hbhfsensor/effects/detect_up.prefab";

        float userRange = 200f;
        float adminRange = 4000f;
        float mapSize = 0f;
        bool useFriends = false;
        bool useClans = false;
        bool useTeams = false;
        bool userMapWide = false;
        bool blockServerCams = false;
        bool playSound = true;
        bool playAtCamera = true;
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

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notauthorized"] = "You are not authorized to use this command!",
                ["foundCamera"] = "5ound Camera {0} owned by {1}",
                ["foundCameras"] = "Found Cameras:",
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
        protected override void LoadDefaultConfig()
        {
#if DEBUG
            Puts("Creating a new config file...");
#endif
            userRange = 200f;
            userMapWide = false;
            adminRange = 4000f;
            useFriends = false;
            useClans = false;
            useTeams = false;
            blockServerCams = false;
            LoadVariables();
        }

        private void LoadConfigVariables()
        {
            CheckCfgFloat("userRange", ref userRange);
            CheckCfgFloat("adminRange", ref adminRange);
            CheckCfg<bool>("useFriends", ref useFriends);
            CheckCfg<bool>("useClans", ref useClans);
            CheckCfg<bool>("useTeams", ref useTeams);
            CheckCfg<bool>("userMapWide", ref userMapWide);
            CheckCfg<bool>("blockServerCams", ref blockServerCams);
            CheckCfg<bool>("playSound", ref playSound);
            CheckCfg<bool>("playAtCamera", ref playAtCamera);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if(Config[Key] is T)
            {
                var = (T)Config[Key];
            }
            else
            {
                Config[Key] = var;
            }
        }

        private void CheckCfgFloat(string Key, ref float var)
        {
            if(Config[Key] != null)
            {
                var = Convert.ToSingle(Config[Key]);
            }
            else
            {
                Config[Key] = var;
            }
        }
        #endregion

        #region Main
        void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            var station = mountable.GetComponentInParent<ComputerStation>() ?? null;
            if(station != null && (blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
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
            Vis.Entities<ComputerStation>(player.transform.position, 2f, stations);
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
                                Puts($"TEST: '{cam}'");
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
                case "local":
                default:
                    foreach(var station in stations)
                    {
                        foundS = true;
                        Message(iplayer, "foundStation");
                        List<CCTV_RC> cameras = new List<CCTV_RC>();

                        float range = userRange;
                        if(userMapWide || iplayer.HasPermission(permCCTVAdmin)) range = mapSize;

                        Vis.Entities<CCTV_RC>(player.transform.position, range, cameras);
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
                                if((ent.OwnerID == 0) && (blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
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
            List<string> foundCameras = new List<string>();
            string msg = null;

            if((iplayer.Object as BasePlayer) == null)
            {
                Vis.Entities<CCTV_RC>(Vector3.zero, mapSize, cameras);
                Puts(Lang("foundCameras"));
                foreach(var camera in cameras)
                {
                    var realcam = camera as IRemoteControllable;
                    if(realcam == null) continue;
                    var loc = realcam.GetEyes();
                    var ent = realcam.GetEnt();
                    if(ent == null) continue;
                    var cname = realcam.GetIdentifier();
                    if(cname == null) continue;
                    if(foundCameras.Contains(cname)) continue;
                    foundCameras.Add(cname);
                    msg += cname + " @ " + loc.position.ToString() + Lang("ownedby") + ent.OwnerID.ToString() + "\n";
                }
                Puts(msg);
            }
            else
            {
                if(!iplayer.IsAdmin && !iplayer.HasPermission(permCCTVList)) return;
                var player = iplayer.Object as BasePlayer;

                Vis.Entities<CCTV_RC>(player.transform.position, adminRange, cameras);
                Message(iplayer, "foundCameras");
                foreach(var camera in cameras)
                {
                    var realcam = camera as IRemoteControllable;
                    if(realcam == null) continue;
                    var loc = realcam.GetEyes();
                    var ent = realcam.GetEnt();
                    if(ent == null) continue;
                    var cname = realcam.GetIdentifier();
                    if(cname == null) continue;
                    if(foundCameras.Contains(cname)) continue;
                    foundCameras.Add(cname);
                    msg += cname + " @ " + loc.position.ToString() + Lang("ownedby") + ent.OwnerID.ToString() + "\n";
                }
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
            if(useFriends && Friends != null)
            {
                var fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if(fr != null && (bool)fr)
                {
                    return true;
                }
            }
            if(useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan  = (string)Clans?.CallHook("GetClanOf", ownerid);
                if(playerclan == ownerclan && playerclan != null && ownerclan != null)
                {
                    return true;
                }
            }
            if(useTeams)
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

                        float y = input.IsDown(BUTTON.FORWARD) ? 1f : (input.IsDown(BUTTON.BACKWARD) ? -1f : 0f);
                        float x = input.IsDown(BUTTON.RIGHT) ? 1f : (input.IsDown(BUTTON.LEFT) ? -1f : 0f);
#if DEBUG
                        string ud = input.IsDown(BUTTON.FORWARD) ? "up" : (input.IsDown(BUTTON.BACKWARD) ? "down" : "");
                        string lr = input.IsDown(BUTTON.RIGHT) ? "right" : (input.IsDown(BUTTON.LEFT) ? "left" : "");
                        string udlr = ud + lr;
                        Puts($"Trying to move camera {udlr}.");
#endif
                        InputState inputState = new InputState();
                        inputState.current.mouseDelta.y = y * 0.2f;
                        inputState.current.mouseDelta.x = x * 0.2f;

                        cctv.UserInput(inputState, player);

                        if(playSound)
                        {
                            Effect effect = new Effect(moveSound, new Vector3(0, 0, 0), Vector3.forward);
                            if(playAtCamera)
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
