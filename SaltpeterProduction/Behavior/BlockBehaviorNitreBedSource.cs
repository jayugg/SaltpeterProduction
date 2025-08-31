using System;
using System.Linq;
using SaltpeterProduction.Blocks;
using SaltpeterProduction.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SaltpeterProduction.Behavior;

public class BlockBehaviorNitreBedSource(Block block) : BlockBehavior(block)
{
    public const string OrganicMaterialStacksCacheKey = "BlockBehaviorNitreBedSource.organicMaterialStacks";
    private const string NitreBedCodeProperty = "nitreBedCode";
    private const string MaterialRequiredProperty = "materialRequired";
    private const float DefaultMaterialRequired = 0.5f;
    private float MaterialRequired { get; set; } = DefaultMaterialRequired;
    private AssetLocation? NitreBedCode { get; set; }
    private BlockNitreBed? NitreBedBlock { get; set; }
    private ItemStack[] _organicMaterialStacks = [];
    
    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        SaltpeterProductionCore.Logger?.VerboseDebug($"Initialising for {block.Code}");
        if (properties[NitreBedCodeProperty]?.AsString() is { } nitreBedCode)
        {
            NitreBedCode = nitreBedCode;
        }
        else SaltpeterProductionCore.Logger?.Warning(
            $"[{nameof(Initialize)}] Cannot find {NitreBedCodeProperty} property." +
            $"Please check your behavior in the blocktype.");

        if (properties[MaterialRequiredProperty] == null)
        {
            SaltpeterProductionCore.Logger?.Warning(
                $"[{nameof(Initialize)}] Cannot find {MaterialRequiredProperty} property." +
                $"Please check your behavior in the blocktype.");
        }
        MaterialRequired = properties[MaterialRequiredProperty]?
                               .AsFloat(DefaultMaterialRequired) ??
                           DefaultMaterialRequired;
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (NitreBedCode != null)
        {
            NitreBedBlock = api.World.BlockAccessor.GetBlock(NitreBedCode) as BlockNitreBed;
        }
        if (NitreBedBlock == null)
            SaltpeterProductionCore.Logger?.Warning(
                $"[{nameof(OnLoaded)}] Could not resolve nitreBed block with code {NitreBedCode ?? "[code is null]"}." +
                $"Please check your behavior in the blocktype.");
        
        _organicMaterialStacks = ObjectCacheUtil.GetOrCreate(api, OrganicMaterialStacksCacheKey,
            (CreateCachableObjectDelegate<ItemStack[]>)(() => BlockNitreBed.GetOrganicMaterialStacks(api))).
            Select(s => s.Clone()).ToArray();
        
        // We update these to reflect the required stack size
        foreach (var stack in _organicMaterialStacks)
        {
            var fillAmount = BlockEntityNitreBed.GetFillAmount(stack.Collectible);
            if (fillAmount.IsZero()) continue;
            var requiredItems = (int) Math.Ceiling(MaterialRequired / fillAmount);
            stack.StackSize = requiredItems;
        }
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (NitreBedBlock == null) return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        if (!byPlayer.Entity.Controls.CtrlKey) return false;
        var heldItemStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (heldItemStack == null) return false;
        if (!BlockEntityNitreBed.CanFillFromPlayer(byPlayer)) return false;
        handling = EnumHandling.PreventSubsequent;
        return true;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection? blockSel,
        ref EnumHandling handling)
    {
        if (NitreBedBlock == null) return;
        if (world.Side != EnumAppSide.Server) return;
        if (blockSel == null) return;
        if (!TryFillFromPlayer(byPlayer)) return;
        
        var pos = blockSel.Position;
        float moistureLevel = 0;
        
        if (world.BlockAccessor.GetBlockEntity(pos) is IFarmlandBlockEntity farmlandBlockEntity) 
            moistureLevel = farmlandBlockEntity.MoistureLevel;
        
        world.BlockAccessor.SetBlock(NitreBedBlock.BlockId, pos);
        
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityNitreBed nitreBedEntity)
            nitreBedEntity.MoistureLevel = moistureLevel;
        
        world.PlaySoundAt("block/dirt", pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, null, true, 16f);
        handling = EnumHandling.PreventSubsequent;
    }
    
    private bool TryFillFromPlayer(IPlayer byPlayer)
    {
        var heldItemStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (heldItemStack == null) return false;
        if (heldItemStack.Collectible is BlockLiquidContainerBase container)
        {
            var contentStack = container.GetContent(heldItemStack);
            if (contentStack is not { Collectible: not null, StackSize: > 0 }) return false;
            var fillAmountPerLitres = BlockEntityNitreBed.GetFillAmountPerLitres(contentStack.Collectible);
            if (fillAmountPerLitres.IsZero()) return false;
            var requiredLitres = MaterialRequired / fillAmountPerLitres;
            if (requiredLitres > container.GetCurrentLitres(heldItemStack)) return false;
            var takenAmount = container.TryTakeLiquid(heldItemStack, requiredLitres)?.StackSize ?? 0;
            container.DoLiquidMovedEffects(byPlayer, contentStack, takenAmount, BlockLiquidContainerBase.EnumLiquidDirection.Pour);
        }
        else
        {
            var fillAmount = BlockEntityNitreBed.GetFillAmount(heldItemStack.Collectible);
            if (fillAmount.IsZero()) return false;
            var requiredItems = (int) Math.Ceiling(MaterialRequired / fillAmount);
            if (requiredItems > heldItemStack.StackSize) return false;
            byPlayer.InventoryManager?.ActiveHotbarSlot?.TakeOut(requiredItems);
        }
        byPlayer.InventoryManager?.ActiveHotbarSlot?.MarkDirty();
        return true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer,
        ref EnumHandling handling)
    {
        var baseInteractions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);
        if (_organicMaterialStacks.Length == 0) return baseInteractions;
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
}