﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BepInEx;
using UnityEngine;
using Il2CppInterop.Runtime;
using AmongUs.GameOptions;
using HarmonyLib;

using ExtremeRoles.Compat.Interface;
using ExtremeRoles.Helper;
using ExtremeRoles.Performance;
using ExtremeRoles.Performance.Il2Cpp;

#nullable enable

namespace ExtremeRoles.Compat.Mods;

public sealed class SubmergedMap : CompatModBase, IMultiFloorModMap
{
    public const string Guid = "Submerged";

    public ShipStatus.MapType MapType => (ShipStatus.MapType)5;
    public bool CanPlaceCamera => false;
    public bool IsCustomCalculateLightRadius => true;

    public TaskTypes RetrieveOxygenMask;

	private Type? elevatorMover;

    private Type? submarineOxygenSystem;
    private PropertyInfo? submarineOxygenSystemInstanceGetter;
    private MethodInfo? submarineOxygenSystemRepairDamageMethod;

    private MethodInfo? getFloorHandlerInfo;
    private MethodInfo? rpcRequestChangeFloorMethod;
    private FieldInfo? onUpperField;

    private FieldInfo? inTransitionField;

    private Type? submarineStatusType;
    private MethodInfo? calculateLightRadiusMethod;
    private MonoBehaviour? submarineStatus;

    private float crewVision;
    private float impostorVision;

    public SubmergedMap(PluginInfo plugin) : base(Guid, plugin)
    {
        // カスタムサボのタスクタイプ取得
        Type taskType = this.ClassType.First(
            t => t.Name == "CustomTaskTypes");
        var retrieveOxigenMaskField = AccessTools.Field(taskType, "RetrieveOxygenMask");
		object? taskTypeObj = retrieveOxigenMaskField.GetValue(null);
		var retrieveOxigenMaskTaskTypeField = AccessTools.Field(taskTypeObj?.GetType(), "taskType");

		object? oxygenTaskType = retrieveOxigenMaskTaskTypeField.GetValue(taskTypeObj);
		if (oxygenTaskType == null) { return; }
		this.RetrieveOxygenMask = (TaskTypes)oxygenTaskType;

        // サブマージドの酸素妨害の修理用
        this.submarineOxygenSystemInstanceGetter = AccessTools.Property(
			this.submarineOxygenSystem, "Instance");
		this.submarineOxygenSystemRepairDamageMethod = AccessTools.Method(
			this.submarineOxygenSystem, "RepairDamage");

		// サブマージドのカスタムMonoを取ってくる
		this.elevatorMover = this.ClassType.First(t => t.Name == "ElevatorMover");

		// フロアを変える用
		Type floorHandlerType = this.ClassType.First(t => t.Name == "FloorHandler");
		this.getFloorHandlerInfo = AccessTools.Method(
            floorHandlerType, "GetFloorHandler", new Type[] { typeof(PlayerControl) });
		this.rpcRequestChangeFloorMethod = AccessTools.Method(
            floorHandlerType, "RpcRequestChangeFloor");
		this.onUpperField = AccessTools.Field(floorHandlerType, "onUpper");

		this.submarineStatusType = ClassType.First(
            t => t.Name == "SubmarineStatus");
		this.calculateLightRadiusMethod = AccessTools.Method(
            submarineStatusType, "CalculateLightRadius");


        Type ventMoveToVentPatchType = ClassType.First(t => t.Name == "VentMoveToVentPatch");
		this.inTransitionField = AccessTools.Field(ventMoveToVentPatchType, "inTransition");
    }
    public void Awake(ShipStatus map)
    {
        Patches.HudManagerUpdatePatchPostfixPatch.ButtonTriggerReset();

        var component = map.GetComponent(Il2CppType.From(submarineStatusType));
        if (component)
        {
            submarineStatus = component.TryCast(
                submarineStatusType) as MonoBehaviour;
        }

        // 毎回毎回取得すると重いのでキャッシュ化
        var curOption = GameOptionsManager.Instance.CurrentGameOptions;
        crewVision = curOption.GetFloat(FloatOptionNames.CrewLightMod);
        impostorVision = curOption.GetFloat(FloatOptionNames.ImpostorLightMod);
    }

