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

        public static IEnumerable<string> TargetDLLs { get; } = new string[] { };

        /**
         * Code largely based on https://github.com/MonoMod/MonoMod/blob/master/MonoMod.RuntimeDetour.HookGen/Program.cs
         */
        public static void Initialize()
        {
            string pathIn = Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll");
            string pathOut = Path.Combine(Paths.PluginPath, "MMHOOK_Assembly-CSharp.dll");

            var size = new FileInfo(pathIn).Length;

            if (File.Exists(pathOut))
            {
                using(var oldMM = AssemblyDefinition.ReadAssembly(pathOut))
                {
                    bool hash = oldMM.MainModule.GetType("BepHookGen.hash" + size) != null;
                    if (hash)
                    {
                        Logger.LogInfo("Already ran for this version, reusing that file.");
                        return;
                    }
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

        public static void Patch(AssemblyDefinition _)
        {

        }
    }
}
