using BepInEx.Configuration;

namespace AutoHookGenPatcher.Config {
    public class PluginConfig
    {
        public ConfigEntry<bool> GenerateForAllPlugins;
        public ConfigEntry<bool> DisableGenerateForAllPlugins;
        public ConfigEntry<bool> ExtendedLogging;
        public PluginConfig(ConfigFile cfg)
        {
            GenerateForAllPlugins = cfg.Bind("Generate MMHOOK File for All Plugins",
                "Enabled", false,
                "If enabled, AutoHookGenPatcher will generate MMHOOK files for all plugins\n" +
                "even if their MMHOOK files were not referenced by other plugins.\n" +
                "Use this for getting the MMHOOK files you need for your plugin.");
            
            DisableGenerateForAllPlugins = cfg.Bind("Generate MMHOOK File for All Plugins",
                "Disable After Generating", true,
                "Automatically disable the above setting after the MMHOOK files have been generated.");
            
            ExtendedLogging = cfg.Bind("Extended Logging",
                "Enabled", false,
                "If enabled, AutoHookGenPatcher will print more information about what it is doing.");
            // ClearUnusedEntries(cfg);
        }

        // private void ClearUnusedEntries(ConfigFile cfg) {
        //     // Normally, old unused config entries don't get removed, so we do it with this piece of code. Credit to Kittenji.
        //     PropertyInfo orphanedEntriesProp = cfg.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);
        //     var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg, null);
        //     orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
        //     cfg.Save(); // Save the config file to save these changes
        // }
    }
}