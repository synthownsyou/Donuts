using Donuts.Spawning.Models;
using Donuts.Spawning.Services;
using Donuts.Utils;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Donuts.Compat;

namespace Donuts.Spawning.Controllers;

public interface IBotDataController : IDisposable
{
	bool StartingGenerationFinished { get; }
	Task<bool> Initialize(CancellationToken cancellationToken);
	Task PrepareStartingBots(CancellationToken cancellationToken);
	Task ReplenishBotCache(CancellationToken cancellationToken);
}

public class BotDataController(DiContainer container) : IBotDataController
{
	[NotNull] private readonly List<IBotDataService> _dataServices = new(DonutsRaidManager.INITIAL_SERVICES_COUNT);
	
	private bool _initialized;
	private bool _initializationFailed;
	private bool _startingGenerationOngoing;
	private bool _startingGenerationFinished;
	public bool StartingGenerationFinished => _startingGenerationFinished;
	private bool _replenishBotCacheOngoing;
	
	void IDisposable.Dispose()
	{
		foreach (IBotDataService service in _dataServices)
		{
			service.Dispose();
		}
	}
	
	public Task<bool> Initialize(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return Task.FromResult(true);
		}
		
		try
		{
			ResolveServices();
		}
		catch (OperationCanceledException) {}
		catch (Exception ex)
		{
			_initializationFailed = true;
			DonutsRaidManager.Logger.LogException(nameof(BotDataController), nameof(Initialize), ex);
		}
		
		if (_initializationFailed || _dataServices.Count == 0)
		{
			return Task.FromResult(false);
		}
		
