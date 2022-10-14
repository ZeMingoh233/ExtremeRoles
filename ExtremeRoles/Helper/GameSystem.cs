﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Hazel;
using UnityEngine;

using Newtonsoft.Json.Linq;

using ExtremeRoles.Extension.Json;
using ExtremeRoles.Compat.Interface;
using ExtremeRoles.Roles;
using ExtremeRoles.Roles.API.Extension.State;
using ExtremeRoles.Performance;

namespace ExtremeRoles.Helper
{
    public static class GameSystem
    {
        public const string SkeldAdmin = "SkeldShip(Clone)/Admin/Ground/admin_bridge/MapRoomConsole";
        public const string SkeldSecurity = "SkeldShip(Clone)/Security/Ground/map_surveillance/SurvConsole";

        public const string MiraHqAdmin = "MiraShip(Clone)/Admin/MapTable/AdminMapConsole";
        public const string MiraHqSecurity = "MiraShip(Clone)/Comms/comms-top/SurvLogConsole";

        public const string PolusAdmin1 = "PolusShip(Clone)/Admin/mapTable/panel_map";
        public const string PolusAdmin2 = "PolusShip(Clone)/Admin/mapTable/panel_map (1)";
        public const string PolusSecurity = "PolusShip(Clone)/Electrical/Surv_Panel";
        public const string PolusVital = "PolusShip(Clone)/Office/panel_vitals";

        public const string AirShipSecurity = "Airship(Clone)/Security/task_cams";
        public const string AirShipVital = "Airship(Clone)/Medbay/panel_vitals";
        public const string AirShipArchiveAdmin = "Airship(Clone)/Records/records_admin_map";
        public const string AirShipCockpitAdmin = "Airship(Clone)/Cockpit/panel_cockpit_map";

        private const string airShipSpawnJson =
            "ExtremeRoles.Resources.JsonData.AirShipSpawnPoint.json";
        private const string airShipKey = "VanillaRandomSpawn";

        public static HashSet<TaskTypes> SaboTask = new HashSet<TaskTypes>()
        {
            TaskTypes.FixLights,
            TaskTypes.RestoreOxy,
            TaskTypes.ResetReactor,
            TaskTypes.ResetSeismic,
            TaskTypes.FixComms,
            TaskTypes.StopCharles
        };

        private static HashSet<TaskTypes> ignoreTask = new HashSet<TaskTypes>()
        {
            TaskTypes.FixWiring,
            TaskTypes.VentCleaning,
        };

        public static bool IsLobby
        {
            get
            {
                return (
                    AmongUsClient.Instance.GameState !=
                    InnerNet.InnerNetClient.GameStates.Started
                );
            }
        }
        public static bool IsFreePlay
        {
            get
            {
                return AmongUsClient.Instance.GameMode == GameModes.FreePlay;
            }
        }

        public static void DisableMapModule(string mapModuleName)
        {
            GameObject obj = GameObject.Find(mapModuleName);
            if (obj != null)
            {
                disableCollider<Collider2D>(obj);
                disableCollider<PolygonCollider2D>(obj);
                disableCollider<BoxCollider2D>(obj);
                disableCollider<CircleCollider2D>(obj);
            }
        }

        public static (int, int) GetTaskInfo(
            GameData.PlayerInfo playerInfo)
        {
            int TotalTasks = 0;
            int CompletedTasks = 0;
            if (!(playerInfo.Disconnected) &&
                 (playerInfo.Tasks != null) &&
                 (playerInfo.Object) &&
                 (playerInfo.Role) &&
                 (playerInfo.Role.TasksCountTowardProgress) &&
                 (PlayerControl.GameOptions.GhostsDoTasks || !playerInfo.IsDead) &&
                  ExtremeRoleManager.GameRole[playerInfo.PlayerId].HasTask()
                )
            {

                for (int j = 0; j < playerInfo.Tasks.Count; ++j)
                {
                    ++TotalTasks;
                    if (playerInfo.Tasks[j].Complete)
                    {
                        ++CompletedTasks;
                    }
                }
            }
            return (CompletedTasks, TotalTasks);
        }

