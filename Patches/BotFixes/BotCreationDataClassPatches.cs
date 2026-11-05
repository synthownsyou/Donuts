using Donuts.Utils;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Donuts.Patches.BotFixes;

[UsedImplicitly]
public class BotCreationDataPatches
{
	private static readonly Type s_targetType = typeof(BotCreationData);
	
	/*
	 * As of SPT 3.11.X
	 * ___list_0 is a List<SpawnPointParams> where SpawnPointParams has a constructor signature:
	 *	public GClass660(Vector3 pos, int corePointId, bool isUsed)
	 *	
	 * SpawnPointParams also has 3 fields:
	 *	public Vector3 position;
	 *	public bool alreadyUsedToSpawn
	 *	public int CorePointId;
	 *
	 * Purpose of this patch:
	 * Donuts currently only adds one spawn position to the BotCreationData;
	 * The current method performs unnecessary code executions after the first GetPosition() call.
	 */
	[UsedImplicitly]
	public class GetPositionPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() =>
			AccessTools.Method(s_targetType, nameof(BotCreationData.GetPosition));
		
		[PatchPrefix]
		private static bool PatchPrefix(ref PositionNote __result, List<PositionNote> ____positions)
		{
			__result = ____positions.PickRandomElement();
			return false;
		}
	}
	
	// TODO: Move this patch to its own file
	[UsedImplicitly]
	public class ChooseProfilePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() =>
			AccessTools.Method(typeof(GetProfileDataParams), nameof(GetProfileDataParams.ChooseProfile));
		
		[PatchPrefix]
		private static bool PatchPrefix(
			GetProfileDataParams __instance,
			ref Profile __result,
			List<Profile> profiles2Select,
			bool withDelete)
		{
			int profileCount = profiles2Select.Count;
			List<Profile> list = new(profileCount);
			
			for (var i = 0; i < profileCount; i++)
			{
				Profile profile = profiles2Select[i];
				var profileSettings = profile.Info.Settings;

				if (profileSettings.Role == __instance.Role &&
					profileSettings.BotDifficulty == __instance.BotDifficulty)
				{
					list.Add(profile);
				}
			}
			
			if (list.Count == 0)
			{
				__result = null;
				return false;
			}
			
			Profile chosenProfile = list.PickRandom();
			
			if (withDelete)
			{
				profiles2Select.Remove(chosenProfile);
			}
			
			__result = chosenProfile;
			return false;
		}
	}
}