    public void Destroy()
    {
        submarineStatus = null;

        // バグってるかもしれないのでもとに戻しとく
        var curOption = GameOptionsManager.Instance.CurrentGameOptions;
        curOption.SetFloat(FloatOptionNames.CrewLightMod, crewVision);
        curOption.SetFloat(FloatOptionNames.ImpostorLightMod, impostorVision);
    }

    public float CalculateLightRadius(GameData.PlayerInfo player, bool neutral, bool neutralImpostor)
    {
		object? value = calculateLightRadiusMethod?.Invoke(
			submarineStatus, new object?[] { null, neutral, neutralImpostor });
		return value != null ? (float)value : 1.0f;
    }

    public float CalculateLightRadius(
        GameData.PlayerInfo player, float visionMod, bool applayVisionEffects = true)
    {
        // サブマージドの視界計算のロジックは「クルーだと停電効果受ける、インポスターだと受けないので」
        // 1. まずはデフォルトの視界をMOD側で用意した視界の広さにリプレイス
        // 2. 視界効果を受けるかをインポスターかどうかで渡して計算
        // 3. 元の視界の広さに戻す

        var curOption = GameOptionsManager.Instance.CurrentGameOptions;
        curOption.SetFloat(FloatOptionNames.CrewLightMod, visionMod);
        curOption.SetFloat(FloatOptionNames.ImpostorLightMod, visionMod);

        float result = CalculateLightRadius(player, true, !applayVisionEffects);

        curOption.SetFloat(FloatOptionNames.CrewLightMod, crewVision);
        curOption.SetFloat(FloatOptionNames.ImpostorLightMod, impostorVision);

        return result;
    }

    public int GetLocalPlayerFloor() => GetFloor(CachedPlayerControl.LocalPlayer);
    public int GetFloor(PlayerControl player)
    {
        MonoBehaviour? floorHandler = getFloorHandler(player);
        if (floorHandler == null) { return int.MaxValue; }
		object? valueObj = onUpperField?.GetValue(floorHandler);
        return valueObj != null && (bool)valueObj ? 1 : 0;
    }
    public void ChangeLocalPlayerFloor(int floor)
    {
        ChangeFloor(CachedPlayerControl.LocalPlayer, floor);
    }
    public void ChangeFloor(PlayerControl player, int floor)
    {
        if (floor > 1) { return; }
        MonoBehaviour? floorHandler = getFloorHandler(player);
        if (floorHandler == null) { return; }
        rpcRequestChangeFloorMethod?.Invoke(floorHandler, new object[] { floor == 1 });
    }

    public Console? GetConsole(TaskTypes task)
    {
        var console = UnityEngine.Object.FindObjectsOfType<Console>();
        switch (task)
        {
            case TaskTypes.FixLights:
                return console.FirstOrDefault(
                    x => x.gameObject.name.Contains("LightsConsole"));
            case TaskTypes.StopCharles:
                List<Console> res = new List<Console>(2);
                Console? leftConsole = console.FirstOrDefault(
                    x => x.gameObject.name.Contains("BallastConsole_1"));
                if (leftConsole != null)
                {
                    res.Add(leftConsole);
                }
                Console? rightConsole = console.FirstOrDefault(
                    x => x.gameObject.name.Contains("BallastConsole_2"));
                if (rightConsole != null)
                {
                    res.Add(rightConsole);
                }
                return res[RandomGenerator.Instance.Next(res.Count)];
            default:
                return null;
        }
    }

