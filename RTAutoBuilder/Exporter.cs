using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Levelup.Selections;
using Kingmaker.UnitLogic.Levelup.Selections.Feature;
using Kingmaker.UnitLogic.Progression.Features.Advancements;
using Kingmaker.UnitLogic.Progression.Paths;
using Kingmaker.Utility.DotNetExtensions;

namespace RTAutoBuilder;

internal class Exporter
{

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

    static (int value, int length) GetIndex(FeatureGroup featureGroup, string guid)
    {
        var mappedGroup = MapGroup(featureGroup);
        var mappings = Main.CodeGuidMap[mappedGroup];
        var length = BuildCodeDecoder.GetGroupLength(mappedGroup);
        var value = mappings.Where(x => x.Value == guid).First().Key;
        return (value, length);
    }

    static void WriteGroupSelection(List<int> choices, List<int> bitsPerChoice, List<FeatureSelectionData> selections, FeatureGroup group)
    {
        var mappedGroup = MapGroup(group);
        var list = selections.Where(x => x.Selection.Group == group).ToList();
        if (list.Count == 1)
        {
            var selectedFeature = list[0].Feature;
            var r = GetIndex(group, selectedFeature.AssetGuid);
            Main.Log.Log($"Writing {r.value} for {r.length} bits for group {group} at rank {list[0].Level}");
            choices.Add(r.value);
            bitsPerChoice.Add(r.length);
        }
        else
        {
            var length = BuildCodeDecoder.GetGroupLength(mappedGroup);
            choices.Add(0);
            bitsPerChoice.Add(length);
            Main.Log.Log($"Writing {0} for {length} bits for group {group}");
        }
    }

    static void WriteSingleSelection(List<int> choices, List<int> bitsPerChoice, FeatureSelectionData selectedFeature)
    {
        var r = GetIndex(selectedFeature.Selection.Group, selectedFeature.Feature.AssetGuid);
        choices.Add(r.value);
        bitsPerChoice.Add(r.length);
    }

    static void WriteEmptyValue(List<int> choices, List<int> bitsPerChoice, FeatureGroup group)
    {
        var r = BuildCodeDecoder.GetGroupLength(MapGroup(group));
        choices.Add(0);
        bitsPerChoice.Add(r);
    }

    internal static string ExportCompanions(BaseUnitEntity entity)
    {
        Main.Log.Log($"Processing {entity.Name}");

        List<int> choices = [];
        List<int> bitsPerChoice = [];

        choices.Add(1);
        bitsPerChoice.Add(5);

        var rtChar = CharacterTools.GetRTCharacter(entity);
        choices.Add(rtChar.Index);
        bitsPerChoice.Add(5);

        BlueprintCareerPath firstArchetype = entity.Progression.AllCareerPaths.Select(x => x.Blueprint)
            .First(x => x.Ranks == 15);

        var a = GetIndex(FeatureGroup.ChargenCareerPath, firstArchetype.AssetGuid);
        choices.Add(a.value);
        bitsPerChoice.Add(a.length);

        BlueprintCareerPath? secondArchetype = entity.Progression.AllCareerPaths.Select(x => x.Blueprint)
            .FirstOrDefault(x => x.Ranks == 20 && x.AssetGuid != BuildCodeDecoder.ExemplarArchetype);
        if (secondArchetype != null)
        {
            var b = GetIndex(FeatureGroup.ChargenCareerPath, secondArchetype.AssetGuid);
            choices.Add(b.value);
            bitsPerChoice.Add(b.length);
        }
        else
        {
            choices.Add(0);
            bitsPerChoice.Add(BuildCodeDecoder.GetGroupLength(FeatureGroup.ChargenCareerPath.ToString()));
        }
        var statSelections = entity.Progression.m_Selections.Where(x => x.Level == 0 && x.Selection.Group == FeatureGroup.ChargenAttribute)
            .Select(x => (x.Feature as BlueprintAttributeAdvancement)!.Stat.ToString()).ToArray();

        foreach (var stat in BuildCodeDecoder.PlayerStatsSorted)
        {
            var count = statSelections.Where(x => x == stat).Count();
            choices.Add(count);
            bitsPerChoice.Add(2);
        }

        List<FeatureSelectionData> lvl0Selections = entity.Progression.m_Selections.Where(x => x.Level == 0).ToList();
        {
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenHomeworld);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenImperialWorld);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenForgeWorld);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenOccupation);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenNavigator);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenPsyker);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenArbitrator);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenMomentOfTriumph);
            WriteGroupSelection(choices, bitsPerChoice, lvl0Selections, FeatureGroup.ChargenDarkestHour);
        }

        WriteArchetypeSelections(entity, choices, bitsPerChoice, firstArchetype);
        if (secondArchetype != null)
        {
            WriteArchetypeSelections(entity, choices, bitsPerChoice, secondArchetype);
        }
        WriteArchetypeSelections(entity, choices, bitsPerChoice, ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(BuildCodeDecoder.ExemplarArchetype));

        var arr = BitPacker.PackChoices(choices.ToArray(), bitsPerChoice.ToArray());
        var code = BuildCodeDecoder.Base62.EncodeWithLength(arr);
        return code;
    }

    private static void WriteArchetypeSelections(BaseUnitEntity entity, List<int> choices, List<int> bitsPerChoice, BlueprintCareerPath archetype)
    {
        var pathSelections = entity.Progression.GetSelectionsByPath(archetype).ToArray();
        for (int i = 0; i < archetype.RankEntries.Length; i++)
        {
            BlueprintPath.RankEntry? rankEntry = archetype.RankEntries[i];
            foreach (var sel in rankEntry.Selections)
            {
                var selectionFeature = (sel as BlueprintSelectionFeature)!;
                try
                {
                    var selected = pathSelections.Where(x => x.Level == i + 1 && x.Selection.AssetGuid == selectionFeature.AssetGuid).ToArray();
                    if (selected.Any())
                    {
                        WriteSingleSelection(choices, bitsPerChoice, selected[0]);
                    }
                    else
                    {
                        WriteEmptyValue(choices, bitsPerChoice, selectionFeature.Group);
                    }
                }
                catch (Exception)
                {
                    Main.Log.Log("Couldn't ");
                }
            }
        }
    }
}
