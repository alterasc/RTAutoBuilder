using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Persistence;
using Newtonsoft.Json;

namespace RTAutoBuilder;

public class SaveSpecificSettings
{
    public Dictionary<string, string> AppliedBuilds = [];

    public const string SaveFileKey = "RTAutoBuilder.SaveSpecificSettings";
    private static void TryLoadSaveSpecificSettings(InGameSettings? maybeSettings)
    {
        var settingsList = maybeSettings?.List ?? Game.Instance?.State?.InGameSettings?.List;
        if (settingsList == null)
        {
            return;
        }
        Main.Log.Log($"Reloading SaveSpecificSettings.");
        SaveSpecificSettings? loaded = null;
        if (settingsList.TryGetValue(SaveFileKey, out var obj) && obj is string json)
        {
            try
            {
                loaded = JsonConvert.DeserializeObject<SaveSpecificSettings>(json);
                Main.Log.Log($"Successfully deserialized SaveSpecificSettings.");
            }
            catch (Exception ex)
            {
                Main.Log.Error($"Deserialization of SaveSpecificSettings failed:\n{ex}");
            }
        }
        if (loaded == null)
        {
            Main.Log.Warning("SaveSpecificSettings not found, creating new...");
            loaded = new();
            loaded.Save();
        }
        Instance = loaded;
    }
    public static SaveSpecificSettings? Instance
    {
        get
        {
            if (field == null)
            {
                TryLoadSaveSpecificSettings(null);
            }
            return field;
        }
        private set;
    }
    public void Save()
    {
        var list = Game.Instance?.State?.InGameSettings?.List;
        if (list == null)
        {
            Main.Log.Log("Warning: Tried to save SaveSpecificSettings while InGameSettingsList was null");
            return;
        }
        var json = JsonConvert.SerializeObject(this);
        list[SaveFileKey] = json;
    }

    [HarmonyPatch]
    private class Patches
    {
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveRoutine))]
        [HarmonyPrefix]
        private static void SaveManager_SaveRoutine_Patch()
        {
            Instance?.Save();
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadFolderSave))]
        [HarmonyPrefix]
        private static void SaveManager_LoadRoutine_Patch()
        {
            Instance = null;
        }

        [HarmonyPatch(typeof(ThreadedGameLoader), nameof(ThreadedGameLoader.DeserializeInGameSettings))]
        [HarmonyPostfix]
        private static void ThreadedGameLoader_DeserializeInGameSettings_Patch(ref Task<InGameSettings> __result)
        {
            __result = __result.ContinueWith(t =>
            {
                TryLoadSaveSpecificSettings(t.Result);
                return t.Result;
            });
        }
    }
}