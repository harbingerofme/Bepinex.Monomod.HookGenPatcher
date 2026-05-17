using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using AutoHookGenPatcher.Config;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace AutoHookGenPatcher;

// [PatcherPluginInfo(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
// internal class Patcher : BasePatcher {
internal static class Patcher {
    internal static string mmhookPath = null!;
    internal static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.PLUGIN_NAME);
    private static readonly PluginConfig BoundConfig = new PluginConfig(new ConfigFile(Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_NAME + ".cfg"), false));
    internal static List<CachedAssemblyInfo> currentPlugins = new();
    internal static List<Task> hookGenTasks = new();
    internal static List<string> mmhookOverrides = new();
    internal static List<string> mmHookFiles = new();
    internal static Dictionary<string, Version> GUIDtoVer = new();
    internal static string? cacheLocation;
    internal static bool isCacheUpdateNeeded = false;
    internal const int cacheVersionID = 2;
    public static void Initialize()
    {
        if(BoundConfig.ExtendedLogging.Value)
        {
            Logger.LogInfo($"[{nameof(Initialize)}] Extended Logging is enabled.");
            if(BoundConfig.GenerateForAllPlugins.Value)
                Logger.LogInfo($"[{nameof(Initialize)}] GenerateForAllPlugins is enabled.");
        }

        mmhookOverrides.Add("Assembly-CSharp");

        mmhookPath = Path.Combine(Paths.PluginPath, "MMHOOK");
        cacheLocation = Path.Combine(Paths.CachePath, "AutoHookGenPatcher_MMHOOK_Cache.xml");

        mmHookFiles = Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories).Where(name => Path.GetFileName(name).StartsWith("MMHOOK_")).ToList();

        if(!Directory.Exists(mmhookPath))
            Directory.CreateDirectory(mmhookPath);

        var watch = Stopwatch.StartNew();
        
        Begin();

        watch.Stop();
        
        ExtendedLogging($"[{nameof(Initialize)}] Took {watch.ElapsedMilliseconds}ms");
    }

    internal static void ExtendedLogging(string message){
        if(BoundConfig.ExtendedLogging.Value) Logger.LogInfo(message);
    }

    private static void Begin()
    {
        var cachedPlugins = TryLoadCache();

        currentPlugins = GetPlugins(cachedPlugins).ToList();

        for(int i = 0; i < currentPlugins.Count; i++)
        {
            var plugin = currentPlugins[i];
            if(IsPluginInfoUpToDate(plugin))
                MarkIfDuplicate(plugin, plugin.GUID, plugin.PluginVersion);
            else
            {
                ReadPluginInfoAndMMHOOKReferences(plugin);
                isCacheUpdateNeeded = true;
            }
        }

        int alreadyHadMMHOOKCount = 0;
        int totalValidPluginsCount = currentPlugins.Count;

        for(int i = 0; i < currentPlugins.Count; i++)
        {
            var plugin = currentPlugins[i];
            if(plugin.IsDuplicate){
                ExtendedLogging($"[{nameof(Begin)}] Skipping HookGen Check For Duplicate or Old Version of {plugin.GUID} ({plugin.PluginVersion})");
                totalValidPluginsCount--;
                continue;
            }

            if(plugin.BadImageFormat){
                ExtendedLogging($"[{nameof(Begin)}] Skipping HookGen Check For {plugin.GUID} Because {nameof(BadImageFormatException)}");
                totalValidPluginsCount--;
                continue;
            }

            if(plugin.AlreadyHasMMHOOK){
                alreadyHadMMHOOKCount++;
                // ExtendedLogging($"[{nameof(Begin)}] Assembly Already Has MMHOOK: " + plugin.GUID);
                continue;
            }

            if(BoundConfig.GenerateForAllPlugins.Value || mmhookOverrides.Contains(plugin.GUID) || currentPlugins.SelectMany(plugins => plugins.References).Contains(plugin.GUID))
            {
                var hookGenTask = new Task(() => StartHookGen(plugin));
                hookGenTask.Start();
                hookGenTasks.Add(hookGenTask);
            }
        }

        if(BoundConfig.GenerateForAllPlugins.Value && BoundConfig.DisableGenerateForAllPlugins.Value)
        {
            Logger.LogInfo($"[{nameof(Begin)}] DisableGenerateForAllPlugins is enabled, disabling GenerateForAllPlugins.");
            BoundConfig.GenerateForAllPlugins.Value = false;
        }
        
        ExtendedLogging($"[{nameof(Begin)}] Already Has MMHOOK: {alreadyHadMMHOOKCount} Out of {totalValidPluginsCount} Assemblies");
        ExtendedLogging($"[{nameof(Begin)}] Waiting for {hookGenTasks.Count} HookGen Task{(hookGenTasks.Count == 1 ? "" : "s")} to finish.");

        hookGenTasks.ForEach(x => x.Wait());
        
        if(isCacheUpdateNeeded || cachedPlugins == null)
            WriteCache(currentPlugins);
    }

    private static void StartHookGen(CachedAssemblyInfo plugin)
    {
        if(HookGenPatch.RunHookGen(plugin)){
            lock(plugin){
                plugin.AlreadyHasMMHOOK = true;
            }
            isCacheUpdateNeeded = true;
        }
    }

    private static IEnumerable<CachedAssemblyInfo> GetPlugins(List<CachedAssemblyInfo>? cachedAssemblies)
    {
        List<string> paths = new();
        Directory.GetFiles(Paths.ManagedPath, "*.dll", SearchOption.AllDirectories).ToList().ForEach(paths.Add);
        Directory.GetFiles(Paths.BepInExAssemblyDirectory, "*.dll", SearchOption.AllDirectories).ToList().ForEach(paths.Add);
        Directory.GetFiles(Paths.PatcherPluginPath, "*.dll", SearchOption.AllDirectories).ToList().ForEach(paths.Add);
        Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories).ToList().ForEach(paths.Add);

        var assemblyPaths = paths.Where(name => !Path.GetFileName(name).StartsWith("MMHOOK_"));

        List<string> mmhookFileNames = new();
        mmHookFiles.ForEach(x => mmhookFileNames.Add(Path.GetFileName(x)));

        foreach (string assemblyPath in assemblyPaths)
        {
            CachedAssemblyInfo? cachedAssembly = cachedAssemblies?.Find(ass => ass.Path.Equals(assemblyPath));

            if(cachedAssembly == null)
            {
                ExtendedLogging($"[{nameof(GetPlugins)}] Found New Assembly: " + Path.GetFileName(assemblyPath));
                yield return new CachedAssemblyInfo(assemblyPath);
            }
            else
            {
                var hasMMHOOKinReality = mmhookFileNames.Contains($"MMHOOK_{cachedAssembly.GUID}.dll");
                // ExtendedLogging($"[{nameof(GetPlugins)}] Found Known Assembly: " + Path.GetFileName(assemblyPath));
                yield return new CachedAssemblyInfo(cachedAssembly.GUID, assemblyPath, cachedAssembly.DateModified, cachedAssembly.MMHOOKDate, cachedAssembly.References, cachedAssembly.PluginVersion.ToString(), cachedAssembly.BadImageFormat, hasMMHOOKinReality);
            }
        }
    }

    private static bool IsPluginInfoUpToDate(CachedAssemblyInfo cachedAssemblyInfo)
    {
        var currentDateModified = File.GetLastWriteTime(cachedAssemblyInfo.Path).Ticks;

        if(currentDateModified == cachedAssemblyInfo.DateModified)
        {
            // ExtendedLogging($"[{nameof(IsPluginInfoUpToDate)}] Assembly is up-to-date: " + Path.GetFileName(cachedAssemblyInfo.Path));
            if(!cachedAssemblyInfo.AlreadyHasMMHOOK)
                return true;
            // TODO: This is horribly unoptimized, we already get MMHOOK file's path in GetPlugins. Fix this.
            var thisMMHOOKPath = mmHookFiles.FirstOrDefault(filePath => Path.GetFileNameWithoutExtension(filePath).EndsWith(cachedAssemblyInfo.GUID));
            if (thisMMHOOKPath is null){
                Logger.LogError($"[{nameof(IsPluginInfoUpToDate)}] Plugin {cachedAssemblyInfo.GUID}'s MMHOOK was found yet it couldn't be found, this shouldn't be possible.");
                cachedAssemblyInfo.AlreadyHasMMHOOK = false;
                return false;
            }

            var currentMMHOOKDate = File.GetLastWriteTime(thisMMHOOKPath).Ticks;
            if(cachedAssemblyInfo.MMHOOKDate != currentMMHOOKDate){
                ExtendedLogging($"[{nameof(IsPluginInfoUpToDate)}] Plugin {cachedAssemblyInfo.GUID} is up-to-date, but MMHOOK is outdated.");
                cachedAssemblyInfo.AlreadyHasMMHOOK = false;
            }
            return true;
        }

        // ExtendedLogging($"[{nameof(IsPluginInfoUpToDate)}] Cached Assembly Info is Not up-to-date! " + Path.GetFileName(cachedAssemblyInfo.Path));
        cachedAssemblyInfo.DateModified = currentDateModified;
        cachedAssemblyInfo.AlreadyHasMMHOOK = false;
        return false;
    }

    private static void ReadPluginInfoAndMMHOOKReferences(CachedAssemblyInfo cachedAssemblyInfo)
    {
        try
        {
            using var pluginAssembly = AssemblyDefinition.ReadAssembly(cachedAssemblyInfo.Path);

            cachedAssemblyInfo.GUID = GetPluginGUID(cachedAssemblyInfo, pluginAssembly);
            if (cachedAssemblyInfo.IsDuplicate)
            {
                ExtendedLogging($"[{nameof(ReadPluginInfoAndMMHOOKReferences)}] Skipping Reading This Version of {cachedAssemblyInfo.GUID}");
                return;
            }
            ExtendedLogging($"[{nameof(ReadPluginInfoAndMMHOOKReferences)}] Starting Reading: " + cachedAssemblyInfo.GUID);

            var mmhookReferences = pluginAssembly.MainModule.AssemblyReferences.Where(x => x.Name.StartsWith("MMHOOK_"));
            cachedAssemblyInfo.References.Clear();

            foreach (var referencedPlugin in mmhookReferences)
            {
                ExtendedLogging($"[{nameof(ReadPluginInfoAndMMHOOKReferences)}] Found Reference to {referencedPlugin.Name} in {cachedAssemblyInfo.GUID}.");
                // Trim out 'MMHOOK_' to get the GUID (the rest of the name, excluding '.dll')
                var referencedPlugin_GUID = referencedPlugin.Name.Substring(7, referencedPlugin.Name.Length - 7);
                cachedAssemblyInfo.References.Add(referencedPlugin_GUID);
            }
        }
        catch (BadImageFormatException)
        {
            ExtendedLogging($"[{nameof(ReadPluginInfoAndMMHOOKReferences)}] {nameof(BadImageFormatException)}: {cachedAssemblyInfo.Path}");
            cachedAssemblyInfo.GUID = GetPluginGUID(cachedAssemblyInfo);
            cachedAssemblyInfo.BadImageFormat = true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{nameof(ReadPluginInfoAndMMHOOKReferences)}] An error occurred while trying to read {cachedAssemblyInfo.Path}\n{ex}");
            cachedAssemblyInfo.GUID = GetPluginGUID(cachedAssemblyInfo);
        }
    }

    private static string GetPluginGUID(CachedAssemblyInfo cachedAssemblyInfo, AssemblyDefinition? pluginAssembly = null)
    {
        CustomAttribute? bepInPluginAttribute = null;

        if (pluginAssembly is not null)
        {
            var pluginAttributes = pluginAssembly.MainModule.GetCustomAttributes();
            bepInPluginAttribute = pluginAttributes.FirstOrDefault(x => x.AttributeType.Name.Equals("BepInPlugin"));
        }

        string? plugin_GUID = null;
        Version? plugin_Ver = new Version(0, 0, 0);

        if(bepInPluginAttribute is not null){
            var bepInPluginAttributeArgs = bepInPluginAttribute.ConstructorArguments;
            plugin_GUID = bepInPluginAttributeArgs[0].Value.ToString();
            // BepInPlugin must have version, and it uses the same Version class we do here
            plugin_Ver = new Version(bepInPluginAttributeArgs[2].Value.ToString());
            MarkIfDuplicate(cachedAssemblyInfo, plugin_GUID, plugin_Ver);
        }

        if(bepInPluginAttribute is null || plugin_GUID is null || plugin_GUID == "") {
            // We use the filename as the GUID, because it doesn't have one.
            var fileName = Path.GetFileName(cachedAssemblyInfo.Path);
            plugin_GUID = Path.GetFileNameWithoutExtension(fileName);

            // GUIDtoVer.Add(plugin_GUID, plugin_Ver);
            MarkIfDuplicate(cachedAssemblyInfo, plugin_GUID, plugin_Ver);
        }
        cachedAssemblyInfo.PluginVersion = plugin_Ver;
        return plugin_GUID;
    }

    private static bool MarkIfDuplicate(CachedAssemblyInfo cachedAssemblyInfo, string plugin_GUID, Version plugin_Ver){
        if (!GUIDtoVer.ContainsKey(plugin_GUID)){
            GUIDtoVer.Add(plugin_GUID, plugin_Ver);
            return false;
        }
        
        // User has two of the same mod installed, likely different versions
        // We want to HookGen the newer version which BepInEx loads
        // TODO: Assemblies with same GUIDs in different directories is fine. This needs to handle that.
        if (GUIDtoVer[plugin_GUID].CompareTo(plugin_Ver) < 0)
        {
            ExtendedLogging($"[{nameof(MarkIfDuplicate)}] Found Newer Version of {plugin_GUID} {GUIDtoVer[plugin_GUID]} => {plugin_Ver} (this)");
            GUIDtoVer.Remove(plugin_GUID);
            currentPlugins.Find(plugin => plugin.GUID.Equals(plugin_GUID) && plugin != cachedAssemblyInfo).IsDuplicate = true;
            GUIDtoVer.Add(plugin_GUID, plugin_Ver);
            return false;
        }

        ExtendedLogging($"[{nameof(MarkIfDuplicate)}] Another Plugin {plugin_GUID} With Same Version or Newer {plugin_Ver} => {GUIDtoVer[plugin_GUID]} Found");
        cachedAssemblyInfo.IsDuplicate = true;
        return true;
    }

    private static List<CachedAssemblyInfo>? TryLoadCache()
    {
        if(!File.Exists(cacheLocation)){
            ExtendedLogging($"[{nameof(TryLoadCache)}] Cache didn't exist.");
            return null;
        }

        XDocument doc = new XDocument();
        List<CachedAssemblyInfo> fromCache = new();

        try{
            doc = XDocument.Load(cacheLocation);
            var version = doc.Root.Attribute("Ver");
            if ((version is null) || int.Parse(version.Value) != cacheVersionID){
                ExtendedLogging("Cache version doesn't match, forcing a refresh.");
                return null;
            }

            foreach(var el in doc.Root.Elements())
            {
                fromCache.Add(new CachedAssemblyInfo(
                    el.Attribute("guid").Value,
                    el.Attribute("path").Value,
                    long.Parse(el.Attribute("dateModified").Value),
                    long.Parse(el.Attribute("MMHOOKDate").Value),
                    el.Attribute("references").Value.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries).ToList(),
                    el.Attribute("pluginVersion").Value,
                    bool.Parse(el.Attribute("badImageFormat").Value)
                    ));
            }
            ExtendedLogging($"[{nameof(TryLoadCache)}] Loaded Cache, which contains {fromCache.Count} entries.");

            return fromCache;
        }
        catch(Exception ex){
            Logger.LogError($"Failed to load cache! Rebuilding it.\n{ex}");
            return null;
            // forceCacheRebuild = true;
        }
    }

    private static void WriteCache(IEnumerable<CachedAssemblyInfo> cachedAssemblyInfos)
    {
        try{
            ExtendedLogging($"[{nameof(WriteCache)}] Updating cache.");
            
            XElement xmlElements = new XElement("cache", new XAttribute("Ver", cacheVersionID), cachedAssemblyInfos.Select(
                assembly => new XElement("assembly",
                new XAttribute("guid", assembly.GUID),
                new XAttribute("path", assembly.Path),
                new XAttribute("dateModified", assembly.DateModified),
                new XAttribute("MMHOOKDate", assembly.MMHOOKDate),
                new XAttribute("references", string.Join("/", assembly.References)), // Note: '/' here is valid on .NET Standard 2.1 but not 2.0, so we should use "/"
                new XAttribute("pluginVersion", assembly.PluginVersion.ToString()),
                new XAttribute("badImageFormat", assembly.BadImageFormat)
                )));
            // Logger.LogInfo("\n" + xmlElements);
            // Create the file.
            File.WriteAllText(cacheLocation, xmlElements.ToString());
        }
        catch(Exception ex) {
            Logger.LogError($"[{nameof(WriteCache)}] Error while writing cache file, clearing it.\n" + ex);
            File.Delete(cacheLocation);
        }
    }

    // Load us https://docs.bepinex.dev/articles/dev_guide/preloader_patchers.html
    public static IEnumerable<string> TargetDLLs { get; } = new string[] { };
    public static void Patch(AssemblyDefinition _) { }
}