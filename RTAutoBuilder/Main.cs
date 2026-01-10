using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Progression.Paths;
using Kingmaker.Utility.DotNetExtensions;
using Newtonsoft.Json;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace RTAutoBuilder;

[EnableReloading]
public static class Main
{
    internal static Harmony HarmonyInstance = null!;
    internal static UnityModManager.ModEntry.ModLogger Log = null!;
    internal static UnityModManager.ModEntry ModEntry = null!;
    public static AutoBuilderSettings Settings = null!;

    public static Dictionary<string, Dictionary<int, string>> CodeGuidMap = [];
    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        ModEntry = modEntry;
        Log = modEntry.Logger;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        Settings = new AutoBuilderSettings();
        Settings.Load();
        modEntry.OnGUI = OnGUI;
        if (!ReadMapping())
        {
            Log.Log("Could not read number->guid mappings needed for decoding build codes");
            return false;
        }

        try
        {
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }
        catch
        {
            HarmonyInstance.UnpatchAll(HarmonyInstance.Id);
            throw;
        }
        return true;
    }
    private static bool ReadMapping()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{nameof(RTAutoBuilder)}.facts_enumerated.json";
        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        using StreamReader reader = new(stream);
        var obj = reader.ReadToEnd();
        CodeGuidMap = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, string>>>(obj)!;
        if (CodeGuidMap == null || CodeGuidMap.Count == 0)
        {
            return false;
        }
        return true;
    }

    private static string inputText = "";
    private static string outputText = "";

    public static void OnGUI(UnityModManager.ModEntry modEntry)
    {
        var isInGame = IsInGame();
        var firstColumnWidth = GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent("Blademaster Master Tactician    ")).x);
        var labelWidth = GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent("Process Input")).x);
        var commentWidth = GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent("Comment should have enough space I think, more than enough")).x);
        GUILayout.BeginVertical(GUILayout.Width(1900));

        // Large text input
        GUILayout.Label("Input code:");
        inputText = GUILayout.TextField(
            inputText,
            GUILayout.Height(80)
        );
        GUILayout.Space(10);
        // Button below input
        if (GUILayout.Button("Process code", GUILayout.Height(40)))
        {
            ProcessCode(modEntry, inputText);
        }
        GUILayout.Space(10);
        GUILayout.Label(
                    outputText,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true)
                );
        GUILayout.Space(10);
        GUILayout.Space(20);
        var unitHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold
        };
        // --- BuildPlans Table ---
        GUILayout.Label("Build Plans:", unitHeaderStyle, GUILayout.ExpandWidth(true));

        GUILayout.Space(20);
        // Group by unitKey and sort
        var groupedPlans = Settings.BuildPlans
            .GroupBy(bp => bp.UnitId)
            .OrderBy(g => CharacterTools.GetRTCharacter(g.Key).Index);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Character/Build", unitHeaderStyle, firstColumnWidth);
        GUILayout.Label("Status", unitHeaderStyle, labelWidth);
        GUILayout.Label("", unitHeaderStyle, labelWidth);
        GUILayout.Label("", unitHeaderStyle, labelWidth);
        GUILayout.Label("Your comment", unitHeaderStyle, commentWidth);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        foreach (var group in groupedPlans)
        {
            // Unit header
            GUILayout.Label(CharacterTools.GetName(group.Key), unitHeaderStyle, firstColumnWidth);

            // Builds for this unit
            foreach (var plan in group.OrderBy(p => p.BuildCode))
            {
                GUILayout.BeginHorizontal();
                var firstArch = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(plan.FirstArchetype).Name;
                var secondArch = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(plan.SecondArchetype).Name;

                GUILayout.Label($"{firstArch} {secondArch}", firstColumnWidth);

                string statusText;
                var statusStyle = new GUIStyle(GUI.skin.label);
                if (!isInGame)
                {
                    statusText = "Not in Game";
                }
                else
                {
                    if (SaveSpecificSettings.Instance?.AppliedBuilds.TryGetValue(plan.UnitId, out var appliedPlan) == true)
                    {
                        if (appliedPlan == plan.BuildCode)
                        {
                            statusText = "Active";
                            statusStyle.normal.textColor = Color.green;
                        }
                        else
                        {
                            statusText = "Inactive";
                        }
                    }
                    else
                    {
                        statusText = "Inactive";
                    }
                }
                GUILayout.Label(statusText, statusStyle, labelWidth);

                GUI.enabled = isInGame;
                if (GUILayout.Button("Activate", labelWidth))
                {
                    SaveSpecificSettings.Instance?.AppliedBuilds[plan.UnitId] = plan.BuildCode;
                }
                GUI.enabled = true;

                if (GUILayout.Button("Delete", labelWidth))
                {
                    if (SaveSpecificSettings.Instance?.AppliedBuilds.TryGetValue(plan.UnitId, out var appliedPlan) == true)
                    {
                        if (appliedPlan == plan.BuildCode)
                        {
                            SaveSpecificSettings.Instance?.AppliedBuilds.Remove(plan.UnitId);
                        }
                    }
                    Settings.BuildPlans.RemoveAll(x => x.BuildCode == plan.BuildCode);
                }

                plan.BuildComment = GUILayout.TextArea(plan.BuildComment, commentWidth);

                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
        }

        GUILayout.EndVertical();
        Settings.Save();
    }

    public static bool IsInGame()
    {
        return Game.Instance.Player?.Party?.Count > 0;
    }
    private static void ProcessCode(UnityModManager.ModEntry modEntry, string text)
    {
        Log.Log($"Code received: {text}");
        BuildPlan plan;
        try
        {
            plan = BuildCodeDecoder.Decode(text);
        }
        catch (Exception e)
        {
            Log.LogException(e);
            outputText = $"Invalid code: {e.Message}";
            return;
        }
        inputText = string.Empty;
        outputText = $"Added plan for {CharacterTools.GetName(plan.UnitId)}";
        if (!Settings.BuildPlans.Any(x => x.BuildCode == text))
        {
            Settings.BuildPlans.Add(plan);
            Settings.Save();
        }
        else
        {
            outputText = "This plan is already loaded";
        }
    }
}
