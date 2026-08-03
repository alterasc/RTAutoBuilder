using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
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

    internal static float UIScale = -1f;
    private static GUILayoutOption FirstColumnWidth = null!;
    private static GUILayoutOption LabelWidth = null!;
    private static GUILayoutOption CommentWidth = null!;
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
        try
        {
            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            using StreamReader reader = new(stream);
            var obj = reader.ReadToEnd();
            CodeGuidMap = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, string>>>(obj)!;
        }
        catch (Exception e)
        {
            Log.LogException($"Could not read number->guid mappings needed for decoding build codes: {e.Message}", e);
        }
        if (CodeGuidMap == null || CodeGuidMap.Count == 0)
        {
            return false;
        }
        return true;
    }

    private static string inputText = "";
    private static string outputText = "";

    private static void LayoutHorizontal(Action body)
    {
        GUILayout.BeginHorizontal();
        try
        {
            body();
        }
        catch (Exception e)
        {
            Log.LogException(e);
        }
        finally
        {
            GUILayout.EndHorizontal();
        }
    }

    public static void OnGUI(UnityModManager.ModEntry modEntry)
    {
        var isInGame = IsInGame();
        if (UnityModManager.UI.Instance.mUIScale != UIScale || FirstColumnWidth == null)
        {
            UIScale = UnityModManager.UI.Instance.mUIScale;
            FirstColumnWidth = GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent("Blademaster Master Tactician    ")).x);
            LabelWidth = GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent("Copy build code111111")).x);
            CommentWidth = GUILayout.Width(GUI.skin.label
                .CalcSize(new GUIContent("Comment should have enough space I think, more than enough")).x);
        }
        GUILayout.BeginVertical(GUILayout.Width(1900));

        DrawInputSection(modEntry);
        DrawTableHeader(FirstColumnWidth, LabelWidth, CommentWidth);

        var entries = GetDisplayEntries(isInGame);
        foreach (var entry in entries.OrderBy(x => (int)x.Character))
        {
            DrawSeparator();
            DrawCharacterRow(entry, FirstColumnWidth, LabelWidth, CommentWidth);
            DrawPlanRows(entry, FirstColumnWidth, LabelWidth, CommentWidth);
            GUILayout.Space(10);
        }
        GUILayout.EndVertical();
    }

    private static void DrawInputSection(UnityModManager.ModEntry modEntry)
    {
        GUILayout.Label("Input code:");
        inputText = GUILayout.TextField(inputText, GUILayout.Height(80));
        GUILayout.Space(10);
        if (GUILayout.Button("Process code", GUILayout.Height(40)))
        {
            ProcessCode(modEntry, inputText);
        }
        GUILayout.Space(10);
        GUILayout.Label(outputText, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.Space(10);
        GUILayout.Space(20);
    }

    private static void DrawTableHeader(GUILayoutOption firstColumnWidth, GUILayoutOption labelWidth, GUILayoutOption commentWidth)
    {
        var unitHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold
        };
        GUILayout.Label("Build Plans:", unitHeaderStyle, GUILayout.ExpandWidth(true));
        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Character/Build", unitHeaderStyle, firstColumnWidth);
        GUILayout.Label("Status", unitHeaderStyle, labelWidth);
        GUILayout.Label("", unitHeaderStyle, labelWidth);
        GUILayout.Label("", unitHeaderStyle, labelWidth);
        GUILayout.Label("Your comment", unitHeaderStyle, commentWidth);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.Space(10);
    }

    private static void DrawSeparator()
    {
        Rect r = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
        GUI.color = Color.gray;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUILayout.Space(3);
    }

    private static List<CharacterDisplayEntry> GetDisplayEntries(bool isInGame)
    {
        List<CharacterDisplayEntry> charactersToDisplay = [];

        if (isInGame)
        {
            foreach (var member in Game.Instance.Player.AllCharacters.Where(x => !x.IsPet))
            {
                try
                {
                    var enumChar = CharacterTools.GetChar(member);
                    charactersToDisplay.Add(new CharacterDisplayEntry(enumChar, true, member));
                }
                catch (Exception e)
                {
                    Log.LogException("Unknown character", e);
                }
            }
        }
        foreach (var plan in Settings.BuildPlans)
        {
            if (Enum.TryParse<CharacterEnum>(plan.UnitId, out var enumChar) && !charactersToDisplay.Any(x => x.Character == enumChar))
            {
                charactersToDisplay.Add(new CharacterDisplayEntry(enumChar, false, null));
            }
        }
        return charactersToDisplay;
    }

    private static string GetCharacterLabel(CharacterDisplayEntry entry)
    {
        var rtCharacter = entry.Character;
        var unit = entry.Unit;
        if (unit != null)
        {
            return unit.CharacterName;
        }
        else if (rtCharacter == CharacterEnum.CustomCompanion)
        {
            return "Mercenary";
        }
        else
        {
            return CharacterTools.GetName(rtCharacter.ToString());
        }
    }

    private static void DrawCharacterRow(CharacterDisplayEntry entry, GUILayoutOption firstColumnWidth, GUILayoutOption labelWidth, GUILayoutOption commentWidth)
    {
        var rtCharacter = entry.Character;
        bool isMercenary = rtCharacter is CharacterEnum.CustomCompanion or CharacterEnum.CustomNavigator;
        var isInParty = entry.IsInParty;
        var unit = entry.Unit;

        var unitHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold
        };

        LayoutHorizontal(() =>
        {
            string label = GetCharacterLabel(entry);
            GUILayout.Label(label, unitHeaderStyle, firstColumnWidth);
            if (isInParty && unit != null && !isMercenary)
            {
                GUILayout.Label("", labelWidth);
                GUILayout.Label("", labelWidth);

                if (GUILayout.Button("Copy build code", labelWidth))
                {
                    try
                    {
                        var code = Exporter.ExportCompanions(unit);
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
                        var code = Exporter.ExportCompanions(unit);
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
                        var code = Exporter.ExportCompanions(unit);
                        var build = BuildCodeDecoder.Decode(code);
                        build.BuildComment = $"Saved on {DateTime.Now.ToString()}, game version {GameVersion.GetVersion()}";
                        if (unit.Progression.CharacterLevel < 55)
                        {
                            build.BuildComment += $", up to level {unit.Progression.CharacterLevel}";
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
        });
        GUILayout.Space(6);
    }

    private static void DrawPlanRows(CharacterDisplayEntry entry, GUILayoutOption firstColumnWidth, GUILayoutOption labelWidth, GUILayoutOption commentWidth)
    {
        var rtCharacter = entry.Character;
        bool isMercenary = rtCharacter is CharacterEnum.CustomCompanion or CharacterEnum.CustomNavigator;
        var isInParty = entry.IsInParty;
        var unit = entry.Unit;
        var isInGame = IsInGame();

        foreach (var plan in Settings.BuildPlans.Where(x => x.UnitId == rtCharacter.ToString()).OrderBy(x => x.BuildCode))
        {
            LayoutHorizontal(() =>
            {
                var firstArch = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(plan.FirstArchetype)?.Name;
                var secondArch = ResourcesLibrary.TryGetBlueprint<BlueprintCareerPath>(plan.SecondArchetype)?.Name;
                if (rtCharacter == CharacterEnum.RogueTrader || isMercenary)
                {
                    var homeworld = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(plan.Homeworld)?.Name;
                    var origin = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(plan.Origin)?.Name;
                    GUILayout.Label($"{homeworld} {origin} {firstArch} {secondArch}", firstColumnWidth);
                }
                else
                {
                    GUILayout.Label($"{firstArch} {secondArch}", firstColumnWidth);
                }

                var status = GetPlanStatus(plan, entry, isInGame, isMercenary, unit);
                GUILayout.Label(status.Text, status.Style, labelWidth);

                DrawActivateButton(plan, isMercenary, unit, isInGame, status.IsActive, labelWidth);
                DrawCopyCodeButton(plan, labelWidth);
                DrawCopyUrlButton(plan, labelWidth);
                DrawDeleteButton(plan, unit, labelWidth);
                plan.BuildComment = GUILayout.TextArea(plan.BuildComment, commentWidth);
                DrawCopyAsMercButton(plan, rtCharacter, labelWidth);
            });
        }
    }

    private static (string Text, GUIStyle Style, bool IsActive) GetPlanStatus(BuildPlan plan, CharacterDisplayEntry entry, bool isInGame, bool isMercenary, BaseUnitEntity? unit)
    {
        var statusStyle = new GUIStyle(GUI.skin.label);
        if (!isInGame)
        {
            return ("Not in Game", statusStyle, false);
        }

        var settings = SaveSpecificSettings.Instance;
        if (settings == null || unit == null)
        {
            return ("Inactive", statusStyle, false);
        }

        bool hasPlan;
        string appliedPlan;

        hasPlan = settings.AppliedBuilds.TryGetValue(unit.UniqueId, out appliedPlan)
            || settings.AppliedBuilds.TryGetValue(plan.UnitId, out appliedPlan);


        if (hasPlan && appliedPlan == plan.BuildCode)
        {
            statusStyle.normal.textColor = Color.green;
            return ("Active", statusStyle, true);
        }

        return ("Inactive", statusStyle, false);
    }

    private static void DrawActivateButton(BuildPlan plan, bool isMercenary, BaseUnitEntity? unit, bool isInGame, bool codeActive, GUILayoutOption labelWidth)
    {
        GUI.enabled = isInGame;
        try
        {
            var buttonText = codeActive ? "Deactivate" : "Activate";
            if (GUILayout.Button(buttonText, labelWidth))
            {
                if (unit != null)
                {
                    if (codeActive)
                    {
                        SaveSpecificSettings.Instance?.AppliedBuilds.Remove(unit.UniqueId);
                        SaveSpecificSettings.Instance?.AppliedBuilds.Remove(plan.UnitId);
                    }
                    else
                    {
                        SaveSpecificSettings.Instance!.AppliedBuilds[unit.UniqueId] = plan.BuildCode;
                    }
                }
            }
        }
        finally
        {
            GUI.enabled = true;
        }
    }

    private static void DrawCopyCodeButton(BuildPlan plan, GUILayoutOption labelWidth)
    {
        if (GUILayout.Button("Copy build code", labelWidth))
        {
            try
            {
                GUIUtility.systemCopyBuffer = plan.BuildCode;
            }
            catch (Exception e)
            {
                Log.LogException(e);
                outputText = e.Message;
            }
        }
    }

    private static void DrawCopyUrlButton(BuildPlan plan, GUILayoutOption labelWidth)
    {
        if (GUILayout.Button("Copy build url", labelWidth))
        {
            try
            {
                var finalUrl = $"https://rt-planner.pages.dev/?buildCode={plan.BuildCode}";
                GUIUtility.systemCopyBuffer = finalUrl;
            }
            catch (Exception e)
            {
                Log.LogException(e);
                outputText = e.Message;
            }
        }
    }

    private static void DrawDeleteButton(BuildPlan plan, BaseUnitEntity? unit, GUILayoutOption labelWidth)
    {
        if (GUILayout.Button("Delete", labelWidth))
        {
            var saveKey = unit?.UniqueId ?? plan.UnitId;
            if (SaveSpecificSettings.Instance?.AppliedBuilds.TryGetValue(saveKey, out var appliedPlan) == true
                || (unit != null && SaveSpecificSettings.Instance?.AppliedBuilds.TryGetValue(plan.UnitId, out appliedPlan) == true))
            {
                if (appliedPlan == plan.BuildCode)
                {
                    SaveSpecificSettings.Instance?.AppliedBuilds.Remove(saveKey);
                }
            }
            Settings.BuildPlans.RemoveAll(x => x.BuildCode == plan.BuildCode);
            Settings.Save();
        }
    }

    private static void DrawCopyAsMercButton(BuildPlan plan, CharacterEnum rtCharacter, GUILayoutOption labelWidth)
    {
        if (rtCharacter != CharacterEnum.RogueTrader)
        {
            return;
        }

        if (GUILayout.Button("Copy as merc", labelWidth))
        {
            var newCode = BuildCodeDecoder.ReencodeAsCharacter(plan.BuildCode, CharacterEnum.CustomCompanion);
            var existing = Settings.BuildPlans.Any(x => x.BuildCode == newCode);
            if (existing)
            {
                outputText = "Plan already exists for mercenary";
            }
            else
            {
                var copy = BuildCodeDecoder.Decode(newCode);
                if (!string.IsNullOrEmpty(plan.BuildComment))
                {
                    copy.BuildComment = $"From: {plan.BuildComment}";
                }
                Settings.BuildPlans.Add(copy);
                Settings.Save();
                outputText = "Copied plan to mercenary";
            }
        }
    }

    public static bool IsInGame()
    {
        return Game.Instance.Player?.Party?.Count > 0;
    }
    private static void ProcessCode(UnityModManager.ModEntry modEntry, string text)
    {
        var trimmedText = text.Trim();
        Log.Log($"Code received: {trimmedText}");
        BuildPlan plan;
        try
        {
            plan = BuildCodeDecoder.Decode(trimmedText);
        }
        catch (Exception e)
        {
            Log.LogException(e);
            outputText = $"Invalid code: {e.Message}";
            return;
        }
        inputText = string.Empty;
        outputText = $"Added plan for {CharacterTools.GetName(plan.UnitId)}";
        if (!Settings.BuildPlans.Any(x => x.BuildCode == trimmedText))
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