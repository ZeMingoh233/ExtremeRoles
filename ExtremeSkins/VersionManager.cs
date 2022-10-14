﻿using System;
using System.Collections.Generic;
using System.Reflection;

using Hazel;

namespace ExtremeSkins
{
    public static class VersionManager
    {
        public static Dictionary<int, Version> PlayerVersion = new Dictionary<int, Version>();
        public const byte RpcCommand = byte.MaxValue - 1;

        public static void ShareVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                RpcCommand, Hazel.SendOption.Reliable, -1);
            writer.WritePacked(ver.Major);
            writer.WritePacked(ver.Minor);
            writer.WritePacked(ver.Build);
            writer.WritePacked(ver.Revision);
            writer.WritePacked(AmongUsClient.Instance.ClientId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            AddVersionData(
                ver.Major, ver.Minor,
                ver.Build, ver.Revision,
                AmongUsClient.Instance.ClientId);
        }
        public static void AddVersionData(
            int major, int minor,
            int build, int revision, int clientId)
        {
            PlayerVersion[clientId] = new System.Version(
                    major, minor, build, revision);
        }

    }
}