        public static int GetRandomCommonTaskId()
        {
            if (CachedShipStatus.Instance == null) { return byte.MaxValue; }

            List<int> taskIndex = getTaskIndex(
                CachedShipStatus.Instance.CommonTasks);

            int index = RandomGenerator.Instance.Next(taskIndex.Count);

            return (byte)taskIndex[index];
        }

        public static int GetRandomLongTask()
        {
            if (CachedShipStatus.Instance == null) { return byte.MaxValue; }

            List<int> taskIndex = getTaskIndex(
                CachedShipStatus.Instance.LongTasks);

            int index = RandomGenerator.Instance.Next(taskIndex.Count);

            return taskIndex[index];
        }

        public static int GetRandomNormalTaskId()
        {
            if (CachedShipStatus.Instance == null) { return byte.MaxValue; }

            List<int> taskIndex = getTaskIndex(
                CachedShipStatus.Instance.NormalTasks);

            int index = RandomGenerator.Instance.Next(taskIndex.Count);

            return taskIndex[index];
        }

        public static Sprite GetAdminButtonImage()
        {
            var imageDict = FastDestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings;
            switch (PlayerControl.GameOptions.MapId)
            {
                case 0:
                case 3:
                    return imageDict[ImageNames.AdminMapButton].Image;
                case 1:
                    return imageDict[ImageNames.MIRAAdminButton].Image;
                case 2:
                    return imageDict[ImageNames.PolusAdminButton].Image;
                default:
                    return imageDict[ImageNames.AirshipAdminButton].Image;
            }
        }

        public static Sprite GetSecurityImage()
        {
            var imageDict = FastDestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings;
            switch (PlayerControl.GameOptions.MapId)
            {
                case 1:
                    return imageDict[ImageNames.DoorLogsButton].Image;
                default:
                    return imageDict[ImageNames.CamsButton].Image;
            }
        }
        public static Sprite GetVitalImage() => 
            FastDestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings[
                ImageNames.VitalsButton].Image;

        public static SystemConsole GetSecuritySystemConsole()
        {
            SystemConsole watchConsole;
            if (ExtremeRolesPlugin.Compat.IsModMap)
            {
                watchConsole = ExtremeRolesPlugin.Compat.ModMap.GetSystemConsole(
                    SystemConsoleType.SecurityCamera);
            }
            else
            {
                watchConsole = getVanillaSecurityConsole();
            }
            return watchConsole;
        }
        public static SystemConsole GetVitalSystemConsole()
        {
            SystemConsole vitalConsole;
            if (ExtremeRolesPlugin.Compat.IsModMap)
            {
                vitalConsole = ExtremeRolesPlugin.Compat.ModMap.GetSystemConsole(
                    SystemConsoleType.Vital);
            }
            else
            {
                vitalConsole = getVanillaVitalConsole();
            }
            return vitalConsole;
        }

        public static void SetTask(
            GameData.PlayerInfo playerInfo,
            int taskIndex)
        {
            NormalPlayerTask task = CachedShipStatus.Instance.GetTaskById((byte)taskIndex);

            PlayerControl player = playerInfo.Object;

            int index = playerInfo.Tasks.Count;
            playerInfo.Tasks.Add(new GameData.TaskInfo((byte)taskIndex, (uint)index));
            playerInfo.Tasks[index].Id = (uint)index;

            task.Id = (uint)index;
            task.Owner = player;
            task.Initialize();

            player.myTasks.Add(task);
            player.SetDirtyBit(1U << (int)player.PlayerId);
        }

