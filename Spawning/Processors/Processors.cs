using Donuts.Spawning.Models;
using UnityEngine;
using Donuts.Compat;

namespace Donuts.Spawning.Processors;

public abstract class SpawnCheckProcessorBase : ProcessorBase<Vector3>;
public abstract class WaveSpawnProcessorBase : ProcessorBase<BotWave>;