﻿using HarmonyLib;

using AmongUs.GameOptions;

using ExtremeRoles.Module.RoleAssign;
using ExtremeRoles.GhostRoles;
using ExtremeRoles.Roles;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Extension.State;
using ExtremeRoles.Roles.API.Interface;

using ExtremeRoles.Performance;

namespace ExtremeRoles.Patches.Player;

#nullable enable

// HotFix : 死人のペットが普通に見えるバグ修正、もうペットだけ消す
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class PlayerControlDiePatch
{
	public static void Postfix(
		PlayerControl __instance)
	{
		if (__instance.Data.IsDead &&
			__instance.cosmetics.CurrentPet != null)
		{
			__instance.cosmetics.currentPet.gameObject.SetActive(false);
		}
	}
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Revive))]
public static class PlayerControlRevivePatch
{
	public static void Postfix(PlayerControl __instance)
	{

		ExtremeRolesPlugin.ShipState.RemoveDeadInfo(__instance.PlayerId);

		// 消したペットをもとに戻しておく
		if (!__instance.Data.IsDead &&
			__instance.cosmetics.CurrentPet != null)
		{
			__instance.cosmetics.currentPet.gameObject.SetActive(true);
			__instance.cosmetics.currentPet.SetIdle();
		}

		if (ExtremeRoleManager.GameRole.Count == 0) { return; }
		if (!RoleAssignState.Instance.IsRoleSetUpEnd) { return; }

		var (onRevive, onReviveOther) = ExtremeRoleManager.GetInterfaceCastedRole<
			IRoleOnRevive>(__instance.PlayerId);

		onRevive?.ReviveAction(__instance);
		onReviveOther?.ReviveAction(__instance);

		SingleRoleBase role = ExtremeRoleManager.GameRole[__instance.PlayerId];

		if (!role.TryGetVanillaRoleId(out RoleTypes roleId) &&
			role.IsImpostor())
		{
			roleId = RoleTypes.Impostor;
		}

		FastDestroyableSingleton<RoleManager>.Instance.SetRole(
			__instance, roleId);

		var ghostRole = ExtremeGhostRoleManager.GetLocalPlayerGhostRole();
		if (ghostRole == null) { return; }

		if (__instance.PlayerId == CachedPlayerControl.LocalPlayer.PlayerId)
		{
			ghostRole.ResetOnMeetingStart();
		}

		lock (ExtremeGhostRoleManager.GameRole)
		{
			ExtremeGhostRoleManager.GameRole.Remove(__instance.PlayerId);
		}
	}
}
