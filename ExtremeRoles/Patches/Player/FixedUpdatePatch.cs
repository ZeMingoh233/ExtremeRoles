﻿using System.Collections.Generic;

using HarmonyLib;

using UnityEngine;
using AmongUs.GameOptions;

using ExtremeRoles.GameMode;
using ExtremeRoles.GhostRoles;
using ExtremeRoles.GhostRoles.API;
using ExtremeRoles.Module.RoleAssign;
using ExtremeRoles.Roles;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Extension.State;
using ExtremeRoles.Roles.API.Interface;
using ExtremeRoles.Performance;
using ExtremeRoles.Performance.Il2Cpp;

using PlayerHeler = ExtremeRoles.Helper.Player;

namespace ExtremeRoles.Patches.Player;

#nullable enable

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class PlayerControlFixedUpdatePatch
{
	public static void Postfix(PlayerControl __instance)
	{
		if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
		{ return; }
		if (!RoleAssignState.Instance.IsRoleSetUpEnd) { return; }
		if (ExtremeRoleManager.GameRole.Count == 0) { return; }
		if (CachedPlayerControl.LocalPlayer.PlayerId != __instance.PlayerId) { return; }

		SingleRoleBase role = ExtremeRoleManager.GetLocalPlayerRole();
		GhostRoleBase ghostRole = ExtremeGhostRoleManager.GetLocalPlayerGhostRole();

		buttonUpdate(__instance, role, ghostRole);
		refreshRoleDescription(__instance, role, ghostRole);
	}

	private static void refreshRoleDescription(
		PlayerControl player,
		SingleRoleBase playerRole,
		GhostRoleBase playerGhostRole)
	{

		var removedTask = new List<PlayerTask>();
		foreach (PlayerTask task in player.myTasks.GetFastEnumerator())
		{
			if (task == null) { return; }

			var textTask = task.gameObject.GetComponent<ImportantTextTask>();
			if (textTask != null)
			{
				removedTask.Add(task); // TextTask does not have a corresponding RoleInfo and will hence be deleted
			}
		}

		foreach (PlayerTask task in removedTask)
		{
			task.OnRemove();
			player.myTasks.Remove(task);
			UnityEngine.Object.Destroy(task.gameObject);
		}

		var importantTextTask = new GameObject("RoleTask").AddComponent<ImportantTextTask>();
		importantTextTask.transform.SetParent(player.transform, false);

		string addText = playerRole.GetImportantText();
		if (playerGhostRole != null)
		{
			addText = $"{addText}\n{playerGhostRole.GetImportantText()}";
		}
		importantTextTask.Text = addText;
		player.myTasks.Insert(0, importantTextTask);

	}
	private static void buttonUpdate(
		PlayerControl player,
		SingleRoleBase playerRole,
		GhostRoleBase playerGhostRole)
	{
		if (!player.AmOwner) { return; }

		bool enable =
			(player.IsKillTimerEnabled || player.ForceKillTimerContinue) &&
			MeetingHud.Instance == null && ExileController.Instance == null;

		killButtonUpdate(player, playerRole, enable);
		ventButtonUpdate(player, playerRole, enable);

		roleAbilityButtonUpdate(playerRole);
		ghostRoleButtonUpdate(playerGhostRole);
	}

	private static void killButtonUpdate(
		PlayerControl player,
		SingleRoleBase role, bool enable)
	{

		bool isImposter = role.IsImpostor();

		HudManager hudManager = FastDestroyableSingleton<HudManager>.Instance;

		if (role.CanKill())
		{
			if (enable && !player.Data.IsDead)
			{
				if (!isImposter)
				{
					player.SetKillTimer(player.killTimer - Time.fixedDeltaTime);
				}

				PlayerControl target = PlayerHeler.GetClosestPlayerInKillRange();

				// Logging.Debug($"TargetAlive?:{target}");

				hudManager.KillButton.SetTarget(target);
				PlayerHeler.SetPlayerOutLine(target, role.GetNameColor());
				hudManager.KillButton.Show();
				hudManager.KillButton.gameObject.SetActive(true);
			}
			else
			{
				hudManager.KillButton.SetDisabled();
			}
		}
		else if (isImposter)
		{
			hudManager.KillButton.SetDisabled();
		}
	}

	private static void roleAbilityButtonUpdate(
		SingleRoleBase role)
	{
		abilityButtonUpdate(role as IRoleAbility);

		var multiAssignRole = role as MultiAssignRoleBase;
		if (multiAssignRole != null)
		{
			abilityButtonUpdate(
				multiAssignRole.AnotherRole as IRoleAbility);
		}
	}

	private static void abilityButtonUpdate(IRoleAbility? abilityRole)
	{
		if (abilityRole != null &&
			abilityRole.Button != null)
		{
			abilityRole.Button.Update();
		}
	}

	private static void ventButtonUpdate(
		PlayerControl player, SingleRoleBase role, bool enable)
	{

		HudManager hudManager = FastDestroyableSingleton<HudManager>.Instance;

		if (!role.CanUseVent() || player.Data.IsDead)
		{
			hudManager.ImpostorVentButton.Hide();
			return;
		}

		bool ventButtonShow = enable || player.inVent;

		if (!role.TryGetVanillaRoleId(out RoleTypes roleId) ||
			roleId is RoleTypes.Shapeshifter or RoleTypes.Impostor)
		{
			if (ventButtonShow &&
				ExtremeGameModeManager.Instance.ShipOption.IsEnableImpostorVent)
			{
				hudManager.ImpostorVentButton.Show();
			}
			else
			{
				hudManager.ImpostorVentButton.SetDisabled();
			}
		}
		else if (
			roleId == RoleTypes.Engineer &&
			player.Data.Role.Role == RoleTypes.Engineer)
		{
			if (ventButtonShow)
			{
				if (!ExtremeGameModeManager.Instance.ShipOption.EngineerUseImpostorVent)
				{
					hudManager.AbilityButton.Show();
				}
				else
				{
					hudManager.ImpostorVentButton.Show();
					hudManager.AbilityButton.gameObject.SetActive(false);
				}
			}
			else
			{
				hudManager.ImpostorVentButton.SetDisabled();
				hudManager.AbilityButton.SetDisabled();
			}
		}
	}

	private static void ghostRoleButtonUpdate(GhostRoleBase playerGhostRole)
	{
		if (playerGhostRole == null) { return; }

		var abilityButton = FastDestroyableSingleton<HudManager>.Instance.AbilityButton;

		switch (Performance.CachedPlayerControl.LocalPlayer.Data.Role.Role)
		{
			case RoleTypes.Engineer:
			case RoleTypes.Scientist:
			case RoleTypes.Shapeshifter:
				abilityButton.Hide();
				break;
			case RoleTypes.CrewmateGhost:
			case RoleTypes.ImpostorGhost:
				if (playerGhostRole.IsVanillaRole() &&
					MeetingHud.Instance == null &&
					ExileController.Instance == null)
				{
					abilityButton.Show();
				}
				else
				{
					abilityButton.Hide();
				}
				break;
			default:
				break;
		}
		playerGhostRole.Button?.Update();
	}
}
