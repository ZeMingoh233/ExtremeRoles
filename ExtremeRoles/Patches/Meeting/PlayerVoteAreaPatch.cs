﻿using System;
using System.Collections;
using System.Collections.Generic;

using HarmonyLib;

using UnityEngine;
using UnityEngine.AddressableAssets;
using Innersloth.Assets;


using BepInEx.Unity.IL2CPP.Utils.Collections;

using ExtremeRoles.Extension.UnityEvent;
using ExtremeRoles.GameMode;
using ExtremeRoles.Module.RoleAssign;
using ExtremeRoles.Performance;
using ExtremeRoles.GhostRoles;
using ExtremeRoles.Roles;
using ExtremeRoles.Roles.API.Interface;

namespace ExtremeRoles.Patches.Meeting;

public static class NamePlateHelper
{
	public static bool NameplateChange = true;

	public static void UpdateNameplate(
		PlayerVoteArea pva, byte playerId = byte.MaxValue)
	{
		var playerInfo = GameData.Instance.GetPlayerById(
			playerId != byte.MaxValue ?
			playerId : pva.TargetPlayerId);
		if (playerInfo == null) { return; }

		var cache = CachedShipStatus.Instance.CosmeticsCache;
		string id = playerInfo.DefaultOutfit.NamePlateId;
		if (ClientOption.Instance.HideNamePlate.Value ||
			!cache.nameplates.TryGetValue(id, out var np) ||
			np == null)
		{
			np = cache.nameplates["nameplate_NoPlate"];
		}
		if (np == null) { return; }
		pva.Background.sprite = np.GetAsset().Image;
	}
}


[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetCosmetics))]
public static class PlayerVoteAreaCosmetics
{
	public static void Postfix(PlayerVoteArea __instance, GameData.PlayerInfo playerInfo)
	{
		NamePlateHelper.UpdateNameplate(
			__instance, playerInfo.PlayerId);
	}
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Select))]
public static class PlayerVoteAreaSelectPatch
{
	private static Dictionary<byte, UiElement> meetingAbilityButton =
		new Dictionary<byte, UiElement>();

	public static void Reset()
    {
		meetingAbilityButton.Clear();
    }

	public static bool Prefix(PlayerVoteArea __instance)
	{
		if (!RoleAssignState.Instance.IsRoleSetUpEnd) { return true; }
		if (ExtremeRoleManager.GameRole.Count == 0) { return true; }

		var state = ExtremeRolesPlugin.ShipState;
		var (buttonRole, anotherButtonRole) = ExtremeRoleManager.GetInterfaceCastedLocalRole<
			IRoleMeetingButtonAbility>();

		if (!state.AssassinMeetingTrigger)
		{
			if (buttonRole != null && anotherButtonRole != null)
            {
				return true; // TODO:Can use both role ability
            }
			else if (buttonRole != null && anotherButtonRole == null)
            {
				return meetingButtonAbility(__instance, buttonRole);
			}
			else if (buttonRole == null && anotherButtonRole != null)
			{
				return meetingButtonAbility(__instance, anotherButtonRole);
			}
			else
			{
				return true;
			}
		}

		if (CachedPlayerControl.LocalPlayer.PlayerId != ExtremeRolesPlugin.ShipState.ExiledAssassinId)
		{
			return false;
		}

		if (!__instance.Parent)
		{
			return false;
		}
		if (!__instance.voteComplete &&
			__instance.Parent.Select((int)__instance.TargetPlayerId))
		{
			__instance.Buttons.SetActive(true);
			float startPos = __instance.AnimateButtonsFromLeft ? 0.2f : 1.95f;
			__instance.StartCoroutine(
				effectsAllWrap(
					new IEnumerator[]
						{
							effectsLerpWrap(0.25f, delegate(float t)
							{
								__instance.CancelButton.transform.localPosition = Vector2.Lerp(
									Vector2.right * startPos,
									Vector2.right * 1.3f,
									Effects.ExpOut(t));
							}),
							effectsLerpWrap(0.35f, delegate(float t)
							{
								__instance.ConfirmButton.transform.localPosition = Vector2.Lerp(
									Vector2.right * startPos,
									Vector2.right * 0.65f,
									Effects.ExpOut(t));
							})
						}
					).WrapToIl2Cpp()
				);

			Il2CppSystem.Collections.Generic.List<UiElement> selectableElements = new Il2CppSystem.Collections.Generic.List<
				UiElement>();
			selectableElements.Add(__instance.CancelButton);
			selectableElements.Add(__instance.ConfirmButton);
			ControllerManager.Instance.OpenOverlayMenu(
				__instance.name,
				__instance.CancelButton,
				__instance.ConfirmButton, selectableElements, false);
		}

		return false;
	}
	private static IEnumerator effectsLerpWrap(
		float duration, Action<float> action)
	{
		for (float t = 0f; t < duration; t += Time.deltaTime)
		{
			action(t / duration);
			yield return null;
		}
		action(1f);
		yield break;
	}
	private static IEnumerator effectsAllWrap(IEnumerator[] items)
	{
		Stack<IEnumerator>[] enums = new Stack<IEnumerator>[items.Length];
		for (int i = 0; i < items.Length; i++)
		{
			enums[i] = new Stack<IEnumerator>();
			enums[i].Push(items[i]);
		}
		int num;
		for (int cap = 0; cap < 100000; cap = num)
		{
			bool flag = false;
			for (int j = 0; j < enums.Length; j++)
			{
				if (enums[j].Count > 0)
				{
					flag = true;
					IEnumerator enumerator = enums[j].Peek();
					if (enumerator.MoveNext())
					{
						if (enumerator.Current is IEnumerator)
						{
							enums[j].Push((IEnumerator)enumerator.Current);
						}
					}
					else
					{
						enums[j].Pop();
					}
				}
			}
			if (!flag)
			{
				break;
			}
			yield return null;
			num = cap + 1;
		}
		yield break;
	}

