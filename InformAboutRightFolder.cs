using BepInEx;

namespace BepInEx.MonoMod.HookGenPatcher
{
    [BepInPlugin("me.harbingerofme.HookGenPatcher", "HookGenPatcher", "0.0.1")]
    class InformUserAboutWrongLocation : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogError("This .dll should be in your patchers folder. Read the installation instructions and try again!");
        }
    }
}
