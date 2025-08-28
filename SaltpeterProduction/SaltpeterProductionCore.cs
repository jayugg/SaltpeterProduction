using HarmonyLib;
using JetBrains.Annotations;
using SaltpeterProduction.Blocks;
using Vintagestory.API.Common;

namespace SaltpeterProduction;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class SaltpeterProductionCore : ModSystem
{
    public const string ModId  = "saltpeterproduction";
    public static ILogger? Logger { get; private set; }
    public static ICoreAPI? Api { get; private set; }
    private static Harmony? HarmonyInstance { get; set; }

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        Api = api;
        Logger = Mod.Logger;
        HarmonyInstance = new Harmony(ModId);
        HarmonyInstance.PatchAll();
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        RegisterBlockClass<BlockMellowEarth>(api, ModId);
        RegisterBlockEntityClass<BlockEntityMellowEarth>(api, ModId);
    }

    public override void Dispose()
    {
        HarmonyInstance?.UnpatchAll(ModId);
        HarmonyInstance = null;
        Logger = null;
        Api = null;
        base.Dispose();
    }
    
    private static void RegisterBlockClass<T>(ICoreAPI api, string modid) where T : Block
    {
        api.RegisterBlockClass($"{modid}:{typeof(T).Name}", typeof(T));
    }
    
    private static void RegisterBlockEntityClass<T>(ICoreAPI api, string modid) where T : BlockEntity
    {
        api.RegisterBlockEntityClass($"{modid}:{typeof(T).Name}", typeof(T));
    }
}