        public static bool SetPlayerNewTask(
            ref PlayerControl player,
            byte taskId, uint gameControlTaskId)
        {
            NormalPlayerTask addTask = CachedShipStatus.Instance.GetTaskById(taskId);
            if (addTask == null) { return false; }

            for (int i = 0; i < player.myTasks.Count; ++i)
            {
                var textTask = player.myTasks[i].gameObject.GetComponent<ImportantTextTask>();
                if (textTask != null) { continue; }

                if (SaboTask.Contains(player.myTasks[i].TaskType)) { continue; }
                if (ExtremeRolesPlugin.Compat.IsModMap)
                {
                    if (ExtremeRolesPlugin.Compat.ModMap.IsCustomSabotageTask(
                            player.myTasks[i].TaskType)) { continue; }
                }

                if (player.myTasks[i].IsComplete)
                {
                    NormalPlayerTask normalPlayerTask = UnityEngine.Object.Instantiate(
                        addTask, player.transform);
                    normalPlayerTask.Id = gameControlTaskId;
                    normalPlayerTask.Owner = player;
                    normalPlayerTask.Initialize();

                    var removeTask = player.myTasks[i];
                    player.myTasks[i] = normalPlayerTask;

                    removeTask.OnRemove();
                    UnityEngine.Object.Destroy(
                        removeTask.gameObject);
                    return true;
                }
            }
            return false;
        }

        public static List<Vector2> GetAirShipRandomSpawn()
        {
            JObject json = JsonParser.GetJObjectFromAssembly(airShipSpawnJson);
            JArray airShipSpawn = json.Get<JArray>(airShipKey);

            List<Vector2> result = new List<Vector2>();

            for (int i = 0; i < airShipSpawn.Count; ++i)
            {
                JArray pos = airShipSpawn.Get<JArray>(i);
                result.Add(new Vector2((float)pos[0], (float)pos[1]));
            }

            return result;
        }

        public static void ShareVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                 CachedPlayerControl.LocalPlayer.PlayerControl.NetId,
                (byte)RPCOperator.Command.ShareVersion,
                Hazel.SendOption.Reliable, -1);
            writer.Write(ver.Major);
            writer.Write(ver.Minor);
            writer.Write(ver.Build);
            writer.Write(ver.Revision);
            writer.WritePacked(AmongUsClient.Instance.ClientId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            RPCOperator.AddVersionData(
                ver.Major, ver.Minor,
                ver.Build, ver.Revision,
                AmongUsClient.Instance.ClientId);
        }

        private static void disableCollider<T>(GameObject obj) where T : Collider2D
        {
            T comp = obj.GetComponent<T>();
            if (comp != null)
            {
                comp.enabled = false;
            }
        }

        private static List<int> getTaskIndex(
            NormalPlayerTask[] tasks)
        {
            List<int> index = new List<int>();
            for (int i = 0; i < tasks.Length; ++i)
            {
                if (!ignoreTask.Contains(tasks[i].TaskType))
                {
                    index.Add(tasks[i].Index);
                }
            }

            return index;
        }

        private static SystemConsole getVanillaSecurityConsole()
        {
            // 0 = Skeld
            // 1 = Mira HQ
            // 2 = Polus
            // 3 = Dleks - deactivated
            // 4 = Airship
            var systemConsoleArray = UnityEngine.Object.FindObjectsOfType<SystemConsole>();
            switch (PlayerControl.GameOptions.MapId)
            {
                case 0:
                case 3:
                    return systemConsoleArray.FirstOrDefault(
                        x => x.gameObject.name.Contains("SurvConsole"));
                case 1:
                    return systemConsoleArray.FirstOrDefault(
                        x => x.gameObject.name.Contains("SurvLogConsole"));
                case 2:
                    return systemConsoleArray.FirstOrDefault(
                        x => x.gameObject.name.Contains("Surv_Panel"));
                case 4:
                    return systemConsoleArray.FirstOrDefault(
                        x => x.gameObject.name.Contains("task_cams"));
                default:
                    return null;
            }
        }

        private static SystemConsole getVanillaVitalConsole()
        {
            // 0 = Skeld
            // 1 = Mira HQ
            // 2 = Polus
            // 3 = Dleks - deactivated
            // 4 = Airship
            var systemConsoleArray = UnityEngine.Object.FindObjectsOfType<SystemConsole>();
            switch (PlayerControl.GameOptions.MapId)
            {
                case 0:
                case 1:
                case 3:
                    return null;
                case 2:
                case 4:
                    return systemConsoleArray.FirstOrDefault(
                        x => x.gameObject.name.Contains("panel_vitals"));
                default:
                    return null;
            }
        }
    }
}