    public List<Vector2> GetSpawnPos(byte playerId)
    {
        ShipStatus ship = CachedShipStatus.Instance;
        Vector2 baseVec = Vector2.up;
        baseVec = baseVec.Rotate(
            (float)(playerId - 1) * (360f / (float)GameData.Instance.PlayerCount));
        Vector2 defaultSpawn = ship.InitialSpawnCenter +
            baseVec * ship.SpawnRadius + new Vector2(0f, 0.3636f);

		List<Vector2> spawnPos = new List<Vector2>()
		{
			defaultSpawn, defaultSpawn + new Vector2(0.0f, 48.119f)
		};

        return spawnPos;
    }

    public HashSet<string> GetSystemObjectName(SystemConsoleType sysConsole)
    {
        switch (sysConsole)
        {
            case SystemConsoleType.Admin:
                return new HashSet<string>()
                {
                    "Submerged(Clone)/TopFloor/Adm-Obsv-Loun-MR/TaskConsoles/console-adm-admintable",
                    "Submerged(Clone)/TopFloor/Adm-Obsv-Loun-MR/TaskConsoles/console-adm-admintable (1)",
                };
            case SystemConsoleType.Vital:
                return new HashSet<string>()
                {
                    "Submerged(Clone)/panel_vitals(Clone)",
                };
            case SystemConsoleType.SecurityCamera:
                return new HashSet<string>()
                {
                    "Submerged(Clone)/BottomFloor/Engines-Security/TaskConsoles/SecurityConsole",
                };
            default:
                return new HashSet<string>();
        }
    }

    public SystemConsole? GetSystemConsole(SystemConsoleType sysConsole)
    {
        var systemConsoleArray = UnityEngine.Object.FindObjectsOfType<SystemConsole>();
        switch (sysConsole)
        {
            case SystemConsoleType.SecurityCamera:
                return systemConsoleArray.FirstOrDefault(
                    x => x.gameObject.name.Contains("SecurityConsole"));
            case SystemConsoleType.Vital:
                return systemConsoleArray.FirstOrDefault(
                    x => x.gameObject.name.Contains("panel_vitals(Clone)"));
            case SystemConsoleType.EmergencyButton:
                return systemConsoleArray.FirstOrDefault(
                    x => x.gameObject.name.Contains("console-mr-callmeeting"));
            default:
                return null;
        }
    }

    public bool IsCustomSabotageNow()
    {
        foreach (NormalPlayerTask task in PlayerControl.LocalPlayer.myTasks.GetFastEnumerator())
        {
            if (task != null && IsCustomSabotageTask(task.TaskType))
            {
                return true;
            }
        }
        return false;
    }

    public bool IsCustomSabotageTask(TaskTypes saboTask) => saboTask == this.RetrieveOxygenMask;

    public bool IsCustomVentUse(Vent vent)
    {
        switch (vent.Id)
        {
            case 9:  // Cannot enter vent 9 (Engine Room Exit Only Vent)!
                if (CachedPlayerControl.LocalPlayer.PlayerControl.inVent)
                {
                    return false;
                }
                return true;
            case 0:
            case 14: // Lower and Upper Central
                return true;
            default:
                return false;
        }
    }

    public (float, bool, bool) IsCustomVentUseResult(
        Vent vent, GameData.PlayerInfo player, bool isVentUse)
    {
		object? valueObj = inTransitionField?.GetValue(null);

		if (valueObj == null || (bool)valueObj)
        {
            return (float.MaxValue, false, false);
        }
        switch (vent.Id)
        {
            case 0:
            case 14: // Lower and Upper Central
                float result = float.MaxValue;
                bool couldUse = isVentUse && !player.IsDead && (player.Object.CanMove || player.Object.inVent);
                bool canUse = couldUse;
                if (canUse)
                {
                    Vector3 center = player.Object.Collider.bounds.center;
                    Vector3 position = vent.transform.position;
                    result = Vector2.Distance(center, position);
                    canUse &= result <= vent.UsableDistance;
                }
                return (result, canUse, couldUse);
            default:
                return (float.MaxValue, false, false);
        }
    }

