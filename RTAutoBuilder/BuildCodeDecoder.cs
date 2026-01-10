using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.UnitLogic.Levelup.Selections;
using Kingmaker.UnitLogic.Levelup.Selections.Feature;
using Kingmaker.UnitLogic.Progression.Paths;
using System.Numerics;

namespace RTAutoBuilder;

internal static class BuildCodeDecoder
{
    public const string ExemplarArchetype = "bcefe9c41c7841c9a99b1dbac1793025";
    private static class Base62
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        internal static byte[] DecodeWithLength(string input)
        {
            BigInteger value = BigInteger.Zero;

            foreach (char c in input)
            {
                int index = Alphabet.IndexOf(c);
                if (index < 0)
                    throw new ArgumentException("Invalid Base62 character");

                value = value * 62 + index;
            }

            var bytes = new List<byte>();
            while (value > 0)
            {
                bytes.Insert(0, (byte)(value & 0xFF));
                value >>= 8;
            }

            if (bytes.Count == 0)
                return [];

            int length = bytes[0];
            if (length < 0 || length > bytes.Count - 1)
                throw new ArgumentException("Invalid length prefix");

            return bytes.GetRange(1, length).ToArray();
        }
    }

    internal static BuildPlan Decode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            throw new Exception("Empty code");
        }
        byte[] arr;
        try
        {
            arr = Base62.DecodeWithLength(code);
        }
        catch (Exception)
        {
            throw new Exception("Invalid code");
        }
        var unpacker = new BitUnpacker(arr);
        var result = new BuildPlan();
        result.BuildCode = code;
        result.Version = unpacker.Read(5);
        if (result.Version < 1)
        {
            throw new Exception("Invalid code version");
        }
        if (result.Version > 0)
        {
            throw new Exception("Build is marked for version not supported by this mod version. Update the mod.");
        }
        RTCharacter rtCharacter;
        try
        {
            rtCharacter = CharacterTools.GetRTCharacter(unpacker.Read(5));
        }
        catch (Exception)
        {
            throw new Exception("Code for invalid character");
        }
        result.UnitId = rtCharacter.Id;
        BlueprintCareerPath firstArchetype;
        BlueprintCareerPath secondArchetype;
        try
        {
            result.FirstArchetype = unpacker.ReadGroup(FeatureGroup.ChargenCareerPath);
            result.SecondArchetype = unpacker.ReadGroup(FeatureGroup.ChargenCareerPath);
            firstArchetype = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(result.FirstArchetype);
            secondArchetype = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(result.SecondArchetype);
        }
        catch (Exception)
        {
            throw new Exception("Invalid code. Plan must have all archetypes set.");
        }
        foreach (var _ in PlayerStatsSorted)
        {
            unpacker.Read(2);
        }
        // reading them to progress offset, not using them for now
        unpacker.ReadGroup(FeatureGroup.ChargenHomeworld);
        unpacker.ReadGroup(FeatureGroup.ChargenImperialWorld);
        unpacker.ReadGroup(FeatureGroup.ChargenForgeWorld);
        unpacker.ReadGroup(FeatureGroup.ChargenOccupation);
        unpacker.ReadGroup(FeatureGroup.ChargenNavigator);
        unpacker.ReadGroup(FeatureGroup.ChargenPsyker);
        unpacker.ReadGroup(FeatureGroup.ChargenArbitrator);
        unpacker.ReadGroup(FeatureGroup.ChargenMomentOfTriumph);
        unpacker.ReadGroup(FeatureGroup.ChargenDarkestHour);

        var exemplarArchetype = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(ExemplarArchetype);
        ReadArchetypeSelections(result, unpacker, firstArchetype);
        ReadArchetypeSelections(result, unpacker, secondArchetype);
        ReadArchetypeSelections(result, unpacker, exemplarArchetype);
        return result;
    }

    private static void ReadArchetypeSelections(BuildPlan result, BitUnpacker unpacker, BlueprintCareerPath archetype)
    {
        result.Selections[archetype.AssetGuid] = [];
        for (int i = 0; i < archetype.RankEntries.Length; i++)
        {
            BlueprintPath.RankEntry entry = archetype.RankEntries[i];
            var rank = i + 1;
            if (entry.m_Selections.Length > 0)
            {
                for (int j = 0; j < entry.m_Selections.Length; j++)
                {
                    var featureSelection = entry.m_Selections[j].Get() as BlueprintSelectionFeature;
                    var sel = unpacker.ReadGroup(featureSelection!.Group);
                    result.Selections[archetype.AssetGuid].Add(new BuildPlan.PlanRankEntry
                    {
                        FeatureGroup = featureSelection.Group.ToString(),
                        Rank = rank,
                        Selection = sel
                    });
                    //Main.Log.Log($"Set selection for first at rank {rank}, group: {featureSelection.Group}, selection: {sel}");
                }
            }
        }
    }

    private static string[] PlayerStatsSorted = [
        "WarhammerWeaponSkill",
        "WarhammerBallisticSkill",
        "WarhammerStrength",
        "WarhammerToughness",
        "WarhammerAgility",
        "WarhammerIntelligence",
        "WarhammerPerception",
        "WarhammerWillpower",
        "WarhammerFellowship"
    ];

    private static int GetGroupLength(string group)
    {
        var groupLength = Main.CodeGuidMap[group].Count;
        var bitsNeeded = (int)Math.Ceiling(Math.Log(groupLength, 2));
        return bitsNeeded;
    }

    internal static string MapGroup(FeatureGroup group)
    {
        return group switch
        {
            FeatureGroup.ActiveAbility or FeatureGroup.FirstCareerAbility or FeatureGroup.SecondCareerAbility or FeatureGroup.FirstOrSecondCareerAbility => "Ability",
            FeatureGroup.Talent or FeatureGroup.FirstCareerTalent or FeatureGroup.SecondCareerTalent or FeatureGroup.FirstOrSecondCareerTalent or FeatureGroup.CommonTalent => "Talent",
            FeatureGroup.None => string.Empty,
            _ => group.ToString(),
        };
    }

    private class BitUnpacker
    {
        private readonly byte[] _data;
        private int _bitOffset;

        public BitUnpacker(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _bitOffset = 0;
        }

        public int Read(int bits)
        {
            int value = 0;

            for (int i = 0; i < bits; i++)
            {
                int byteIndex = _bitOffset >> 3;
                int bitIndex = 7 - (_bitOffset & 7);

                int bit = (_data[byteIndex] >> bitIndex) & 1;

                value = (value << 1) | bit;
                _bitOffset++;
            }
            return value;
        }

        public string ReadGroup(FeatureGroup featureGroup)
        {
            string mappedGroup = MapGroup(featureGroup);
            int length = GetGroupLength(mappedGroup);
            int value = Read(length);
            //Main.Log.Log($"Read {length} bits, value: {value}, new offset: {_bitOffset}, mapped group: {mappedGroup}, original group: {featureGroup}");
            if (value == 0)
            {
                return string.Empty;
            }
            var result = Main.CodeGuidMap[mappedGroup][value];
            Main.Log.Log($"Decode feature {ResourcesLibrary.TryGetBlueprint<BlueprintUnitFact>(result).Name}");
            return result;
        }

        public bool HasMore()
        {
            return _bitOffset < _data.Length * 8;
        }
    }
}

