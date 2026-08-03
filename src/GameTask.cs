// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
using System.Linq;
using CivOne.Events;
using CivOne.Tasks;

namespace CivOne
{
	public abstract class GameTask : BaseInstance
	{
		private static GameTask? _currentTask = null;
		private static List<GameTask> _tasks = new();

		public static bool Any() => (_tasks.Count > 0);
		public static bool Is<T>() where T : GameTask => (_currentTask is not null && _currentTask is T);
		// Between two tasks _currentTask is null (Finish clears it) while the queue is still
		// full, and the very next Update() call starts the one at the head. Judging Fast only
		// by _currentTask made that gap read as "not fast", so the host loop dropped out of
		// fast-forward and paced the transition at 60 Hz — measured at 518,654 transitions over
		// 381 turns, ~1,361 per turn at ~3.6ms each: 4.9 seconds of a 13-second turn, the
		// largest single cost in the game and more than every named task type combined.
		//
		// Falling back to the head of the queue is exactly as conservative as before: it asks
		// the same question of the task that is about to run, so a Show or a Message still
		// stops the fast-forward before it is displayed.
		public static bool Fast
		{
			get
			{
				GameTask? task = _currentTask ?? (_tasks.Count > 0 ? _tasks[0] : null);
				return task is not null && Common.HasAttribute<Fast>(task);
			}
		}
		// TEMPORARY (2026-08-03): names the task holding the queue, so the pacing probe in
		// RuntimeHandler can say WHICH task type the 60 Hz wait is being spent on. Remove with
		// the rest of the instrumentation.
		internal static string CurrentName => _currentTask?.GetType().Name ?? "none";
		public static int Count<T>() where T : GameTask => _tasks.Count(t => t is T);

		private static void NextTask()
		{
			// Snapshot the task: Run()/Step() can end the task synchronously (EndTask →
			// Finish nulls _currentTask), so referencing _currentTask afterwards in the
			// catch block would throw a second, masking NullReferenceException.
			GameTask task = _currentTask = _tasks[0];
			TaskEventArgs eventArgs = new TaskEventArgs();
			Started?.Invoke(task, eventArgs);
			if (eventArgs.Aborted)
			{
				task.EndTask();
				return;
			}
			try
			{
				task.Run();
			}
			catch (Exception ex)
			{
				Log($"[GameTask] Unhandled exception in {task.GetType().Name}.Run(): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
				// Don't call EndTask() here — it would re-fire Done and could throw again, escaping the catch.
				// Just drop the task; Update() will call NextTask() on the next tick.
				_tasks.Remove(task);
				if (_currentTask == task) _currentTask = null;
			}
		}

		public static bool Update()
		{
			if (_currentTask is not null)
			{
				// Snapshot: Step() can end the task synchronously (EndTask → Finish nulls
				// _currentTask), so the catch below must not re-read _currentTask.
				GameTask task = _currentTask;
				try
				{
					return task.Step();
				}
				catch (Exception ex)
				{
					Log($"[GameTask] Unhandled exception in {task.GetType().Name}.Step(): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
					// Don't call EndTask() here — it would re-fire Done and could throw again, escaping the catch.
					// Just drop the task; the next Update() call will advance to the next queued task.
					_tasks.Remove(task);
					if (_currentTask == task) _currentTask = null;
					return true;
				}
			}
			else if (_tasks.Count == 0)
				return false;

			NextTask();
			return true;
		}

		public static void Enqueue(GameTask task)
		{
			if (task is null) return;
			task.Done += Finish;
			_tasks.Add(task);
		}

		public static void Insert(GameTask task)
		{
			if (task is null) return;
			task.Done += Finish;
			_tasks.Insert(0, task);
		}

		protected static void RemoveQueued(Predicate<GameTask> match)
		{
			_tasks.RemoveAll(t => t != _currentTask && match(t));
		}

		private static void Finish(object sender, EventArgs args)
		{
			_tasks.Remove((sender as GameTask)!);
			if (!_tasks.Any())
			{
				_currentTask = null;
				return;
			}

			NextTask();
		}

		public static event TaskEventHandler? Started;
		public event EventHandler? Done;

		protected virtual bool Step() => false;

		public abstract void Run();

		protected void EndTask()
		{
			if (Done is null) return;
			Done(this, null);
			Done = null;
		}
	}
}