    public void RpcRepairCustomSabotage()
    {
        using (var caller = RPCOperator.CreateCaller(
            RPCOperator.Command.IntegrateModCall))
        {
            caller.WriteByte(IMapMod.RpcCallType);
            caller.WriteByte((byte)MapRpcCall.RepairAllSabo);
        }
        RepairCustomSabotage();
    }

    public void RpcRepairCustomSabotage(TaskTypes saboTask)
    {
        using (var caller = RPCOperator.CreateCaller(
            RPCOperator.Command.IntegrateModCall))
        {
            caller.WriteByte(IMapMod.RpcCallType);
            caller.WriteByte((byte)MapRpcCall.RepairCustomSaboType);
            caller.WriteInt((int)saboTask);
        }
        RepairCustomSabotage(saboTask);
    }

    public void RepairCustomSabotage()
    {
        RepairCustomSabotage(this.RetrieveOxygenMask);
    }

    public void RepairCustomSabotage(TaskTypes saboTask)
    {
        if (saboTask == this.RetrieveOxygenMask)
        {
            CachedShipStatus.Instance.RpcRepairSystem((SystemTypes)130, 64);
            submarineOxygenSystemRepairDamageMethod?.Invoke(
                submarineOxygenSystemInstanceGetter?.GetValue(null),
                new object[] { PlayerControl.LocalPlayer, (byte)64 });
        }
    }
    public void AddCustomComponent(
        GameObject addObject, CustomMonoBehaviourType customMonoType)
    {
        switch (customMonoType)
        {
            case CustomMonoBehaviourType.MovableFloorBehaviour:
				addObject.AddComponent(
					Il2CppType.From(this.elevatorMover)).TryCast<MonoBehaviour>();
				break;
            default:
                break;

        }
    }

    public void SetUpNewCamera(SurvCamera camera)
    {
        var fixConsole = camera.transform.FindChild("FixConsole");
        if (fixConsole != null)
        {
            var boxCollider = fixConsole.GetComponent<BoxCollider2D>();
            if (boxCollider != null)
            {
                UnityEngine.Object.Destroy(boxCollider);
            }
        }
    }

