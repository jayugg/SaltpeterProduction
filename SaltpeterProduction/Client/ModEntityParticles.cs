using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SaltpeterProduction.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SaltpeterProduction.Client;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class ModEntityParticles : ModSystem
{
    private static readonly Random Random = new();
    private EntityParticleSystem? _entityParticleSystem;
    // Queue if in the future want to execute stuff on the EP thread
    private readonly Queue<Action> _simTickEntityParticleQueue = new();
    private float _accumulator;
    private ICoreClientAPI? Capi {get; set;}

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        Capi = api;
        _entityParticleSystem = api.ModLoader.GetModSystem<EntityParticleSystem>();
        _entityParticleSystem.OnSimTick += OnSimTick;
    }

    private void OnSimTick(float dt)
    {
        _accumulator += dt;
        while (_simTickEntityParticleQueue.Count > 0)
            _simTickEntityParticleQueue.Dequeue()();
        if (Capi == null) return;
        if (_accumulator <= 0.5)
            return;
        _accumulator = 0.0f;
        EntityPos pos = Capi.World.Player.Entity.Pos;
        if (pos.Dimension != 0)
            return;
        ClimateCondition climateAt = Capi.World.BlockAccessor.GetClimateAt(pos.AsBlockPos);
        SpawnMatingGnatsSwarm(Capi, pos, climateAt);
    }
    
    private static void SpawnMatingGnatsSwarm(ICoreClientAPI capi, EntityPos pos, ClimateCondition climate)
    {
        var entityParticleSystem = capi.ModLoader.GetModSystem<EntityParticleSystem>();
        if (climate.Temperature < 17.0 ||
            // 100 more than vanilla supported count (200), to ensure some always get spawned
            entityParticleSystem?.Count["matinggnats"] > 300)
            return;
        var attempts = 0;
        for (var i = 0; i < 100 && attempts < 6; ++i)
        {
            var xPos = pos.X + (Random.NextDouble() - 0.5) * 24;
            var yPos = pos.Y + (Random.NextDouble() - 0.5) * 24;
            var zPos = pos.Z + (Random.NextDouble() - 0.5) * 24;
            if (pos.HorDistanceTo(xPos, zPos) < 2.0) continue;
            var blockEntity =
                capi.World.BlockAccessor.GetBlockEntity(new BlockPos((int)xPos, (int)yPos, (int)zPos));
            if (blockEntity is not BlockEntityMellowEarth {HasMaterialStored: true}) continue;
            var cohesion = (float) GameMath.Max(Random.NextDouble() * 1.1, 0.25) / 2f;
            var spawnCount = 10 + Random.Next(21);
            for (var j = 0; j < spawnCount; ++j)
            {
                entityParticleSystem?.SpawnParticle(new EntityParticleMatingGnats(capi, cohesion, xPos + 0.5, yPos + 0.5, zPos + 0.5));
            }
            ++attempts;
        }
    }

    // Must call on EntityParticle Thread, thus we enqueue to _simTickEntityParticleQueue
    public static void EnqueueGnatsSpawn(ICoreClientAPI capi, EntityPos pos, ClimateCondition climate)
    {
        capi.ModLoader.GetModSystem<ModEntityParticles>()._simTickEntityParticleQueue.Enqueue(
            () => SpawnMatingGnatsSwarm(capi, pos, climate)
        );
    }
}