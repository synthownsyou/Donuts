using Comfort.Common;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Reflection;

namespace Donuts.Patches;

[UsedImplicitly]
internal class GenerateStartingBotsPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		Type gameType = typeof(LocalGame);
		if (DonutsPlugin.FikaEnabled)
		{
			// Fika 2.4 moved CoopGame from Fika.Core.Coop.GameMode to Fika.Core.Main.GameMode.
			// Keep the old lookup as a fallback so the patch remains compatible with both layouts.
			gameType = AccessTools.TypeByName("Fika.Core.Main.GameMode.CoopGame")
				?? AccessTools.TypeByName("Fika.Core.Coop.GameMode.CoopGame");
		}

		if (gameType == null)
		{
			DonutsRaidManager.Logger.LogError("Unable to locate the active game type for Donuts raid loading.");
			return null;
		}
		
		return AccessTools.Method(gameType, "vmethod_5");
	}
	
	[PatchPostfix]
	private static void PatchPostfix(ref IEnumerator __result)
	{
		if (!Singleton<AbstractGame>.Instance.InRaid) return;

		// SetSettings can run before bot spawning is enabled in Fika. Ensure the manager exists
		// at the point where raid setup is actually waiting for starting bot data.
		if (!Singleton<DonutsRaidManager>.Instantiated)
		{
			DonutsRaidManager.Enable();
		}
		
		if (Singleton<DonutsRaidManager>.Instantiated && DonutsRaidManager.IsBotSpawningEnabled)
		{
			__result = Singleton<DonutsRaidManager>.Instance.DonutsRaidLoadingTask(__result); // Thanks danW
		}
	}
}
