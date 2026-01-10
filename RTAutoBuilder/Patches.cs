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

                var rtCharacter = CharacterTools.GetRTCharacter(__instance.Unit);
                var instance = SaveSpecificSettings.Instance;
                if (instance == null || !instance.AppliedBuilds.TryGetValue(rtCharacter.Id, out var buildCode))
                {
                    return;
                }
                var plan = Main.Settings.BuildPlans.FirstOrDefault(x => x.BuildCode == buildCode);
                if (plan == null)
                {
                    try
                    {
                        plan = BuildCodeDecoder.Decode(buildCode);
                    }
                    catch (Exception e)
                    {
                        Main.Log.Error($"Couldn't decode plan: {buildCode}. Removed it from preselection. Error: {e}");
                        instance.AppliedBuilds.Remove(rtCharacter.Id);
                        return;
                    }
                }

                foreach (var item in arr)
                {
                    var career = item.CareerPathVM.CareerPath;
                    var group = item.FeatureGroup;
                    var rank = item.Rank;
                    var toSelect = plan.GetSelection(career, rank, group);
                    if (!string.IsNullOrEmpty(toSelect))
                    {
                        var preselection = item.m_ShowGroupList
                            .Where(x => x.FeatureList != null)
                            .SelectMany(x => x.FeatureList)
                            .FirstOrDefault(x => x.Feature != null && x.Feature.AssetGuid == toSelect);
                        if (preselection != null)
                        {
                            Main.Log.Log($"Found selection for career: {career}, at rank {rank}, group: {group}, found: {toSelect}");
                            preselection.Select();
                        }
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
