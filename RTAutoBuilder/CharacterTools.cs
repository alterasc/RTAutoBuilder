using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Localization;

namespace RTAutoBuilder;

public static class CharacterTools
{
    public const string MAIN_CHARACTER_ID = "RogueTrader";
    public const string MAIN_CHARACTER_NAME_KEY = "e9f0f599-62bd-4cc5-893b-6611e8084f5e";

    private static readonly List<RTCharacter> characters = [
        new RTCharacter(MAIN_CHARACTER_ID, 1, null, MAIN_CHARACTER_NAME_KEY),
        new RTCharacter("Abelard", 2, "51604d37ac0e499da70e2c4a4f086066"),
        new RTCharacter("Idira", 3, "948abdeffc744794589d6b3f59a1cd0f"),
        new RTCharacter("Argenta", 4, "ec7f34b2ef6d4624af3e5024f4fc8e6e"),
        new RTCharacter("Cassia", 5, "df918c2d635446e8ba6cba0123d2cb6e"),
        new RTCharacter("Heinrix", 6, "70e4973d9cef400da93e0b5672dcd014"),
        new RTCharacter("Kibellah", 7, "88d4190122cd4b6c9c91d7fef4066651"),
        new RTCharacter("Pasqal", 8, "e1cfcddc1dc447278762a0725753c394"),
        new RTCharacter("Jae", 9, "768a816a8b734882af399f8258ffcada"),
        new RTCharacter("Yrliet", 10, "20c5ce9f1e2bcf9448a7a0fd0850f5d2"),
        new RTCharacter("Solomorne", 11, "a699795d21f74159abb00f9a217fa97d"),
        new RTCharacter("Ulfar", 12, "daaf3d6bae644af8a9128ea09044bb99"),
        new RTCharacter("Marazhai", 13, "d2b74abcac1d497992e4cacd2fae1467"),
        new RTCharacter("Chorda", 14, "884b70bd817640dda143966166587b98", null, ["cabf247f1fb3494289f48a48512132f3"]),
        new RTCharacter("Winterscale", 15, "73c59c9ec5bd4c929be80e15d7f88c73"),
        new RTCharacter("Uralon", 16, "c222cb0668ea49f4a9ac04de2a3e25ba"),
    ];

    public static RTCharacter GetRTCharacter(BaseUnitEntity unitEntity)
    {
        if (unitEntity == null)
        {
            throw new ArgumentNullException(nameof(unitEntity));
        }
        if (unitEntity.IsMainCharacter)
        {
            return characters.First(x => x.Id == MAIN_CHARACTER_ID);
        }
        else
        {
            var result = characters.First(x => x.AllBlueprints.Contains(unitEntity.Blueprint.AssetGuid));
            return result;
        }
    }

    public static RTCharacter GetRTCharacter(int index)
    {
        return characters.First(x => x.Index == index);
    }

    public static RTCharacter GetRTCharacter(string id)
    {
        return characters.First(x => x.Id == id);
    }


    public static string GetName(string id)
    {
        return characters.First(x => x.Id == id).LocalizedName;
    }
}

public class RTCharacter
{
    public readonly string Id;
    public readonly int Index;
    public readonly string? BlueprintId;
    public readonly string[] AllBlueprints;
    private readonly LocalizedString? nameOverride;

    public RTCharacter(string id, int index, string? blueprintId, string? nameKey = null, string[]? allBlueprints = null)
    {
        Id = id;
        Index = index;
        BlueprintId = blueprintId;
        if (nameKey != null)
        {
            nameOverride = new LocalizedString { Key = nameKey };
        }
        if (blueprintId != null)
        {
            AllBlueprints = [blueprintId, .. allBlueprints ?? []];
        }
        else
        {
            AllBlueprints = [];
        }
    }

    public string LocalizedName
    {
        get
        {
            if (nameOverride != null)
            {
                return nameOverride.Text;
            }
            return ResourcesLibrary.TryGetBlueprint<BlueprintUnit>(BlueprintId).LocalizedName.String.Text;
        }
    }
}