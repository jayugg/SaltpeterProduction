using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SaltpeterProduction.Blocks;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class BlockMellowEarth : Block
{
    private const string InteractionCacheKey = "mellowEathBlockInteractions";
    
    private WorldInteraction[] _interactions = [];
    
    public override void OnLoaded(ICoreAPI coreApi)
    {
        base.OnLoaded(coreApi);
        _interactions = ObjectCacheUtil.GetOrCreate(coreApi, InteractionCacheKey, (CreateCachableObjectDelegate<WorldInteraction[]>)(CreateWorldInteractions));
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMellowEarth blockEntity)
            return blockEntity.OnBlockInteractStart(world, byPlayer, blockSel);
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMellowEarth blockEntity)
            blockEntity.OnBlockInteractStop(world, byPlayer, blockSel);
        else base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        var baseInteractions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        return _interactions.Append(baseInteractions);
    }

    private WorldInteraction[] CreateWorldInteractions()
    {
        const string bucketCode = "game:woodbucket";
        var bucket = api.World.GetBlock(bucketCode) as BlockBucket ??
                     api.World.Blocks.FirstOrDefault(b => b is BlockBucket) as BlockBucket;
        List<ItemStack> organicMaterials = [];
        foreach (var collObj in api.World.Collectibles.Where(c => c.Code != null))
        {
            if (!(BlockEntityMellowEarth.GetFillAmount(collObj) > 0)) continue;
            if (collObj.IsLiquid() && bucket != null)
            {
                var bucketStack = new ItemStack(bucket);
                bucket.SetContent(bucketStack, new ItemStack(collObj));
                bucket.SetCurrentLitres(bucketStack, 10);
                organicMaterials.Add(bucketStack);
            }
            else
            {
                organicMaterials.Add(new ItemStack(collObj));
            }
        }
        return
        [
            new WorldInteraction()
            {
                ActionLangCode = $"{SaltpeterProductionCore.ModId}:blockhelp-mellowearth-placeorganic",
                HotKeyCode = "ctrl",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = organicMaterials.ToArray()
            }
        ];
    }
}