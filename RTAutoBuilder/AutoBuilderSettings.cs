using Newtonsoft.Json;

namespace RTAutoBuilder;

public class AutoBuilderSettings
{
    private string GetFilePath()
    {
        return Path.Combine(Main.ModEntry.Path, $"{nameof(AutoBuilderSettings)}.json");
    }

    public List<string> KnownBuildCodes = [];
    public List<BuildPlan> BuildPlans = [];
    public void Save()
    {
        File.WriteAllText(GetFilePath(), JsonConvert.SerializeObject(this, Formatting.Indented));
    }
    public void Load()
    {
        var userPath = GetFilePath();
        if (File.Exists(userPath))
        {
            var content = File.ReadAllText(userPath);
            try
            {
                JsonConvert.PopulateObject(content, this);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"Failed to load user settings at {userPath}. Settings will be rebuilt.:\n{ex}");
                File.WriteAllText(userPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }
        else
        {
            Main.Log.Warning($"No Settings file found with path {userPath}, creating new.");
            File.WriteAllText(userPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
