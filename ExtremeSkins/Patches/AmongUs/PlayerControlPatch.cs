﻿using Hazel;
using HarmonyLib;

using ExtremeSkins.SkinManager;

namespace ExtremeSkins.Patches.AmongUs
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckColor))]
    public static class PlayerControlCheckColorPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte bodyColor)
        {
            // Fix incorrect color assignment
            uint color = (uint)bodyColor;

            if (isTaken(__instance, color) || color >= Palette.PlayerColors.Length)
            {
                int num = 0;
                while (num++ < 50 && (color >= ExtremeColorManager.ColorNum || isTaken(__instance, color)))
                {
                    color = (color + 1) % ExtremeColorManager.ColorNum;
                }
            }
            __instance.RpcSetColor((byte)color);
            return false;
        }

        private static bool isTaken(PlayerControl player, uint color)
        {
            foreach (GameData.PlayerInfo info in GameData.Instance.AllPlayers)
            {
                if (!info.Disconnected &&
                    info.PlayerId != player.PlayerId &&
                    info.DefaultOutfit.ColorId == color)
                {
                    return true;
                }
            }
            return false;
        }

    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    public class PlayerControlHandleRpcPatch
    {
        static void Postfix(
            PlayerControl __instance,
            [HarmonyArgument(0)] byte callId,
            [HarmonyArgument(1)] MessageReader reader)
        {

            if (__instance == null || reader == null) { return; }

            switch (callId)
            {
                case VersionManager.RpcCommand:
                    int major = reader.ReadPackedInt32();
                    int minor = reader.ReadPackedInt32();
                    int build = reader.ReadPackedInt32();
                    int revision = reader.ReadPackedInt32();
                    int clientId = reader.ReadPackedInt32();
                    VersionManager.AddVersionData(
                        major, minor, build,
                        revision, clientId);
                    break;
                default:
                    break;
            }
        }
    }
}
