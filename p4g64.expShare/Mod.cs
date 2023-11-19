using p4g64.expShare.Configuration;
using p4g64.expShare.NuGet.templates.defaultPlus;
using p4g64.expShare.Template;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using System.IO;
using static p4g64.expShare.Native;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p4g64.expShare;
/// <summary>
/// Your mod logic goes here.
/// </summary>
public unsafe class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private IHook<SetupResultsExpDelegate> _setupExpHook;
    private IHook<GivePartyMembersExpDelegate> _givePartyMembersExpHook;
    private IHook<PartyMemberLevelUpsDelegate> _partyMemberLevelUpsHook;

    private Dictionary<PartyMember, int> _expGains = new();
    private Dictionary<PartyMember, PersonaChanges> _levelUps = new();

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;

        Utils.Initialise(_logger, _configuration, _modLoader);
        Native.Initialise(_hooks);

        Utils.SigScan("48 89 54 24 ?? 48 89 4C 24 ?? 53 55 56 57 41 54 41 56", "SetupResultsExp", address =>
        {
            _setupExpHook = _hooks.CreateHook<SetupResultsExpDelegate>(SetupResultsExp, address).Activate();
        });

        Utils.SigScan("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 20 48 8B D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 8D B3 ?? ?? ?? ??", "GivePartyMembersExp", address =>
        {
            _givePartyMembersExpHook = _hooks.CreateHook<GivePartyMembersExpDelegate>(GivePartyMembersExp, address).Activate();
        });

        Utils.SigScan("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 81 EC A0 00 00 00 48 8B 05 ?? ?? ?? ??", "PartyMemberLevelUps", address =>
        {
            _partyMemberLevelUpsHook = _hooks.CreateHook<PartyMemberLevelUpsDelegate>(PartyMemberLevelUps, address).Activate();
        });

    }

    private void SetupResultsExp(ResultsPartyInfo* results, astruct_8* param_2)
    {
        _setupExpHook.OriginalFunction(results, param_2);

        // Setup Party Exp
        for (PartyMember member = PartyMember.Yosuke; member <= PartyMember.Teddie; member++)
        {
            _expGains.Remove(member);
            _levelUps.Remove(member);
            if (!IsInactive(member, results)) continue;

            var persona = GetPartyMemberPersona(member);
            var level = persona->Level;
            if (level >= 99) continue;

            int gainedExp = (int)(CalculateGainedExp(level, param_2) * _configuration.PartyExpMultiplier);
            var currentExp = persona->Exp;
            var requiredExp = GetTotalRequiredExp(persona, (ushort)(level + 1));
            _expGains[member] = gainedExp;
            if (requiredExp <= currentExp + gainedExp)
            {
                Utils.LogDebug($"{member} is ready to level up");
                results->LevelUpStatus |= 0x10; // signify that a party member is ready to level up
                var statChanges = new PersonaChanges { };
                GenerateLevelUpPersona(persona, &statChanges, gainedExp);
                _levelUps[member] = statChanges;
            }
        }

        // Setup Protag Persona Exp
        //var activePersona = GetPartyMemberPersona(PartyMember.Protag);
        //for (short i = 0; i < 12; i++)
        //{
        //    var persona = GetProtagPersona(i);
        //    if (persona == (Persona*)0 || persona->Id == activePersona->Id)
        //        continue;

        //    var level = persona->Level;
        //    if (level >= 99) continue;

        //    results->ProtagExpGains[i] += (uint)(CalculateGainedExp(level, param_2) * _configuration.PersonaExpMultiplier);
        //    int gainedExp = (int)results->ProtagExpGains[i];
        //    Utils.LogDebug($"Giving Protag Persona {i} ({persona->Id}) {gainedExp} exp");
        //    var currentExp = persona->Exp;
        //    var requiredExp = GetPersonaRequiredExp(persona, (ushort)(level + 1));
        //    if (requiredExp <= currentExp + gainedExp)
        //    {
        //        Utils.LogDebug($"Protag Persona {i} ({persona->Id}) is ready to level up");
        //        results->LevelUpStatus |= 8; // signify that a protag persona is ready to level up
        //        GenerateLevelUpPersona(persona, &(&results->ProtagPersonaChanges)[i], gainedExp);
        //    }
        //}
    }

    private void GivePartyMembersExp(ResultsPartyInfo* results)
    {
        _givePartyMembersExpHook.OriginalFunction(results);

        for (PartyMember member = PartyMember.Yosuke; member <= PartyMember.Teddie; member++)
        {
            if (!IsInactive(member, results) || !_expGains.ContainsKey(member)) continue;

            var persona = GetPartyMemberPersona(member);
            var expGained = _expGains[member];
            persona->Exp += expGained;
            Utils.LogDebug($"Gave {expGained} exp to {member}");

            var requiredExp = GetTotalRequiredExp(persona, (ushort)(persona->Level + 1));
            if (requiredExp <= persona->Exp)
            {
                var statChanges = _levelUps[member];
                Utils.LogError($"Levelling up {member} without menu display, this shouldn't happen!");
                HiddenLevelUpPartyMember(persona, &statChanges);
            }
        }
    }

    private nuint PartyMemberLevelUps(PartyMemberLevelUpsArgs* args)
    {
        var info = args->Info;
        // Give exp to any inactive members that didn't level up
        if (_levelUps.Count == 0 && _expGains.Count > 0)
        {
            for (int i = 0; i < _expGains.Count; i++)
            {
                var partyMember = _expGains.First().Key;
                var persona = GetPartyMemberPersona(partyMember);
                var expGained = _expGains[partyMember];
                persona->Exp += expGained;
                _expGains.Remove(partyMember);
                Utils.LogDebug($"Gave {expGained} exp to {partyMember}");
            }
        }

        // We only want to change stuff in state 1 (when it's picking a Persona's level up stuff to deal with)
        if (info->State > 1 || _levelUps.Count == 0)
        {
            return _partyMemberLevelUpsHook.OriginalFunction(args);
        }

        var partyInfo = &info->Results->PartyInfo;

        Utils.LogDebug($"LevelUpSlot = {info->LevelUpSlot}");
        for (int i = 0; i < 4; i++)
        {
            Utils.LogDebug($"Slot {i} is {(PartyMember)partyInfo->PartyMembers[i]} who has {(&partyInfo->PersonaChanges)[i].LevelIncrease} level increases and {partyInfo->ExpGains[i]} exp gained.");
        }

        // Wait until all of the real level ups have been done so we can safely overwrite their data
        for (int i = info->LevelUpSlot; i < 4; i++)
        {
            var curMember = partyInfo->PartyMembers[i];
            if (curMember == 0) continue;

            if ((&partyInfo->PersonaChanges)[i].LevelIncrease != 0)
            {
                return _partyMemberLevelUpsHook.OriginalFunction(args);
            }
            else
            {
                // Give them the exp ourself and clear them to ensure they get it exactly once
                GetPartyMemberPersona((PartyMember)curMember)->Exp += (int)partyInfo->ExpGains[i];
                partyInfo->PartyMembers[i] = 0;
            }
        }

        // Clear all of the real level ups so they can't loop
        for (int i = 1; i < 4; i++)
            partyInfo->PartyMembers[i] = 0;

        // Change the data of an active party member to an inactive one
        info->LevelUpSlot = 0;
        var levelUp = _levelUps.First();
        var member = levelUp.Key;
        var statChanges = levelUp.Value;
        partyInfo->PartyMembers[0] = (short)member;
        partyInfo->ExpGains[0] = (uint)_expGains[member];
        partyInfo->PersonaChanges = statChanges;

        Utils.LogDebug($"Leveling up {member}");
        _levelUps.Remove(member);
        _expGains.Remove(member);

        return _partyMemberLevelUpsHook.OriginalFunction(args);
    }


    private bool IsInactive(PartyMember member, ResultsPartyInfo* results)
    {
        // Check if they're already in the party
        for (int i = 0; i < 4; i++)
        {
            if (results->PartyMembers[i] == (short)member) return false;
        }

        return IsPartyMemberAvailable(member);
    }

    private delegate void SetupResultsExpDelegate(ResultsPartyInfo* results, astruct_8* param_2);
    private delegate void GivePartyMembersExpDelegate(ResultsPartyInfo* results);
    private delegate nuint PartyMemberLevelUpsDelegate(PartyMemberLevelUpsArgs* args);

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }
    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}