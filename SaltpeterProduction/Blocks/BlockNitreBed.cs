using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace SaltpeterProduction.Blocks;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class BlockNitreBed : Block
{
    private const string OrganicMaterialStacksCacheKey = "BlockNitreBed.organicMaterialStacks";
    private List<ItemStack> _organicMaterialStacks = [];
    
    public override void OnLoaded(ICoreAPI coreApi)
    {
        base.OnLoaded(coreApi);
        _organicMaterialStacks = ObjectCacheUtil.GetOrCreate(coreApi, OrganicMaterialStacksCacheKey,
            (CreateCachableObjectDelegate<List<ItemStack>>)(() => GetOrganicMaterialStacks(coreApi)));
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityNitreBed blockEntity)
            return blockEntity.OnBlockInteractStart(world, byPlayer, blockSel);
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityNitreBed blockEntity)
            return blockEntity.OnBlockInteractStep(world, byPlayer, blockSel);
        return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        var baseInteractions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        if (_organicMaterialStacks.Count == 0) return baseInteractions;
        WorldInteraction[] newInteractions =
        [
            new()
            {
                ActionLangCode = $"{SaltpeterProductionCore.ModId}:blockhelp-nitrebed-placeorganic",
                HotKeyCode = "ctrl",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = _organicMaterialStacks.ToArray()
            }
        ];
        return newInteractions.Append(baseInteractions);
    }

    public static List<ItemStack> GetOrganicMaterialStacks(ICoreAPI api)
    {
        const string bucketCode = "game:woodbucket";
        var bucket = api.World.GetBlock(bucketCode) as BlockBucket ??
                     api.World.Blocks.FirstOrDefault(b => b is BlockBucket) as BlockBucket;
        List<ItemStack> organicMaterials = [];
        foreach (var collObj in api.World.Collectibles.Where(c => c.Code != null))
        {
            if (!(BlockEntityNitreBed.GetFillAmount(collObj) > 0)) continue;
            if (collObj.IsLiquid() && bucket != null)
            {
                var bucketStack = new ItemStack(bucket);
                bucket.SetContent(bucketStack, new ItemStack(collObj));
                bucket.SetCurrentLitres(bucketStack, 10);
                organicMaterials.Add(bucketStack);
            }
            else
                organicMaterials.Add(new ItemStack(collObj));
        }

        return organicMaterials;
    }
}