    protected override void PatchAll(Harmony harmony)
    {
#pragma warning disable CS8604
		Type exileCont = ClassType.First(
            t => t.Name == "SubmergedExileController");
        MethodInfo wrapUpAndSpawn = AccessTools.Method(
            exileCont, "WrapUpAndSpawn");
        ExileController? cont = null;
        MethodInfo wrapUpAndSpawnPrefix = SymbolExtensions.GetMethodInfo(
            () => Patches.SubmergedExileControllerWrapUpAndSpawnPatch.Prefix());
		MethodInfo wrapUpAndSpawnPostfix = SymbolExtensions.GetMethodInfo(
            () => Patches.SubmergedExileControllerWrapUpAndSpawnPatch.Postfix(cont));

        System.Collections.IEnumerator? enumerator = null;
        Type submarineSelectSpawn = ClassType.First(
            t => t.Name == "SubmarineSelectSpawn");
        MethodInfo prespawnStep = AccessTools.Method(
            submarineSelectSpawn, "PrespawnStep");
#pragma warning disable CS8601
		MethodInfo prespawnStepPrefix = SymbolExtensions.GetMethodInfo(
            () => Patches.SubmarineSelectSpawnPrespawnStepPatch.Prefix(ref enumerator));
#pragma warning restore CS8601
		MethodInfo onDestroy = AccessTools.Method(
            submarineSelectSpawn, "OnDestroy");
        MethodInfo onDestroyPrefix = SymbolExtensions.GetMethodInfo(
            () => Patches.SubmarineSelectOnDestroyPatch.Prefix());

        Type hudManagerUpdatePatch = ClassType.First(
            t => t.Name == "ChangeFloorButtonPatches");
        MethodInfo hudManagerUpdatePatchPostfix = AccessTools.Method(
            hudManagerUpdatePatch, "HudUpdatePatch");
        object? hudManagerUpdatePatchInstance = null;
        Patches.HudManagerUpdatePatchPostfixPatch.SetType(
            hudManagerUpdatePatch);
        MethodInfo hubManagerUpdatePatchPostfixPatch = SymbolExtensions.GetMethodInfo(
            () => Patches.HudManagerUpdatePatchPostfixPatch.Postfix(
                hudManagerUpdatePatchInstance));

        this.submarineOxygenSystem = ClassType.First(
            t => t.Name == "SubmarineOxygenSystem");
        MethodInfo submarineOxygenSystemDetoriorate = AccessTools.Method(
            submarineOxygenSystem, "Detoriorate");
        object? submarineOxygenSystemInstance = null;
        Patches.SubmarineOxygenSystemDetorioratePatch.SetType(this.submarineOxygenSystem);
        MethodInfo submarineOxygenSystemDetorioratePostfixPatch = SymbolExtensions.GetMethodInfo(
            () => Patches.SubmarineOxygenSystemDetorioratePatch.Postfix(
                submarineOxygenSystemInstance));

        Minigame? game = null;

        Type submarineSurvillanceMinigame = ClassType.First(
            t => t.Name == "SubmarineSurvillanceMinigame");
        MethodInfo submarineSurvillanceMinigameSystemUpdate = AccessTools.Method(
            submarineSurvillanceMinigame, "Update");
        Patches.SubmarineSurvillanceMinigamePatch.SetType(submarineSurvillanceMinigame);
        MethodInfo submarineSurvillanceMinigameSystemUpdatePrefixPatch = SymbolExtensions.GetMethodInfo(
            () => Patches.SubmarineSurvillanceMinigamePatch.Prefix(game));
        MethodInfo submarineSurvillanceMinigameSystemUpdatePostfixPatch = SymbolExtensions.GetMethodInfo(
            () => Patches.SubmarineSurvillanceMinigamePatch.Postfix(game));
#pragma warning restore CS8604

		// 会議終了時のリセット処理を呼び出せるように
		harmony.Patch(wrapUpAndSpawn,
            new HarmonyMethod(wrapUpAndSpawnPrefix),
            new HarmonyMethod(wrapUpAndSpawnPostfix));

        // アサシン会議発動するとスポーン画面が出ないように
        harmony.Patch(prespawnStep,
            new HarmonyMethod(prespawnStepPrefix));

        // キルクール周りが上書きされているのでそれの調整
        harmony.Patch(onDestroy,
            new HarmonyMethod(onDestroyPrefix));

        // フロアの階層変更ボタンの位置を変えるパッチ
        harmony.Patch(hudManagerUpdatePatchPostfix,
            postfix : new HarmonyMethod(hubManagerUpdatePatchPostfixPatch));

        // 酸素枯渇発動時アサシンは常にマスクを持つパッチ
        harmony.Patch(submarineOxygenSystemDetoriorate,
            postfix: new HarmonyMethod(submarineOxygenSystemDetorioratePostfixPatch));

        // サブマージドのセキュリティカメラの制限をつけるパッチ
        harmony.Patch(submarineSurvillanceMinigameSystemUpdate,
            new HarmonyMethod(submarineSurvillanceMinigameSystemUpdatePrefixPatch),
            new HarmonyMethod(submarineSurvillanceMinigameSystemUpdatePostfixPatch));
    }

	private MonoBehaviour? getFloorHandler(PlayerControl player)
	{
		object? handlerObj = this.getFloorHandlerInfo?.Invoke(null, new object[] { player });

		if (handlerObj == null) { return null; }

		return ((Component)handlerObj).TryCast<MonoBehaviour>();
	}
}