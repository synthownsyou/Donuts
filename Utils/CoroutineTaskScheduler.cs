using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Donuts.Utils;

/// <summary>
/// Bridges standard .NET tasks to Unity's frame loop without relying on an
/// external async runtime. Unity API work is always resumed by this component.
/// </summary>
internal sealed class CoroutineTaskScheduler : MonoBehaviour
{
	private static CoroutineTaskScheduler _instance;
	private static int _mainThreadId;
	private readonly ConcurrentQueue<Action> _mainThreadWork = new();

	internal static void Initialize(GameObject owner)
	{
		if (_instance != null)
		{
			return;
		}

		_mainThreadId = Thread.CurrentThread.ManagedThreadId;
		_instance = owner.AddComponent<CoroutineTaskScheduler>();
	}

	internal static Task NextFrame(CancellationToken cancellationToken = default) =>
		DelayFrames(1, cancellationToken);

	internal static Task DelayFrames(int frameCount, CancellationToken cancellationToken = default)
	{
		EnsureInitialized();
		var completion = new TaskCompletionSource<bool>();
		RunOnMainThread(() =>
		{
			if (cancellationToken.IsCancellationRequested)
			{
				completion.TrySetCanceled(cancellationToken);
				return;
			}

			_instance.StartCoroutine(
				_instance.DelayFramesRoutine(Math.Max(1, frameCount), cancellationToken, completion));
		});
		return completion.Task;
	}

	internal static Task Delay(int milliseconds, CancellationToken cancellationToken = default)
	{
		EnsureInitialized();
		var completion = new TaskCompletionSource<bool>();
		RunOnMainThread(() =>
		{
			if (cancellationToken.IsCancellationRequested)
			{
				completion.TrySetCanceled(cancellationToken);
				return;
			}

			_instance.StartCoroutine(
				_instance.DelayRoutine(Math.Max(0, milliseconds), cancellationToken, completion));
		});
		return completion.Task;
	}

	internal static Task SwitchToMainThread(CancellationToken cancellationToken = default)
	{
		EnsureInitialized();
		if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
		{
			return cancellationToken.IsCancellationRequested
				? Task.FromCanceled(cancellationToken)
				: Task.CompletedTask;
		}

		var completion = new TaskCompletionSource<bool>();
		_instance._mainThreadWork.Enqueue(() =>
		{
			if (cancellationToken.IsCancellationRequested)
			{
				completion.TrySetCanceled(cancellationToken);
			}
			else
			{
				completion.TrySetResult(true);
			}
		});
		return completion.Task;
	}

	private static void RunOnMainThread(Action action)
	{
		if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
		{
			action();
			return;
		}

		_instance._mainThreadWork.Enqueue(action);
	}

	private void Update()
	{
		while (_mainThreadWork.TryDequeue(out Action work))
		{
			work();
		}
	}

	private IEnumerator DelayFramesRoutine(
		int frameCount,
		CancellationToken cancellationToken,
		TaskCompletionSource<bool> completion)
	{
		for (var frame = 0; frame < frameCount; frame++)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				completion.TrySetCanceled(cancellationToken);
				yield break;
			}

			yield return null;
		}

		Complete(completion, cancellationToken);
	}

	private IEnumerator DelayRoutine(
		int milliseconds,
		CancellationToken cancellationToken,
		TaskCompletionSource<bool> completion)
	{
		float endTime = Time.realtimeSinceStartup + milliseconds / 1000f;
		while (Time.realtimeSinceStartup < endTime)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				completion.TrySetCanceled(cancellationToken);
				yield break;
			}

			yield return null;
		}

		Complete(completion, cancellationToken);
	}

	private static void Complete(TaskCompletionSource<bool> completion, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			completion.TrySetCanceled(cancellationToken);
		}
		else
		{
			completion.TrySetResult(true);
		}
	}

	private static void EnsureInitialized()
	{
		if (_instance == null)
		{
			throw new InvalidOperationException("Donuts coroutine scheduler has not been initialized.");
		}
	}
}
