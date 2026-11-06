using Donuts.Spawning.Services;
using Donuts.Utils;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Donuts.Spawning.Models;

public class GenerateBotProfilesAsyncEnumerable(
	[NotNull] IBotDataService dataService,
	int maxBotsToGenerate,
	int minGroupSize,
	int maxGroupSize,
	CancellationToken token)
	: IAsyncEnumerable<BotGenerationProgress>
{
	public IAsyncEnumerator<BotGenerationProgress> GetAsyncEnumerator(
		CancellationToken cancellationToken = default)
	{
		return new AsyncEnumerator(dataService, maxBotsToGenerate, minGroupSize, maxGroupSize, token);
	}
	
	private class AsyncEnumerator(
		[NotNull] IBotDataService dataService,
		int maxBotsToGenerate,
		int minGroupSize,
		int maxGroupSize,
		CancellationToken token)
		: IAsyncEnumerator<BotGenerationProgress>
	{
		public BotGenerationProgress Current { get; } = new(maxBotsToGenerate);
		
		public ValueTask DisposeAsync()
		{
			return default;
		}
		
		public async ValueTask<bool> MoveNextAsync()
		{
			if (token.IsCancellationRequested || Current.TotalBotsGenerated >= maxBotsToGenerate)
			{
				return false;
			}
			
			int groupSize = BotHelper.GetBotGroupSize(dataService.GroupChance, minGroupSize, maxGroupSize,
				Current.maxBotsToGenerate - Current.TotalBotsGenerated);
			
			(bool success, PrepBotInfo prepBotInfo) = await dataService.TryGenerateBotProfiles(
				dataService.BotDifficulties.PickRandomElement(),
				groupSize,
				saveToCache: false,
				cancellationToken: token);
			
			if (token.IsCancellationRequested)
			{
				return false;
			}
			
			if (success)
			{
				dataService.StartingBotsCache.Enqueue(prepBotInfo);
				Current.Report(groupSize);
			}
			
			return true;
		}
	}
}
