using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;

namespace RTAutoBuilder;

public struct CharacterDisplayEntry
{
    public CharacterEnum Character;
    public bool IsInParty;
    public BaseUnitEntity? Unit;

    public CharacterDisplayEntry(CharacterEnum character, bool isInParty, BaseUnitEntity? unit)
    {
        Character = character;
        IsInParty = isInParty;
        Unit = unit;
    }
}

public enum CharacterEnum
{
    Unknown = 0,
    RogueTrader = 1,
    Abelard = 2,
    Idira = 3,
    Argenta = 4,
    Cassia = 5,
    Heinrix = 6,
    Kibellah = 7,
    Pasqal = 8,
    Jae = 9,
    Yrliet = 10,
    Solomorne = 11,
    Ulfar = 12,
    Marazhai = 13,
    Chorda = 14,
    Winterscale = 15,
    Uralon = 16,
    Eogunn = 17,
    CustomNavigator = 18,
    CustomCompanion = 19,
}

public static class CharacterTools
{
    public static readonly Dictionary<CharacterEnum, List<string>> CharacterBlueprints = new()
    {
        { CharacterEnum.RogueTrader, [ "e9f0f59962bd4cc5893b6611e8084f5e" ] },
        { CharacterEnum.Abelard, [ "51604d37ac0e499da70e2c4a4f086066" ] },
        { CharacterEnum.Idira, [ "948abdeffc744794589d6b3f59a1cd0f" ] },
        { CharacterEnum.Argenta, [ "ec7f34b2ef6d4624af3e5024f4fc8e6e"] },
        { CharacterEnum.Cassia, [ "df918c2d635446e8ba6cba0123d2cb6e"] },
        { CharacterEnum.Heinrix, [ "70e4973d9cef400da93e0b5672dcd014"] },
        { CharacterEnum.Kibellah, [ "88d4190122cd4b6c9c91d7fef4066651"] },
        { CharacterEnum.Pasqal, [ "e1cfcddc1dc447278762a0725753c394"] },
        { CharacterEnum.Jae, [ "768a816a8b734882af399f8258ffcada"] },
        { CharacterEnum.Yrliet, [ "20c5ce9f1e2bcf9448a7a0fd0850f5d2"] },
        { CharacterEnum.Solomorne, [ "a699795d21f74159abb00f9a217fa97d"] },
        { CharacterEnum.Ulfar, [ "daaf3d6bae644af8a9128ea09044bb99"] },
        { CharacterEnum.Marazhai, [ "d2b74abcac1d497992e4cacd2fae1467"] },
        { CharacterEnum.Chorda, [ "884b70bd817640dda143966166587b98", "cabf247f1fb3494289f48a48512132f3"] },
        { CharacterEnum.Winterscale, [ "73c59c9ec5bd4c929be80e15d7f88c73"] },
        { CharacterEnum.Uralon, [ "c222cb0668ea49f4a9ac04de2a3e25ba"] },
        { CharacterEnum.Eogunn, [ "2e5e746cc6d043ab8d395c67d07ac56b"] },
        { CharacterEnum.CustomNavigator, [] },
        { CharacterEnum.CustomCompanion, [] }
    };

    public static CharacterEnum GetChar(BaseUnitEntity entity)
    {
        if (entity.IsMainCharacter)
        {
            return CharacterEnum.RogueTrader;
        }
        if (entity.IsCustomCompanion())
        {
            return entity.IsNavigatorCompanion() ? CharacterEnum.CustomNavigator : CharacterEnum.CustomCompanion;
        }
        KeyValuePair<CharacterEnum, List<string>> result = CharacterBlueprints.AsEnumerable().FirstOrDefault(x => x.Value.Contains(entity.Blueprint.AssetGuid));
        if (result.Equals(default))
        {
            return CharacterEnum.Unknown;
        }
        else
        {
            return result.Key;
        }
    }

    public static readonly Dictionary<string, string> CharacterIdToFirstBlueprint = CharacterBlueprints
        .Where(kvp => kvp.Value.Count > 0)
        .ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value[0]);

    public static string GetName(string id)
    {
        if (Enum.TryParse<CharacterEnum>(id, out var enumChar))
        {
            if (enumChar == CharacterEnum.Unknown)
            {
                return id;
            }
            var bps = CharacterBlueprints[enumChar];
            if (bps != null && bps.Count > 0)
            {
                var blueprint = ResourcesLibrary.TryGetBlueprint<BlueprintUnit>(bps[0]);
                if (blueprint != null && blueprint.LocalizedName != null)
                {
                    return blueprint.LocalizedName.String.Text;
                }
            }
        }
        return id;
    }
}