#define DEBUG
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
    [Info("CCTVControl", "RFC1920", "1.0.4")]
    [Description("Allows players to add their local CCTV cameras in bulk to a Computer Station")]
    class CCTVControl : RustPlugin
    {
        #region vars
        [PluginReference]
        private Plugin Clans, Friends, RustIO;

        private const string permCCTV = "cctvcontrol.use";
        private const string permCCTVAdmin = "cctvcontrol.admin";
        private const string permCCTVList = "cctvcontrol.list";

        float userRange = 200f;
        float adminRange = 4000f;
        float mapSize = 0f;
        bool useFriends = false;
        bool useClans = false;
        bool useTeams = false;
        bool userMapWide = false;
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
                ["foundCamera"] = "Found Camera {0} owned by {1}",
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

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if(data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                //Changed = true;
            }

            object value;
            if(!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                //Changed = true;
            }
            return value;
        }
        #endregion

        #region Main
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
                            Puts($"Found camera {cname}.");
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
                                if(ent.OwnerID == 0)
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
                if(msg != null) Message(iplayer, msg);
            }
        }

        void AddCamera(BasePlayer basePlayer, ComputerStation station, string str)
        {
            uint d = 0;
            BaseNetworkable baseNetworkable;
            IRemoteControllable component;
            bool flag = false;
            foreach (IRemoteControllable allControllable in RemoteControlEntity.allControllables)
            {
                if (allControllable == null || !(allControllable.GetIdentifier() == str))
                {
                    continue;
                }
                if (allControllable.GetEnt() != null)
                {
                    d = allControllable.GetEnt().net.ID;
                    flag = true;
                    if (!flag)
                    {
                        return;
                    }
                    baseNetworkable = BaseNetworkable.serverEntities.Find(d);
                    if (baseNetworkable == null)
                    {
                        return;
                    }
                    component = baseNetworkable.GetComponent<IRemoteControllable>();
                    if (component == null)
                    {
                        return;
                    }
                    if (str == component.GetIdentifier())
                    {
                        station.controlBookmarks.Add(str, d);
                    }
                    station.SendControlBookmarks(basePlayer);
                    return;
                }
                else
                {
                    Debug.LogWarning("Computer station added bookmark with missing ent, likely a static CCTV (wipe the server)");
                }
            }
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

//        private void OnPlayerInput(BasePlayer player, InputState input)
//        {
//            if(player == null || input == null) return;
//            try
//            {
//                var activeCamera = player.GetMounted().GetComponentInParent<ComputerStation>() ?? null;
//                if(activeCamera != null)
//                {
//                    activeCamera.currentlyControllingEnt.Get(true).GetComponent<IRemoteControllable>().UserInput(input, player);
//                }
//            }
//            catch {}
//        }

        // How they accept input from the user to a camera
//        public override void UserInput(InputState inputState, BasePlayer player)
//        {
//            if (!this.hasPTZ)
//            {
//                return;
//            }
//            float single = 1f;
//            float single1 = Mathf.Clamp(-inputState.current.mouseDelta.y, -1f, 1f);
//            float single2 = Mathf.Clamp(inputState.current.mouseDelta.x, -1f, 1f);
//            this.pitchAmount = Mathf.Clamp(this.pitchAmount + single1 * single * this.turnSpeed, this.pitchClamp.x, this.pitchClamp.y);
//            this.yawAmount = Mathf.Clamp(this.yawAmount + single2 * single * this.turnSpeed, this.yawClamp.x, this.yawClamp.y);
//            Quaternion quaternion = Quaternion.Euler(this.pitchAmount, 0f, 0f);
//            Quaternion quaternion1 = Quaternion.Euler(0f, this.yawAmount, 0f);
//            this.pitch.transform.localRotation = quaternion;
//            this.yaw.transform.localRotation = quaternion1;
//            if (single1 != 0f || single2 != 0f)
//            {
//                base.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
//            }
//        }
        #endregion
    }
}