	private static bool meetingButtonAbility(
		PlayerVoteArea instance,
		IRoleMeetingButtonAbility role)
	{
		byte target = instance.TargetPlayerId;

        if (instance.AmDead)
		{
			return true;
		}
		if (!instance.Parent)
		{
			return false;
		}
		if (role.IsBlockMeetingButtonAbility(instance))
        {
			return true;
        }

		if (!instance.voteComplete &&
			instance.Parent.Select((int)target))
		{

			if (!meetingAbilityButton.TryGetValue(target, out UiElement abilitybutton) ||
				abilitybutton == null)
			{
				UiElement newAbilitybutton = GameObject.Instantiate(
					instance.CancelButton, instance.ConfirmButton.transform.parent);
				var passiveButton = newAbilitybutton.GetComponent<PassiveButton>();
				passiveButton.OnClick.RemoveAllPersistentAndListeners();
				passiveButton.OnClick.AddListener(
					(UnityEngine.Events.UnityAction)instance.Cancel);
                passiveButton.OnClick.AddListener(
                    (UnityEngine.Events.UnityAction)(
                        () => { newAbilitybutton.gameObject.SetActive(false); }));
				passiveButton.OnClick.AddListener(role.CreateAbilityAction(instance));

                var render = newAbilitybutton.GetComponent<SpriteRenderer>();

				role.ButtonMod(instance, newAbilitybutton);
				role.SetSprite(render);

				meetingAbilityButton[target] = newAbilitybutton;
				abilitybutton = newAbilitybutton;
			}

			if (abilitybutton == null) { return true; }

			abilitybutton.gameObject.SetActive(true);
			instance.Buttons.SetActive(true);

			float startPos = instance.AnimateButtonsFromLeft ? 0.2f : 1.95f;

			instance.StartCoroutine(
				effectsAllWrap(
					new IEnumerator[]
						{
							effectsLerpWrap(0.25f, delegate(float t)
							{
								instance.CancelButton.transform.localPosition = Vector2.Lerp(
									Vector2.right * startPos,
									Vector2.right * 1.3f,
									Effects.ExpOut(t));
							}),
							effectsLerpWrap(0.35f, delegate(float t)
							{
								instance.ConfirmButton.transform.localPosition = Vector2.Lerp(
									Vector2.right * startPos,
									Vector2.right * 0.65f,
									Effects.ExpOut(t));
							}),
							effectsLerpWrap(0.45f, delegate(float t)
							{
								abilitybutton.transform.localPosition = Vector2.Lerp(
									Vector2.right * startPos,
									Vector2.right * -0.01f,
									Effects.ExpOut(t));
							})
						}
					).WrapToIl2Cpp()
				);

			Il2CppSystem.Collections.Generic.List<UiElement> selectableElements = new Il2CppSystem.Collections.Generic.List<UiElement>();
			selectableElements.Add(instance.CancelButton);
			selectableElements.Add(instance.ConfirmButton);
			selectableElements.Add(abilitybutton);

			ControllerManager.Instance.OpenOverlayMenu(
				instance.name,
				instance.CancelButton,
				instance.ConfirmButton, selectableElements, false);
		}

		return false;

	}
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetCosmetics))]
public static class PlayerVoteAreaSetCosmeticsPatch
{
	public static void Postfix(PlayerVoteArea __instance)
	{
		if (ExtremeGameModeManager.Instance.ShipOption.IsFixedVoteAreaPlayerLevel)
        {
			__instance.LevelNumberText.text = "99";
		}
	}
}


[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetDead))]
public static class PlayerVoteAreaSetDeadPatch
{
	public static bool Prefix(
		PlayerVoteArea __instance,
		[HarmonyArgument(0)] bool didReport,
		[HarmonyArgument(1)] bool isDead,
		[HarmonyArgument(2)] bool isGuardian = false)
	{
		if (!ExtremeRolesPlugin.ShipState.AssassinMeetingTrigger) { return true; }

		__instance.AmDead = false;
		__instance.DidReport = didReport;
		__instance.Megaphone.enabled = didReport;
		__instance.Overlay.gameObject.SetActive(false);
		__instance.XMark.gameObject.SetActive(false);

		return false;
	}

	public static void Postfix(
		PlayerVoteArea __instance,
		[HarmonyArgument(0)] bool didReport,
		[HarmonyArgument(1)] bool isDead,
		[HarmonyArgument(2)] bool isGuardian = false)
        {
		if (ExtremeGameModeManager.Instance.ShipOption.IsRemoveAngleIcon)
		{
			__instance.GAIcon.gameObject.SetActive(false);
		}
		else
		{
			bool isGhostRole = isGuardian ||
				ExtremeGhostRoleManager.GameRole.ContainsKey(__instance.TargetPlayerId);

			__instance.GAIcon.gameObject.SetActive(isGhostRole);
		}
	}
}
