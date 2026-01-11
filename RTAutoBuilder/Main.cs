using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.GameInfo;
using Kingmaker.UnitLogic.Progression.Features;
using Kingmaker.UnitLogic.Progression.Paths;
using Kingmaker.Utility.DotNetExtensions;
using Newtonsoft.Json;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace RTAutoBuilder;

#if DEBUG
[EnableReloading]
#endif
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
        var labelWidth = GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent("Copy build code111111")).x);
        var exportWidth = GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent("Export Code")).x * 2);
        var commentWidth = GUILayout.Width(GUI.skin.label
            .CalcSize(new GUIContent("Comment should have enough space I think, more than enough")).x);
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


        GUILayout.BeginHorizontal();
        GUILayout.Label("Character/Build", unitHeaderStyle, firstColumnWidth);
        GUILayout.Label("Status", unitHeaderStyle, labelWidth);
        GUILayout.Label("", unitHeaderStyle, labelWidth);
        GUILayout.Label("", unitHeaderStyle, labelWidth);
        GUILayout.Label("Your comment", unitHeaderStyle, commentWidth);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.Space(10);

        List<string> party = [];

        Dictionary<string, (RTCharacter character, bool isInParty)> charactersToDisplay = [];

        if (isInGame)
        {
            foreach (var member in Game.Instance.Player.Party)
            {
                try
                {
                    var c = CharacterTools.GetRTCharacter(member);
                    charactersToDisplay[c.Id] = (c, true);
                }
                catch (Exception e)
                {
                    Log.LogException("Unknown character", e);
                }
            }
        }
        foreach (var plan in Settings.BuildPlans)
        {
            if (!charactersToDisplay.ContainsKey(plan.UnitId))
            {
                charactersToDisplay[plan.UnitId] = (CharacterTools.GetRTCharacter(plan.UnitId), false);
            }
        }

        foreach (var entry in charactersToDisplay.OrderBy(x => x.Value.character.Index))
        {
            Rect r = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
            GUI.color = Color.gray;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Space(3);
            var rtCharacter = entry.Value.character;
            var isInParty = entry.Value.isInParty;
            GUILayout.BeginHorizontal();
            // Unit header
            GUILayout.Label(rtCharacter.LocalizedName, unitHeaderStyle, firstColumnWidth);
            if (isInParty)
            {
                GUILayout.Label("", labelWidth);
                GUILayout.Label("", labelWidth);
                var uni = Game.Instance.Player.Party.First(x => CharacterTools.GetRTCharacter(x).Id == rtCharacter.Id);

                if (GUILayout.Button("Copy build code", labelWidth))
                {
                    try
                    {
                        var code = Exporter.ExportCompanions(uni);
                        GUIUtility.systemCopyBuffer = code;
                    }
                    catch (Exception e)
                    {
                        Log.LogException(e);
                        outputText = e.Message;
                    }
                }
                if (GUILayout.Button("Copy build url", labelWidth))
                {
                    try
                    {
                        var code = Exporter.ExportCompanions(uni);
                        var finalUrl = $"https://rt-planner.pages.dev/?buildCode={code}";
                        GUIUtility.systemCopyBuffer = finalUrl;
                    }
                    catch (Exception e)
                    {
                        Log.LogException(e);
                        outputText = e.Message;
                    }
                }
                GUILayout.Label("", labelWidth);
                if (GUILayout.Button("Save as plan", labelWidth))
                {
                    try
                    {
                        var code = Exporter.ExportCompanions(uni);
                        var build = BuildCodeDecoder.Decode(code);
                        build.BuildComment = $"Saved on {DateTime.Now.ToString()}, game version {GameVersion.GetVersion()}";
                        if (uni.Progression.CharacterLevel < 55)
                        {
                            build.BuildComment += $", up to level {uni.Progression.CharacterLevel}";
                        }
                        if (!Settings.BuildPlans.Any(x => x.BuildCode == code))
                        {
                            Settings.BuildPlans.Add(build);
                            Settings.Save();
                        }
                        else
                        {
                            outputText = "This plan is already loaded";
                        }
                    }
                    catch (Exception e)
                    {
                        Log.LogException(e);
                        outputText = e.Message;
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            foreach (var plan in Settings.BuildPlans.Where(x => x.UnitId == rtCharacter.Id).OrderBy(x => x.BuildCode))
            {
                GUILayout.BeginHorizontal();
                var firstArch = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(plan.FirstArchetype).Name;
                var secondArch = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(plan.SecondArchetype).Name;
                if (plan.UnitId == CharacterTools.MAIN_CHARACTER_ID)
                {
                    var homeworld = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(plan.Homeworld)?.Name;
                    var origin = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(plan.Origin)?.Name;
                    GUILayout.Label($"{homeworld} {origin} {firstArch} {secondArch}", firstColumnWidth);
                }
                else
                {
                    GUILayout.Label($"{firstArch} {secondArch}", firstColumnWidth);
                }
                string statusText;
                var statusStyle = new GUIStyle(GUI.skin.label);
                var codeActive = false;
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
                            codeActive = true;
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
                var buttonText = codeActive ? "Deactivate" : "Activate";
                if (GUILayout.Button(buttonText, labelWidth))
                {
                    if (codeActive)
                    {
                        SaveSpecificSettings.Instance?.AppliedBuilds.Remove(plan.UnitId);
                    }
                    else
                    {
                        SaveSpecificSettings.Instance?.AppliedBuilds[plan.UnitId] = plan.BuildCode;
                    }
                }
                GUI.enabled = true;
                if (GUILayout.Button("Copy build code", labelWidth))
                {
                    try
                    {
                        var code = plan.BuildCode;
                        GUIUtility.systemCopyBuffer = code;
                    }
                    catch (Exception e)
                    {
                        Log.LogException(e);
                        outputText = e.Message;
                    }
                }
                if (GUILayout.Button("Copy build url", labelWidth))
                {
                    try
                    {
                        var code = plan.BuildCode;
                        var finalUrl = $"https://rt-planner.pages.dev/?buildCode={code}";
                        GUIUtility.systemCopyBuffer = finalUrl;
                    }
                    catch (Exception e)
                    {
                        Log.LogException(e);
                        outputText = e.Message;
                    }
                }
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
