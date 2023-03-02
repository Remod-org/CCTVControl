#region License (GPL v2)
/*
    CCTV Control
    Copyright (c) 2020-2023 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the license only.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v2)
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using Oxide.Core.Extensions;

namespace Oxide.Plugins
{
    [Info("CCTVControl", "RFC1920", "1.0.12")]
    [Description("Allows players to add CCTV cameras to a Computer Station and control them remotely")]
    internal class CCTVControl : RustPlugin
    {
        #region vars
        private ConfigData configData;
        private readonly Plugin Clans, Friends, RustIO;
        private const string permCCTV = "cctvcontrol.use";
        private const string permCCTVAdmin = "cctvcontrol.admin";
        private const string permCCTVList = "cctvcontrol.list";
        private readonly string moveSound = "assets/prefabs/deployable/playerioents/detectors/hbhfsensor/effects/detect_up.prefab";
        private readonly int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Default", "Trigger", "Deployed", "AI", "Deployable");

        private float mapSize;
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region init
        private void Init()
        {
            AddCovalenceCommand("cctv", "cmdCCTV");
            AddCovalenceCommand("cctvlist", "cmdCCTVList");

            permission.RegisterPermission(permCCTV, this);
            permission.RegisterPermission(permCCTVAdmin, this);
            permission.RegisterPermission(permCCTVList, this);
        }

        private void DestroyAll<T>()
        {
            Object[] objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            if (objects != null)
            {
                foreach (Object gameObj in objects)
                {
                    UnityEngine.Object.Destroy(gameObj);
                }
            }
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

        private void Loaded()
        {
            mapSize = ConVar.Server.worldsize > 0 ? ConVar.Server.worldsize : 4000f;
            LoadVariables();
        }
        #endregion

        #region Config
        public class ConfigData
        {
            public bool debug;
            public float userRange;
            public float adminRange;
            public bool useFriends;
            public bool useClans;
            public bool useTeams;
            public bool userMapWide;
            public bool blockServerCams;
            public bool playSound;
            public bool playAtCamera;

            public VersionNumber Version;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                debug = false,
                userRange = 200f,
                adminRange = 4000f,
                useFriends = false,
                useClans = false,
                useTeams = false,
                userMapWide = false,
                blockServerCams = false,
                playSound = true,
                playAtCamera = true,
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
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
        //private object OnCCTVDirectionChange(CCTV_RC camera, BasePlayer player)
        //{
        //    Puts("Moving light to match camera");
        //    BaseEntity sl = camera.gameObject.GetComponentInChildren<SimpleLight>() as BaseEntity;
        //    //sl.transform.forward = new Vector3(camera.pivotOrigin.forward.x, sl.transform.forward.y, camera.pivotOrigin.forward.z);
        //    sl.transform.forward = new Vector3(camera.transform.forward.x, sl.transform.forward.y, camera.transform.forward.z);
        //    return null;
        //}

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            ComputerStation station = mountable.GetComponentInParent<ComputerStation>();
            if (station != null && (configData.blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
            {
                if (configData.debug) Puts("OnEntityMounted: player mounted CS!");
                List<string> toremove = new List<string>();
                foreach (string bm in station.controlBookmarks)
                {
                    IRemoteControllable realcam = RemoteControlEntity.FindByID(bm);
                    BaseEntity ent = realcam.GetEnt();
                    if (ent == null) continue;
                    string cname = realcam.GetIdentifier();
                    if (cname == null) continue;
                    if (ent.OwnerID == 0)
                    {
                        toremove.Add(cname);
                    }
                }
                foreach (string cn in toremove)
                {
                    station.controlBookmarks.Remove(cn);
                }
                station.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                station.SendControlBookmarks(player);
            }
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            ComputerStation station = mountable.GetComponentInParent<ComputerStation>();
            if (station != null)
            {
                if (configData.debug) Puts("OnEntityMounted: player dismounted CS!");
            }
        }

        public void RemoveComps(BaseEntity obj)
        {
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<GroundWatch>());
            foreach (MeshCollider mesh in obj.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Command("cctv")]
        private void cmdCCTV(IPlayer iplayer, string command, string[] args)
        {
            if (!(iplayer.Object is BasePlayer)) return;
            if (!iplayer.HasPermission(permCCTV)) { Message(iplayer, "notauthorized"); return; }
            BasePlayer player = iplayer.Object as BasePlayer;

            List<ComputerStation> stations = new List<ComputerStation>();
            Vis.Entities(player.transform.position, 2f, stations);
            bool foundS = false;

            string cmd = null;
            if (args.Length == 0) cmd = null;
            else cmd = args[0];

            switch(cmd)
            {
                case "clear":
                    foreach (ComputerStation station in stations)
                    {
                        station.controlBookmarks.Clear();
                        station.SendControlBookmarks(player);
                    }
                    break;
//                case "rename":
//                    {
//                        List<CCTV_RC> cameras = new List<CCTV_RC>();
//                        Vis.Entities(player.transform.position, 2f, cameras);
//                        foreach (var cam in cameras)
//                        {
//                            cam.UpdateIdentifier(args[1]);
//                            break;
//                        }
//                    }
//                    break;
                case "add":
                    if (args.Length > 1)
                    {
                        List<string> cameras = new List<string>();
                        if (args[1].Contains(","))
                        {
                            string newargs = string.Concat(args);
                            newargs = newargs.Replace("add", "");
                            cameras = newargs.Split(',').ToList();
                        }
                        else
                        {
                            cameras.Add(args[1].Trim());
                        }

                        foreach (ComputerStation station in stations)
                        {
                            foreach (string cname in cameras)
                            {
                                string cam = cname.Trim();
                                if (station.controlBookmarks.Contains(cam))
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
                    foreach (ComputerStation station in stations)
                    {
                        foundS = true;
                        Message(iplayer, "foundStation");
                        List<Drone> drones = new List<Drone>();

                        float range = configData.userRange;
                        if (configData.userMapWide || iplayer.HasPermission(permCCTVAdmin)) range = mapSize;

                        Vis.Entities(player.transform.position, range, drones, targetLayer);
                        List<string> foundCameras = new List<string>();

                        foreach (Drone camera in drones)
                        {
                            IRemoteControllable realcam = camera as IRemoteControllable;
                            if (realcam == null) continue;
                            BaseEntity ent = realcam.GetEnt();
                            if (ent == null) continue;
                            string cname = realcam.GetIdentifier();
                            if (cname == null) continue;
                            if (foundCameras.Contains(cname)) continue;
                            if (configData.debug) Puts($"Found drone {cname} at {ent.transform.position.ToString()}.");
                            if (ent.OwnerID.ToString() == iplayer.Id || IsFriend(player.userID, ent.OwnerID) || iplayer.HasPermission(permCCTVAdmin))
                            {
                                if (station.controlBookmarks.Contains(cname))
                                {
                                    Message(iplayer, "cameraexists", cname);
                                    continue;
                                }
                                foundCameras.Add(cname);

                                string displayName = Lang("unknown");
                                if ((ent.OwnerID == 0) && (configData.blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
                                {
                                    if (configData.debug) Puts($"Disabling server-owned drone {cname} {ent.OwnerID.ToString()}.");
                                    continue;
                                }
                                else if (ent.OwnerID == 0)
                                {
                                    displayName = Lang("server");
                                }
                                else
                                {
                                    BasePlayer pl = BasePlayer.Find(ent.OwnerID.ToString());
                                    if (pl != null) displayName = pl.displayName;
                                }

                                Message(iplayer, "foundDrone", cname, displayName);
                                AddCamera(player, station, cname);
                            }
                        }
                        break;
                    }
                    if (!foundS)
                    {
                        Message(iplayer, "noStation");
                    }
                    break;
                default:
                    foreach (ComputerStation station in stations)
                    {
                        foundS = true;
                        Message(iplayer, "foundStation");
                        List<CCTV_RC> cameras = new List<CCTV_RC>();

                        float range = configData.userRange;
                        if (configData.userMapWide || iplayer.HasPermission(permCCTVAdmin)) range = mapSize;

                        Vis.Entities(player.transform.position, range, cameras, targetLayer);
                        List<string> foundCameras = new List<string>();
                        if (configData.debug) Puts($"Searching for cameras over a {range.ToString()}m radius.");
                        foreach (CCTV_RC camera in cameras)
                        {
                            IRemoteControllable realcam = camera as IRemoteControllable;
                            if (realcam == null) continue;
                            BaseEntity ent = realcam.GetEnt();
                            if (ent == null) continue;
                            string cname = realcam.GetIdentifier();
                            if (cname == null) continue;
                            if (foundCameras.Contains(cname)) continue;
                            if (configData.debug) Puts($"Found camera {cname} at {ent.transform.position.ToString()}.");
                            if (ent.OwnerID.ToString() == iplayer.Id || IsFriend(player.userID, ent.OwnerID) || iplayer.HasPermission(permCCTVAdmin))
                            {
                                if (station.controlBookmarks.Contains(cname))
                                {
                                    Message(iplayer, "cameraexists", cname);
                                    continue;
                                }
                                foundCameras.Add(cname);

                                string displayName = Lang("unknown");
                                if ((ent.OwnerID == 0) && (configData.blockServerCams && !player.IPlayer.HasPermission(permCCTVAdmin)))
                                {
                                    if (configData.debug) Puts($"Disabling server-owned camera {cname} {ent.OwnerID.ToString()}.");
                                    continue;
                                }
                                else if (ent.OwnerID == 0)
                                {
                                    displayName = Lang("server");
                                }
                                else
                                {
                                    BasePlayer pl = BasePlayer.Find(ent.OwnerID.ToString());
                                    if (pl != null) displayName = pl.displayName;
                                }

                                Message(iplayer, "foundCamera", cname, displayName);
                                AddCamera(player, station, cname);
                            }
                        }
                        break;
                    }
                    if (!foundS)
                    {
                        Message(iplayer, "noStation");
                    }
                    break;
            }
        }

        [Command("cctvlist")]
        private void cmdCCTVList(IPlayer iplayer, string command, string[] args)
        {
            List<CCTV_RC> cameras = new List<CCTV_RC>();
            List<Drone> drones = new List<Drone>();
            List<string> foundCameras = new List<string>();
            string msg = null;

            Vis.Entities(Vector3.zero, mapSize, cameras);
            Vis.Entities(Vector3.zero, mapSize, drones);

            msg += Lang("foundCameras") + "\n";
            foreach (CCTV_RC camera in cameras)
            {
                IRemoteControllable realcam = camera as IRemoteControllable;
                if (realcam == null) continue;
                Transform loc = realcam.GetEyes();
                BaseEntity ent = realcam.GetEnt();
                if (ent == null) continue;
                string cname = realcam.GetIdentifier();
                if (cname == null) continue;
                if (foundCameras.Contains(cname)) continue;
                foundCameras.Add(cname);
                msg += cname + " @ " + loc.position.ToString() + Lang("ownedby") + ent.OwnerID.ToString() + "\n";
            }
            msg += Lang("foundDrones") + "\n";
            foreach (Drone camera in drones)
            {
                IRemoteControllable realcam = camera as IRemoteControllable;
                if (realcam == null) continue;
                Transform loc = realcam.GetEyes();
                BaseEntity ent = realcam.GetEnt();
                if (ent == null) continue;
                string cname = realcam.GetIdentifier();
                if (cname == null) continue;
                if (foundCameras.Contains(cname)) continue;
                foundCameras.Add(cname);
                msg += cname + " @ " + loc.position.ToString() + Lang("ownedby") + ent.OwnerID.ToString() + "\n";
            }

            if (!(iplayer.Object is BasePlayer))
            {
                Puts(msg);
            }
            else
            {
                Message(iplayer, msg);
            }
        }

        private void AddCamera(BasePlayer basePlayer, ComputerStation station, string str)
        {
            if (configData.debug) Puts($"Trying to add camera {str}");
            uint d = 0;
            BaseNetworkable baseNetworkable;
            IRemoteControllable component;
            foreach (IRemoteControllable allControllable in RemoteControlEntity.allControllables)
            {
                string curr = allControllable.GetIdentifier();
                if (allControllable == null)
                {
                    if (configData.debug) Puts($"  skipping null camera {curr}");
                    continue;
                }
                if (curr != str) continue;

                if (allControllable.GetEnt() != null)
                {
                    d = allControllable.GetEnt().net.ID;
                    baseNetworkable = BaseNetworkable.serverEntities.Find(d);

                    if (baseNetworkable == null)
                    {
                        if (configData.debug) Puts("  baseNetworkable null");
                        return;
                    }
                    component = baseNetworkable.GetComponent<IRemoteControllable>();
                    if (component == null)
                    {
                        if (configData.debug) Puts("  component null");
                        return;
                    }
                    if (str == component.GetIdentifier())
                    {
                        if (configData.debug) Puts("  adding to station...");
                        station.controlBookmarks.Add(str);
                    }
                    station.SendControlBookmarks(basePlayer);
                    return;
                }
                else
                {
                    if (configData.debug) Puts("Computer station added bookmark with missing ent, likely a static CCTV (wipe the server)");
                    return;
                }
            }
            if (configData.debug) Puts($"  {str} cannot be controlled.  Check power!");
        }

        // playerid = active player, ownerid = owner of camera, who may be offline
        private bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (configData.useFriends && Friends != null)
            {
                object fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
                    return true;
                }
            }
            if (configData.useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan  = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan == ownerclan && playerclan != null && ownerclan != null)
                {
                    return true;
                }
            }
            if (configData.useTeams)
            {
                BasePlayer player = BasePlayer.FindByID(playerid);
                if (player.currentTeam != 0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (playerTeam == null) return false;
                    if (playerTeam.members.Contains(ownerid))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;

            if (input.IsDown(BUTTON.FORWARD) || input.IsDown(BUTTON.BACKWARD) || input.IsDown(BUTTON.LEFT) || input.IsDown(BUTTON.RIGHT))
            {
                try
                {
                    ComputerStation activeCamera = player.GetMounted().GetComponentInParent<ComputerStation>();
                    if (activeCamera != null)
                    {
                        CCTV_RC cctv = activeCamera.currentlyControllingEnt.Get(true).GetComponent<CCTV_RC>();
                        if (cctv == null) return;
                        if (cctv.IsStatic() && !player.IPlayer.HasPermission(permCCTVAdmin)) return;
                        cctv.hasPTZ = true;

                        float x = input.IsDown(BUTTON.RIGHT) ? 1f : (input.IsDown(BUTTON.LEFT) ? -1f : 0f);
                        float y = input.IsDown(BUTTON.FORWARD) ? 1f : (input.IsDown(BUTTON.BACKWARD) ? -1f : 0f);

                        if (configData.debug)
                        {
                            string lr = input.IsDown(BUTTON.RIGHT) ? "right" : (input.IsDown(BUTTON.LEFT) ? "left" : "");
                            string ud = input.IsDown(BUTTON.FORWARD) ? "up" : (input.IsDown(BUTTON.BACKWARD) ? "down" : "");
                            string udlr = ud + lr;
                            Puts($"Trying to move camera {udlr}.");
                        }
                        float speed = 0.1f;
                        if (input.IsDown(BUTTON.SPRINT)) speed *= 3;

                        InputState inputState = new InputState();
                        inputState.current.mouseDelta.y = y * speed;
                        inputState.current.mouseDelta.x = x * speed;

                        CameraViewerId cv = new CameraViewerId(player.userID, 0);
                        cctv.UserInput(inputState, cv);

                        if (configData.playSound)
                        {
                            Effect effect = new Effect(moveSound, new Vector3(0, 0, 0), Vector3.forward);
                            if (configData.playAtCamera)
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

        public class MovingLight : FacepunchBehaviour
        {
            public CCTV_RC camera;
            public SimpleLight light;

            public void Awake()
            {
                camera = GetComponentInParent<CCTV_RC>();
                light = GetComponentInParent<SimpleLight>();
            }

            public void Update()
            {
                if (light == null) return;
                if (camera == null) return;
                light.transform.localEulerAngles = new Vector3(camera.pivotOrigin.rotation.x, camera.pivotOrigin.rotation.y, camera.pivotOrigin.rotation.z);
                //sl.transform.localEulerAngles = new Vector3(0, 0, 0);

                //Vector3 vector3 = Vector3Ex.Direction(cctv.transform.position, cctv.yaw.transform.position);
                //vector3 = cctv.transform.InverseTransformDirection(vector3);
                //sl.transform.localRotation = Quaternion.Euler(vector3);
                light.SendNetworkUpdateImmediate(true);// BasePlayer.NetworkQueue.Update);
            }
        }
    }
}
