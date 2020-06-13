# BepInEx.MonoMod.HookGenPatcher

Generates [MonoMod.RuntimeDetour.HookGen's](https://github.com/MonoMod/MonoMod) `MMHOOK` file during the [BepInEx](https://github.com/BepInEx/BepInEx) preloader phase. 

Installation:
Put in `BepInEx\patchers` folder.
Make sure the `MonoMod.exe` and `MonoMod.RuntimeDetour.HookGen.exe` files are also present.

**This project is not officially linked with BepInEx nor with MonoMod.**

License:
You may freely distribute this for any purpose. Note that MonoMod files are needed for succesful execution, and different licensing rules may apply to those.

## See also:
[LighterPatcher](https://github.com/harbingerofme/LighterPatcher) which is designed to easy the load on the game when having particularily large MMHOOK files by stripping unneeded types. LgihterPatcher and HookGenPatcher work in conjuction with each other to prevent multiple runs.