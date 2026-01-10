using Kingmaker.UnitLogic.Levelup.Selections;
using Kingmaker.UnitLogic.Progression.Paths;

namespace RTAutoBuilder;

public class BuildPlan
{
    public int Version;
    public string UnitId = null!;
    public string? Homeworld;
    public string? Origin;
    public string FirstArchetype = null!;
    public string SecondArchetype = null!;
    public string BuildComment = string.Empty;
    public string BuildCode = null!;
    public Dictionary<string, List<PlanRankEntry>> Selections = [];
    public class PlanRankEntry
    {
        public string FeatureGroup = null!;
        public int Rank;
        public string? Selection;
    }

    public string? GetSelection(BlueprintCareerPath path, int rank, FeatureGroup featureGroup)
    {
        return Selections[path.AssetGuid].Where(x => x.Rank == rank && x.FeatureGroup == featureGroup.ToString()).FirstOrDefault()?.Selection;
    }
}
