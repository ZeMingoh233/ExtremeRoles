﻿using System.Collections.Generic;
using System.Text;

using Hazel;

using ExtremeRoles.GhostRoles.Crewmate;
using ExtremeRoles.GhostRoles.Impostor;


namespace ExtremeRoles.Module
{
    public class GhostRoleAbilityManager
    {
        public enum AbilityType : byte
        {
            PoltergeistMoveDeadbody,
            FaunusOpenSaboConsole,

            VentgeistVentAnime,
            SaboEvilResetSabotageCool
        }

        private HashSet<AbilityType> useAbility = new HashSet<AbilityType>();

        public GhostRoleAbilityManager()
        {
            this.useAbility.Clear();
        }

        public void Clear()
        {
            this.useAbility.Clear();
        }
        public string CreateAbilityReport()
        {

            StringBuilder creater = new StringBuilder(this.useAbility.Count);
            foreach (AbilityType abilityType in this.useAbility)
            {
                creater.AppendLine(
                    Helper.Translation.GetString(
                        abilityType.ToString()));
            }

            return creater.ToString();
        }
        public bool IsUseAbility() => this.useAbility.Count > 0;

        public void AddAbilityCall(AbilityType abilityType)
        {
            this.useAbility.Add(abilityType);
        }

        public void UseGhostAbility(
            byte abilityType, ref MessageReader reader)
        {
            switch ((AbilityType)abilityType)
            {
                case AbilityType.VentgeistVentAnime:
                    int ventId = reader.ReadInt32();
                    Ventgeist.VentAnime(ventId);
                    break;
                case AbilityType.PoltergeistMoveDeadbody:
                    byte poltergeistPlayerId = reader.ReadByte();
                    byte poltergeistMoveDeadbodyPlayerId = reader.ReadByte();
                    bool pickUp = reader.ReadBoolean();
                    Poltergeist.DeadbodyMove(
                        poltergeistPlayerId,
                        poltergeistMoveDeadbodyPlayerId, pickUp);
                    break;
                case AbilityType.SaboEvilResetSabotageCool:
                    SaboEvil.ResetCool();
                    break;
                default:
                    break;
            }
            this.useAbility.Add((AbilityType)abilityType);
        }
    }
}
