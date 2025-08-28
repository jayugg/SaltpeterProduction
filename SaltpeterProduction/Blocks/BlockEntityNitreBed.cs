using System;
using System.Text;
using JetBrains.Annotations;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SaltpeterProduction.Blocks;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class BlockEntityNitreBed : BlockEntity
{
    private const string FillAmountAttributeKey = "nitreBedFillAmount";

    private const string FillAmountPerLitresAttributeKey = "nitreBedAmountPerLitre";
    // TODO Refactor: less hardcoded growth stages and growth rate
    private const int HoursPerGrowthDefault = 72;
    private static readonly AssetLocation SaltpeterBudCode0 = $"{SaltpeterProductionCore.ModId}:saltpeterbud-0";
    private static readonly AssetLocation SaltpeterBudCode1 = $"{SaltpeterProductionCore.ModId}:saltpeterbud-1";
    private static readonly AssetLocation SaltpeterCode = "game:saltpeter-d";
    private static readonly Random Rand = new();
    // Should be between 0 and 1
    private float _organicMaterial;
    private float OrganicMaterial
    {
        get => _organicMaterial;
        set => _organicMaterial = Math.Clamp(value, 0f, 1f);
    }
    private double TotalHoursLastGrowth { get; set; }
    private Block BlockAbove => Api.World.BlockAccessor.GetBlock(Pos.Copy().Up());
    private double HoursPerGrowth =>
        Block.Attributes["hoursPerGrowth"]?.AsDouble(HoursPerGrowthDefault) ?? HoursPerGrowthDefault;
    public bool HasMaterialStored => OrganicMaterial > 1e-3;
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        RegisterGameTickListener(OnRandomTick, 3300 + Rand.Next(400));
    }

    public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!byPlayer.Entity.Controls.CtrlKey) return false;
        var heldItemStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (heldItemStack == null) return false;
        SaltpeterProductionCore.Logger?.Warning("Interact in be");
        if (OrganicMaterial >= 1) return false;
        return CanFillFromPlayer(byPlayer);
    }
    
    public void OnBlockInteractStop(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server) return;
        if (!TryFillFromPlayer(byPlayer)) return;
        world.PlaySoundAt("block/dirt", Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, true, 16f);
        TotalHoursLastGrowth = world.Calendar.TotalHours;
        MarkDirty();
    }

    private static bool CanFillFromPlayer(IPlayer byPlayer)
    {
        var heldItemStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (heldItemStack == null) return false;
        float availableFillAmount;
        if (heldItemStack.Collectible is BlockLiquidContainerBase container)
        {
            var contentStack = container.GetContent(heldItemStack);
            if (contentStack is not { Collectible: not null, StackSize: > 0 }) return false;
            availableFillAmount = GetFillAmountPerLitres(contentStack.Collectible) * container.GetCurrentLitres(heldItemStack);
        }
        else
        {
            availableFillAmount = GetFillAmount(heldItemStack.Collectible) * heldItemStack.StackSize;
        }
        return (availableFillAmount > 1e-3); // Floating point comparison
    }

    private bool TryFillFromPlayer(IPlayer byPlayer)
    {
        var heldItemStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (heldItemStack == null) return false;
        float fillAmount;
        if (heldItemStack.Collectible is BlockLiquidContainerBase container)
        {
            var contentStack = container.GetContent(heldItemStack);
            if (contentStack is not { Collectible: not null, StackSize: > 0 }) return false;
            var fillAmountPerLitres = GetFillAmountPerLitres(contentStack.Collectible);
            var availableFillAmount = fillAmountPerLitres * container.GetCurrentLitres(heldItemStack);
            if (availableFillAmount <= 1e-3) return false;
            var remainingToFill = 1 - OrganicMaterial;
            var requiredLitres = remainingToFill / fillAmountPerLitres;
            var takenAmount = container.TryTakeLiquid(heldItemStack, requiredLitres)?.StackSize ?? 0;
            var itemsPerLitre = BlockLiquidContainerBase.GetContainableProps(contentStack)?.ItemsPerLitre ?? 100;
            var takenLitres = takenAmount / itemsPerLitre;
            fillAmount = takenLitres * fillAmountPerLitres;
            container.DoLiquidMovedEffects(byPlayer, contentStack, takenAmount, BlockLiquidContainerBase.EnumLiquidDirection.Pour);
        }
        else
        {
            fillAmount = GetFillAmount(heldItemStack.Collectible);
            if (fillAmount <= 1e-3) return false;
            byPlayer.InventoryManager?.ActiveHotbarSlot?.TakeOut(1);
        }
        OrganicMaterial += fillAmount;
        byPlayer.InventoryManager?.ActiveHotbarSlot?.MarkDirty();
        return true;
    }

    private bool TryGetNextSaltpeterGrowth(out Block? saltpeterGrowth)
    {
        saltpeterGrowth = null;
        if (BlockAbove.Code.ToString().Contains(SaltpeterBudCode0))
        {
            saltpeterGrowth = Api.World.GetBlock(SaltpeterBudCode1);
            return true;
        }
        if (BlockAbove.Code.ToString().Contains(SaltpeterBudCode1))
        {
            saltpeterGrowth = Api.World.GetBlock(SaltpeterCode);
            return true;
        }
        if (BlockAbove.BlockId == 0)
        {
            saltpeterGrowth = Api.World.GetBlock(SaltpeterBudCode0);
            return true;
        }
        return false;
    }

    private void OnRandomTick(float dt)
    {
        if (Api is not ICoreServerAPI sapi) return;
        if (!sapi.World.IsFullyLoadedChunk(this.Pos))
            return;
        if (sapi.World.BlockAccessor.GetBlock(Pos) is not BlockNitreBed) return;
        // Determine the correct state based on OrganicMaterial
        UpdateVariant();
        if (!HasMaterialStored) return;
        if (!TryGetNextSaltpeterGrowth(out var saltpeterGrowth) || saltpeterGrowth is null) return;
        var totalHours = sapi.World.Calendar.TotalHours;
        // Growth checks
        var hoursPerGrowth = Block.Attributes["hoursPerGrowth"]?.AsDouble(HoursPerGrowthDefault) ?? HoursPerGrowthDefault;
        if ((totalHours - TotalHoursLastGrowth > hoursPerGrowth))
        {
            if (OrganicMaterial < 0.5f)
            {
                SaltpeterProductionCore.Logger?.VerboseDebug($"Not enough material {OrganicMaterial}");
                return;
            }
            sapi.World.BlockAccessor.SetBlock(saltpeterGrowth.BlockId, Pos.Copy().Up());
            OrganicMaterial -= 0.5f;
            TotalHoursLastGrowth = totalHours;
            MarkDirty();
        }
    }

    private void UpdateVariant()
    {
        var targetState = HasMaterialStored ? "moist" : "dry";
        var targetBlockCode = Block.CodeWithVariant("state", targetState);
        if (Block.Code == targetBlockCode) return;
        var targetBlock = Api.World.GetBlock(targetBlockCode);
        Api.World.BlockAccessor.ExchangeBlock(targetBlock.BlockId, Pos);
        MarkDirty();
    }

    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        TotalHoursLastGrowth = Api.World.Calendar.TotalHours;
    }

    public override void OnBlockBroken(IPlayer? byPlayer = null)
    {
        base.OnBlockBroken(byPlayer);
        if (BlockAbove.Code.ToString().Contains(SaltpeterBudCode0) ||
            BlockAbove.Code.ToString().Contains(SaltpeterBudCode1))
        {
            Api.World.BlockAccessor.BreakBlock(Pos.Copy().Up(), byPlayer);
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (HasMaterialStored)
            dsc.AppendLine(Lang.Get("Fill {0}%", Math.Round(OrganicMaterial * 100)));
        var hoursNextGrowth = HoursPerGrowth + TotalHoursLastGrowth - forPlayer.Entity.World.Calendar.TotalHours;
        if (hoursNextGrowth <= HoursPerGrowth && hoursNextGrowth >= 0)
            dsc.AppendLine(Lang.Get("Hours to next growth {0}", Math.Round(hoursNextGrowth)));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        OrganicMaterial = Math.Clamp(tree.GetFloat("organicMaterial"), 0, 1);
        TotalHoursLastGrowth = tree.GetDouble("totalHoursLastGrowth");
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("organicMaterial", OrganicMaterial);
        tree.SetDouble("totalHoursLastGrowth", TotalHoursLastGrowth);
    }

    public static float GetFillAmount(CollectibleObject collObj) => collObj.Attributes?[FillAmountAttributeKey].AsFloat() ?? 0;
    public static float GetFillAmountPerLitres(CollectibleObject collObj) => collObj.Attributes?[FillAmountPerLitresAttributeKey].AsFloat(1) ?? 1;
}