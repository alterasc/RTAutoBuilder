using HarmonyLib;
using Kingmaker.UI.MVVM.VM.ServiceWindows.CharacterInfo.Sections.Careers.CareerPath;

namespace RTAutoBuilder;

public static class Patches
{
    /**
     * Preselects options during leveling
     */
    [HarmonyPatch(typeof(CareerPathVM), nameof(CareerPathVM.UpdateCareerPath))]
    internal static class CareerPathVM_Patches
    {
        [HarmonyPostfix]
        internal static void UpdateCareerPath_Postfix(CareerPathVM __instance)
        {
            if (__instance.AvailableSelections == null)
            {
                return;
            }
            var arr = __instance.AvailableSelections.ToList();
            if (arr.Count == 0)
            {
                return;
            }
            try
            {
                var character = CharacterTools.GetChar(__instance.Unit);
                var unit = __instance.Unit;
                var instance = SaveSpecificSettings.Instance;
                if (instance == null)
                {
                    return;
                }
                if (!instance.AppliedBuilds.TryGetValue(unit.UniqueId, out var buildCode)
                    && !instance.AppliedBuilds.TryGetValue(character.ToString(), out buildCode))
                {
                    return;
                }
                var plan = Main.Settings.BuildPlans.FirstOrDefault(x => x.BuildCode == buildCode);
                if (plan == null)
                {
                    instance.AppliedBuilds.Remove(unit.UniqueId);
                    instance.AppliedBuilds.Remove(character.ToString());
                    Main.Log.Log($"Removed code {buildCode} from save, because it is no longer present in global settings");
                    return;
                }

                foreach (var item in arr)
                {
                    var career = item.CareerPathVM.CareerPath;
                    var group = item.FeatureGroup;
                    var rank = item.Rank;
                    var toSelect = plan.GetSelection(career, rank, group);
                    if (!string.IsNullOrEmpty(toSelect))
                    {
                        item.UpdateFeatures();
                        var preselection = item.m_ShowGroupList
                            .Where(x => x.FeatureList != null)
                            .SelectMany(x => x.FeatureList)
                            .FirstOrDefault(x => x.Feature != null && x.Feature.AssetGuid == toSelect);
                        if (preselection != null)
                        {
                            Main.Log.Log($"Found selection for career: {career}, at rank {rank}, group: {group}, found: {toSelect}");
                            preselection.Select();
                        }
                        else
                        {
                            var groupList = item.m_ShowGroupList;
                            if (groupList == null)
                            {
                                Main.Log.Log($"Couldn't find selection for career: {career}, at rank {rank}, group: {group}, expected: {toSelect} — m_ShowGroupList is NULL");
                            }
                            else
                            {
                                var allGuids = groupList
                                    .Where(x => x.FeatureList != null)
                                    .SelectMany(x => x.FeatureList)
                                    .Where(x => x.Feature != null)
                                    .Select(x => x.Feature.AssetGuid)
                                    .ToList();
                                Main.Log.Log($"Couldn't find selection for career: {career}, at rank {rank}, group: {group}, expected: {toSelect} — {groupList.Count} group(s), {allGuids.Count} feature(s) available: [{string.Join(", ", allGuids)}]");
                            }
                        }
                    }
                    else
                    {
                        Main.Log.Log($"No selection for career: {career}, at rank {rank}, group: {group}");
                    }
                }
            }
            catch (Exception e)
            {
                Main.Log.LogException("Couldn't apply plan", e);
            }
        }
    }
}
