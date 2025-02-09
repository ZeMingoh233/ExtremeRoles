﻿using System;

using UnityEngine;

using HarmonyLib;

using AmongUs.Data;

using ExtremeRoles.GameMode;
using ExtremeRoles.Roles;
using ExtremeRoles.Performance;

namespace ExtremeRoles.Patches.Controller
{
	[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChatNote))]
	public static class ChatControllerAddChatNotePatch
	{
		public static bool Prefix(
			ChatController __instance,
			[HarmonyArgument(0)] GameData.PlayerInfo srcPlayer,
			[HarmonyArgument(1)] ChatNoteTypes noteType)
		{

			if (!ExtremeRolesPlugin.ShipState.AssassinMeetingTrigger) { return true; }
			if (noteType == ChatNoteTypes.DidVote) { return false; }

			return true;
		}
	}

	[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    public static class ChatControllerAddChatPatch
    {
        public static bool Prefix(
            ChatController __instance,
            [HarmonyArgument(0)] PlayerControl sourcePlayer,
            [HarmonyArgument(1)] string chatText)
        {
			var roleDict = ExtremeRoleManager.GameRole;

			if (roleDict.Count == 0) { return true; }

			if (!sourcePlayer || !CachedPlayerControl.LocalPlayer)
			{
				return false;
			}

			GameData.PlayerInfo data = CachedPlayerControl.LocalPlayer.Data;
			GameData.PlayerInfo data2 = sourcePlayer.Data;

			bool assassinMeeting = ExtremeRolesPlugin.ShipState.AssassinMeetingTrigger;

			if (!assassinMeeting)
            {
				if (data2 == null || data == null || (data2.IsDead && !data.IsDead))
				{
					return false;
				}
			}

			if (!roleDict.TryGetValue(data.PlayerId, out var role) ||
				!roleDict.TryGetValue(data2.PlayerId, out var role2))
			{
				return true;
			}

			if (assassinMeeting && (!role.IsImpostor() || !role2.IsImpostor()))
            {
				return false;
            }

			ChatBubble chatBubble = __instance.GetPooledBubble();
			try
			{
				chatBubble.transform.SetParent(__instance.scroller.Inner);
				chatBubble.transform.localScale = Vector3.one;
				bool flag = sourcePlayer.PlayerId == CachedPlayerControl.LocalPlayer.PlayerId;
				if (flag)
				{
					chatBubble.SetRight();
				}
				else
				{
					chatBubble.SetLeft();
				}

				Color seeColor;

				if (data.PlayerId == data2.PlayerId)
                {
					seeColor = role.GetNameColor(CachedPlayerControl.LocalPlayer.Data.IsDead);
                }
				else
                {
					seeColor = role.GetTargetRoleSeeColor(role2, data2.PlayerId);
				}

				bool didVote = MeetingHud.Instance && MeetingHud.Instance.DidVote(sourcePlayer.PlayerId);

				chatBubble.SetCosmetics(data2);

				__instance.SetChatBubbleName(
					chatBubble, data2, data2.IsDead,
					didVote, seeColor, null);
				chatBubble.SetText(
					DataManager.Settings.Multiplayer.CensorChat ?
                    BlockedWords.CensorWords(chatText) : chatText);
				chatBubble.AlignChildren();
				__instance.AlignAllBubbles();
				if (!__instance.IsOpenOrOpening && __instance.notificationRoutine == null)
				{
					__instance.notificationRoutine = __instance.StartCoroutine(__instance.BounceDot());
				}
				if (!flag)
				{
					SoundManager.Instance.PlaySound(
						__instance.messageSound, false, 1f).pitch = 0.5f + (float)sourcePlayer.PlayerId / 15f;
				}
				return false;
			}
			catch (Exception ex)
			{
				ExtremeRolesPlugin.Logger.LogError(ex);
				__instance.chatBubblePool.Reclaim(chatBubble);
				return false;
			}

		}
    }

	[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
	public static class ChatControllerSendChatPatch
	{
		public static void Prefix(ChatController __instance)
		{
            if (ExtremeGameModeManager.Instance.RoleSelector.IsCanUseAndEnableXion())
            {
				Roles.Solo.Host.Xion.ParseCommand(
					__instance.freeChatField.Text);
            }
		}
	}
}