		_initialized = true;
		_ = PrepareStartingBots(cancellationToken);
		return Task.FromResult(true);
	}
	
	private void ResolveServices()
	{
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;
		
		if (forceAllBotType is "PMC" or "Disabled")
		{
			var pmcService = container.Resolve<IBotDataService>(DonutsRaidManager.PMC_SERVICE_KEY);
			AddDataService(pmcService);
		}
		
		if (forceAllBotType is "SCAV" or "Disabled")
		{
			var scavService = container.Resolve<IBotDataService>(DonutsRaidManager.SCAV_SERVICE_KEY);
			AddDataService(scavService);
		}
	}

	public async Task PrepareStartingBots(CancellationToken cancellationToken)
	{
		if (!_initialized || _startingGenerationOngoing || _startingGenerationFinished)
		{
			return;
		}

		_startingGenerationOngoing = true;
		try
		{
			await GenerateStartingBots(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {}
		catch (Exception ex)
		{
			DonutsRaidManager.Logger.LogException(nameof(BotDataController), nameof(PrepareStartingBots), ex);
		}
		finally
		{
			foreach (IBotDataService service in _dataServices)
			{
				service.StartingGenerationComplete = true;
			}
			_startingGenerationOngoing = false;
			_startingGenerationFinished = true;
		}
	}
	
	private void AddDataService(IBotDataService dataService)
	{
		if (!_dataServices.Contains(dataService))
		{
			_dataServices.Add(dataService);
		}
	}
	
	private async Task GenerateStartingBots(CancellationToken cancellationToken)
	{
		var states = _dataServices.Select(service => new StartingGenerationState(service, cancellationToken)).ToList();
		var generationTimer = Stopwatch.StartNew();
		double generationBudgetSeconds = Math.Max(0f, DefaultPluginVars.maxRaidDelay.Value);

		try
		{
			// Always generate at least one usable group for each enabled service. This avoids
			// entering a raid with no PMCs or no Scavs when another mod makes profile creation slow.
			foreach (StartingGenerationState state in states)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!await GenerateNextStartingGroup(state, cancellationToken))
				{
					throw new InvalidOperationException(
						$"Donuts could not prepare the minimum starting {state.SpawnType} group.");
				}

				DonutsRaidManager.Logger.LogWarning(
					$"Donuts prepared the minimum starting {state.SpawnType} group " +
					$"({state.Enumerator.Current.TotalBotsGenerated} profiles)."
				);
			}

			// Preserve the original Donuts behavior whenever generation is fast enough: keep
			// filling the complete starting population until the configured load budget expires.
			while (states.Any(state => !state.Complete && !state.Failed) &&
				generationTimer.Elapsed.TotalSeconds < generationBudgetSeconds)
			{
				var generatedAny = false;
				foreach (StartingGenerationState state in states)
				{
					if (state.Complete || state.Failed)
					{
						continue;
					}

					cancellationToken.ThrowIfCancellationRequested();
					if (generationTimer.Elapsed.TotalSeconds >= generationBudgetSeconds)
					{
						break;
					}

					if (!await GenerateNextStartingGroup(state, cancellationToken))
					{
						state.Failed = true;
						continue;
					}

					generatedAny = true;
				}

				if (!generatedAny)
				{
					break;
				}
			}

			foreach (StartingGenerationState state in states.Where(state =>
				!state.Complete && !state.Failed))
			{
				DonutsRaidManager.Logger.LogWarning(
					$"Starting {state.SpawnType} profile generation exceeded the {generationBudgetSeconds:0}-second load budget at {state.Enumerator.Current.Progress:P0}. " +
					"The remaining population will be supplied by normal Donuts waves.");
			}

			foreach (StartingGenerationState state in states.Where(state => state.Complete))
			{
				DonutsRaidManager.Logger.LogWarning(
					$"Donuts prepared the full starting {state.SpawnType} population " +
					$"({state.Enumerator.Current.TotalBotsGenerated} profiles)."
				);
			}
		}
		finally
		{
			foreach (StartingGenerationState state in states)
			{
				await state.Enumerator.DisposeAsync();
			}
		}
	}
	
	private static async Task<bool> GenerateNextStartingGroup(
		StartingGenerationState state,
		CancellationToken cancellationToken)
	{
		try
		{
			EventBus.Raise(BotGenStatusChangeEvent.Create(state.StatusMessage,
				state.Enumerator.Current.Progress));

			if (!await state.Enumerator.MoveNextAsync())
			{
				state.Complete = true;
				EventBus.Raise(BotGenStatusChangeEvent.Create(state.StatusMessage, progress: 1));
				return true;
			}

			state.GeneratedGroups++;
			EventBus.Raise(BotGenStatusChangeEvent.Create(state.StatusMessage,
				state.Enumerator.Current.Progress));
			return true;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (TimeoutException)
		{
			DonutsRaidManager.Logger.LogError(
				$"Timed out while requesting {state.SpawnType} bot profiles. Donuts will not retry this request automatically.");
			EventBus.Raise(BotGenStatusChangeEvent.Create(
				$"Donuts: {state.SpawnType} generation timed out."));
		}
		catch (Exception ex)
		{
			DonutsRaidManager.Logger.LogException(nameof(BotDataController), nameof(GenerateNextStartingGroup), ex);
		}
		
		return false;
	}

	private sealed class StartingGenerationState
	{
		public IBotDataService DataService { get; }
		public IAsyncEnumerator<BotGenerationProgress> Enumerator { get; }
		public string SpawnType { get; }
		public string StatusMessage { get; }
		public bool Complete { get; set; }
		public bool Failed { get; set; }
		public int GeneratedGroups { get; set; }

		public StartingGenerationState(IBotDataService dataService, CancellationToken cancellationToken)
		{
			DataService = dataService;
			SpawnType = dataService.SpawnType.LocalizedPlural();
			StatusMessage = $"Donuts: Generating {SpawnType}...";
			Enumerator = dataService.CreateStartingBotGenerationStream(cancellationToken)
				.GetAsyncEnumerator(cancellationToken);
		}
	}
	
	public async Task ReplenishBotCache(CancellationToken cancellationToken)
	{
		if (!_initialized || _startingGenerationOngoing || _replenishBotCacheOngoing)
		{
			return;
		}
		
		_replenishBotCacheOngoing = true;
		try
		{
			for (int i = _dataServices.Count - 1; i >= 0; i--)
			{
				IBotDataService service = _dataServices[i];
				try
				{
					await service.ReplenishBotCache(cancellationToken);
				}
				catch (OperationCanceledException) {}
				catch (Exception ex)
				{
					DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(ReplenishBotCache), ex);
				}
			}
		}
		finally
		{
			_replenishBotCacheOngoing = false;
		}
	}
}
