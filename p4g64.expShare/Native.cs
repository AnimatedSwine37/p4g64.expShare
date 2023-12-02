using p4g64.expShare.NuGet.templates.defaultPlus;
using Reloaded.Hooks.ReloadedII.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static p4g64.expShare.Native;

namespace p4g64.expShare;
internal static unsafe class Native
{
    internal static GetPartyMemberPersonaDelegate GetPartyMemberPersona;
    internal static CalculateGainedExpDelegate CalculateGainedExp;
    internal static CalculateAdjustedGainedExpDelegate CalculateAdjustedGainedExp;
    internal static GetTotalRequiredExpDelegate GetTotalRequiredExp;
    internal static GenerateLevelUpPersonaDelegate GenerateLevelUpPersona;
    internal static CanPersonaLevelUpDelegate CanPersonaLevelUp;
    internal static HiddenLevelUpPartyMemberDelegate HiddenLevelUpPartyMember;
    internal static IsPartyMemberAvailableDelegate IsPartyMemberAvailable;
    internal static PartyInfo** PartyInfoThing;

    internal static void Initialise(IReloadedHooks hooks)
    {
        Utils.SigScan("40 53 48 83 EC 20 0F B7 D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 66 83 FB 01 75 ?? 48 8B 15 ?? ?? ?? ?? 48 0F BF 82 ?? ?? ?? ??", "GetPartyMemberPersona", address =>
        {
            GetPartyMemberPersona = hooks.CreateWrapper<GetPartyMemberPersonaDelegate>(address, out _);
        });

        Utils.SigScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 8B F1 48 8B FA", "CalculateGainedExp", address =>
        {
            CalculateGainedExp = hooks.CreateWrapper<CalculateGainedExpDelegate>(address, out _);
        });

        Utils.SigScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 8B F1 48 8B FA", "CalculateAdjustedGainedExp", address =>
        {
            CalculateAdjustedGainedExp = hooks.CreateWrapper<CalculateAdjustedGainedExpDelegate>(address, out _);
        });

        Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 89 CF 0F B7 DA 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 66 8B 05 ?? ?? ?? ??", "GetTotalRequiredExp", address =>
        {
            GetTotalRequiredExp = hooks.CreateWrapper<GetTotalRequiredExpDelegate>(address, out _);
        });

        Utils.SigScan("48 89 5C 24 ?? 48 89 54 24 ?? 48 89 4C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 30", "GenerateLevelUpPersona", address =>
        {
            GenerateLevelUpPersona = hooks.CreateWrapper<GenerateLevelUpPersonaDelegate>(address, out _);
        });

        Utils.SigScan("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 20 48 89 CE 49 89 D6", "HiddenLevelUpPartyMember", address =>
        {
            HiddenLevelUpPartyMember = hooks.CreateWrapper<HiddenLevelUpPartyMemberDelegate>(address, out _);
        });

        Utils.SigScan("40 53 48 83 EC 20 0F BF D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 33 D2", "IsPartyMemberAvailable", address =>
        {
            IsPartyMemberAvailable = hooks.CreateWrapper<IsPartyMemberAvailableDelegate>(address, out _);
        });

        Utils.SigScan("48 8B 15 ?? ?? ?? ?? 66 89 87 ?? ?? ?? ??", "PartyInfoPtr", address =>
        {
            PartyInfoThing = (PartyInfo**)Utils.GetGlobalAddress(address + 3);
            Utils.LogDebug($"Found PartyInfoThing at 0x{(nuint)PartyInfoThing:X}");
        });
    }

    internal delegate Persona* GetPartyMemberPersonaDelegate(PartyMember member);
    internal delegate int CalculateGainedExpDelegate(int level, astruct_8* param_2);
    internal delegate int CalculateAdjustedGainedExpDelegate(astruct_8* param_1, int gainedExp, PartyMember partyMember);
    internal delegate int GetTotalRequiredExpDelegate(Persona* persona, ushort level);
    internal delegate void GenerateLevelUpPersonaDelegate(Persona* persona, PersonaChanges* changes, int gainedExp);
    internal delegate nuint CanPersonaLevelUpDelegate(Persona* persona, nuint expGained, nuint param_3, nuint param_4);
    internal delegate void HiddenLevelUpPartyMemberDelegate(Persona* persona, PersonaChanges* personaStatChanges);
    internal delegate bool IsPartyMemberAvailableDelegate(PartyMember member);

    internal enum PartyMember : short
    {
        None,
        Protag,
        Yosuke,
        Chie,
        Yukiko,
        Rise,
        Kanji,
        Naoto,
        Teddie
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct Persona
    {
        [FieldOffset(0)]
        internal bool IsRegistered;

        [FieldOffset(2)]
        internal short Id;

        [FieldOffset(4)]
        internal byte Level;

        [FieldOffset(8)]
        internal int Exp;

        [FieldOffset(12)]
        internal fixed short Skils[8];

        [FieldOffset(0x1C)]
        internal PersonaStats Stats;

        [FieldOffset(0x21)]
        internal PersonaStats BonusStats;

        [FieldOffset(0x2F)]
        byte unk; // just to make it 0x30 long
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PersonaStats
    {
        internal byte Strength;
        internal byte Magic;
        internal byte Endurance;
        internal byte Agility;
        internal byte Luck;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PersonaChanges
    {
        [FieldOffset(0)]
        internal byte LevelIncrease;

        [FieldOffset(0x87)]
        byte unk; // Just to make it the right length
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct ResultsPartyInfo
    {
        [FieldOffset(0)]
        internal int LevelUpStatus;

        [FieldOffset(4)]
        internal int GainedExp;

        [FieldOffset(8)]
        internal fixed uint ProtagExpGains[12];

        [FieldOffset(0x38)]
        internal PersonaChanges ProtagPersonaChanges; // This is an array of 12

        [FieldOffset(0x69A)]
        internal fixed short PartyMembers[4];

        [FieldOffset(0x6A4)]
        internal fixed uint ExpGains[4];

        [FieldOffset(0x6B4)]
        internal PersonaChanges PersonaChanges; // This is an array of 4
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PartyMemberLevelUpsArgs
    {
        [FieldOffset(0x48)]
        internal astruct_9* Info;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct astruct_9
    {
        [FieldOffset(4)]
        internal uint State;

        [FieldOffset(0x68)]
        internal int LevelUpSlot;

        [FieldOffset(0x70)]
        internal BattleResults* Results;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct BattleResults
    {
        [FieldOffset(0x74)]
        internal ResultsPartyInfo PartyInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct astruct_8
    {

    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PartyInfo
    {
        [FieldOffset(4)]
        internal fixed short InParty[3];

        [FieldOffset(0xa30)]
        internal short ActivePersonaSlot;

        [FieldOffset(0xa34)]
        internal Persona ProtagPersonas; // this is an array of 12
    }
}
