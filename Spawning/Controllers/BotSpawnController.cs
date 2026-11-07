using Donuts.Spawning.Services;
using Donuts.Utils;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Donuts.Compat;

namespace Donuts.Spawning.Controllers;

public interface IBotSpawnController
{
	void Initialize();
	Task SpawnStartingBots(CancellationToken cancellationToken);
	Task SpawnBotWaves(CancellationToken cancellationToken);
}

public class BotSpawnController([NotNull] DiContainer container) : IBotSpawnController
{
	[NotNull] private readonly List<IBotSpawnService> _spawnServices = new(DonutsRaidManager.INITIAL_SERVICES_COUNT);
	
	private const int MS_DELAY_BETWEEN_SPAWNS = 500;
	private const float SPAWN_INTERVAL_SECONDS = 1f;
	
	private bool _initialized;
	
	private bool _hasSpawnedStartingBots;
	private bool _startingSpawnOngoing;
	private float _startingSpawnPrevTime;
	
	private bool _waveSpawnOngoing;
	private float _waveSpawnPrevTime;
	
	public void Initialize()
	{
		if (_initialized)
		{
			return;
		}
		
		try
		{
			ResolveServices();
		}
		catch (OperationCanceledException) {}
		catch (Exception ex)
		{
			DonutsRaidManager.Logger.LogException(nameof(BotSpawnController), nameof(Initialize), ex);
		}
		
		if (_spawnServices.Count == 0)
		{
			return;
		}
		
		_initialized = true;
	}
	
	private void ResolveServices()
	{
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;
		
		if (forceAllBotType is "PMC" or "Disabled")
		{
			var pmcService = container.Resolve<IBotSpawnService>(DonutsRaidManager.PMC_SERVICE_KEY);
			if (!_spawnServices.Contains(pmcService))
			{
				_spawnServices.Add(pmcService);
			}
		}
		
		if (forceAllBotType is "SCAV" or "Disabled")
		{
			var scavService = container.Resolve<IBotSpawnService>(DonutsRaidManager.SCAV_SERVICE_KEY);
			if (!_spawnServices.Contains(scavService))
			{
				_spawnServices.Add(scavService);
			}
		}
	}
	
	public async Task SpawnStartingBots(CancellationToken cancellationToken)
	{
		if (!_initialized ||
			_hasSpawnedStartingBots ||
			_startingSpawnOngoing ||
			Time.time < _startingSpawnPrevTime + SPAWN_INTERVAL_SECONDS)
		{
			return;
		}
		
		_startingSpawnPrevTime = Time.time;
		_startingSpawnOngoing = true;
		_hasSpawnedStartingBots = true;
		try
		{
			for (int i = _spawnServices.Count - 1; i >= 0; i--)
			{
				IBotSpawnService service = _spawnServices[i];
				try
				{
					if (!await service.SpawnStartingBots(cancellationToken))
					{
						_hasSpawnedStartingBots = false;
					}

					cancellationToken.ThrowIfCancellationRequested();
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(SpawnStartingBots), ex);
				}
			}
		}
		finally
		{
			_startingSpawnOngoing = false;
		}
	}
	
	public async Task SpawnBotWaves(CancellationToken cancellationToken)
	{
		if (!_initialized ||
			!_hasSpawnedStartingBots ||
			_waveSpawnOngoing ||
			Time.time < _waveSpawnPrevTime + SPAWN_INTERVAL_SECONDS)
		{
			return;
		}
		
		_waveSpawnPrevTime = Time.time;
		_waveSpawnOngoing = true;
		try
		{
			// Spawn all queued bot waves
			var hasWavesToSpawn = true;
			while (hasWavesToSpawn && !cancellationToken.IsCancellationRequested)
			{
				hasWavesToSpawn = false;
				for (int i = _spawnServices.Count - 1; i >= 0; i--)
				{
					cancellationToken.ThrowIfCancellationRequested();
					IBotSpawnService service = _spawnServices[i];
					try
					{
						if (!await service.TrySpawnBotWave(cancellationToken))
						{
							continue;
						}
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						return;
					}
					catch (Exception ex)
					{
						DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(SpawnBotWaves), ex);
					}

					hasWavesToSpawn = true;
					if (i > 0)
					{
						await CoroutineTaskScheduler.Delay(MS_DELAY_BETWEEN_SPAWNS, cancellationToken);
					}
				}

				if (hasWavesToSpawn)
				{
					await CoroutineTaskScheduler.Delay(MS_DELAY_BETWEEN_SPAWNS, cancellationToken);
				}
			}
		}
		finally
		{
			_waveSpawnOngoing = false;
		}
	}
}
