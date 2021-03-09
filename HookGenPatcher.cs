using BepInEx.Configuration;
using Mono.Cecil;
using MonoMod;
using MonoMod.RuntimeDetour.HookGen;
using System;
using System.Collections.Generic;
using System.IO;

namespace BepInEx.MonoMod.HookGenPatcher
{
    public static class HookGenPatcher
    {
        internal static Logging.ManualLogSource Logger = Logging.Logger.CreateLogSource("HookGenPatcher");

        private const string CONFIG_FILE_NAME = "HookGenPatcher.cfg";

        private static readonly ConfigFile Config = new ConfigFile(Path.Combine(Paths.ConfigPath, CONFIG_FILE_NAME), true);

        private const char EntrySeparator = ',';

        private static readonly ConfigEntry<string> AssemblyNamesToHookGenPatch = Config.Bind("General", "MMHOOKAssemblyNames",
            "Assembly-CSharp.dll", $"Assembly names to make mmhooks for, separate entries with : {EntrySeparator} ");

        public static IEnumerable<string> TargetDLLs { get; } = new string[] { };

        /**
         * Code largely based on https://github.com/MonoMod/MonoMod/blob/master/MonoMod.RuntimeDetour.HookGen/Program.cs
         */
        public static void Initialize()
        {
            var assemblyNames = AssemblyNamesToHookGenPatch.Value.Split(EntrySeparator);

            var mmhookFolder = Path.Combine(Paths.PluginPath, "MMHOOK");

            foreach (var customAssemblyName in assemblyNames)
            {
                var mmhookFileName = "MMHOOK_" + customAssemblyName;

                string pathIn = Path.Combine(Paths.ManagedPath, customAssemblyName);
                string pathOut = Path.Combine(mmhookFolder, mmhookFileName);
                bool shouldCreateDirectory = true;

                foreach (string mmhookFile in Directory.EnumerateFiles(Paths.PluginPath, mmhookFileName, SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(mmhookFile).Equals(mmhookFileName))
                    {
                        pathOut = mmhookFile;
                        Logger.LogInfo("Previous MMHOOK location found. Using that location to save instead.");
                        shouldCreateDirectory = false;
                        break;
                    }
                }

                if(shouldCreateDirectory)
                {
                    Directory.CreateDirectory(mmhookFolder);
                }
                var size = new FileInfo(pathIn).Length;

                if (File.Exists(pathOut))
                {
                    try
                    {
                        using (var oldMM = AssemblyDefinition.ReadAssembly(pathOut))
                        {
                            bool hash = oldMM.MainModule.GetType("BepHookGen.hash" + size) != null;
                            if (hash)
                            {
                                Logger.LogInfo("Already ran for this version, reusing that file.");
                                continue;
                            }
                        }
                    }
                    catch (BadImageFormatException)
                    {
                        Logger.LogWarning($"Failed to read {Path.GetFileName(pathOut)}, probably corrupted, remaking one.");
                    }
                }

                Environment.SetEnvironmentVariable("MONOMOD_HOOKGEN_PRIVATE", "1");
                Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");

                using (MonoModder mm = new MonoModder()
                {
                    InputPath = pathIn,
                    OutputPath = pathOut,
                    ReadingMode = ReadingMode.Deferred
                })
                {

                    (mm.AssemblyResolver as BaseAssemblyResolver)?.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

                    mm.Read();

                    mm.MapDependencies();

                    if (File.Exists(pathOut))
                    {
                        Logger.LogDebug($"Clearing {pathOut}");
                        File.Delete(pathOut);
                    }

                    Logger.LogInfo("Starting HookGenerator");
                    HookGenerator gen = new HookGenerator(mm, Path.GetFileName(pathOut));

                    using (ModuleDefinition mOut = gen.OutputModule)
                    {
                        gen.Generate();
                        mOut.Types.Add(new TypeDefinition("BepHookGen", "hash" + size, TypeAttributes.AutoClass));
                        mOut.Write(pathOut);
                    }

                    Logger.LogInfo("Done.");
                }
            }
        }

        public static void Patch(AssemblyDefinition _)
        {

        }
    }
}
