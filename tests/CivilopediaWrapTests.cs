// CivOne tests
//
// Resources.GetCivilopediaText wraps text to a 294px column. Its fall-through — taken when
// the next word does not fit — used to flush the current line, empty it, and loop again
// WITHOUT consuming any input. With an empty line the test becomes "does this word fit on its
// own", so a word wider than the column produced an infinite loop appending empty strings to
// a growing list.
//
// At font 6 a glyph is 10px and the column is 294, so any 30-character token triggers it.
//
// Observed as a hard hang mid-game: 100% of a core, memory climbing, and a window that would
// not repaint its cursor, because the main thread never returned. The headless harness sails
// straight past it — nothing there renders text — which is why turn processing looked healthy
// while the game was frozen.
//
// Every test here runs under a watchdog. A regression must FAIL, not hang the suite.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CivOne.Tests
{
	public class CivilopediaWrapTests
	{
		private const int WatchdogMs = 10000;

		private static string[] Wrap(string text)
		{
			Sim.EnsureRuntime();

			// Inject the text the way a data file would supply it. GetCivilopediaText reads
			// TextFile.Instance.GetGameText(name), so the honest route in is that dictionary.
			var tf = typeof(Game).Assembly.GetType("CivOne.IO.TextFile")!;
			object inst = tf.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Static)!.GetValue(null)!;
			var dict = (System.Collections.IDictionary)tf.GetField("_gameTexts",
				BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(inst)!;
			dict["WRAPTEST/ENTRY"] = new[] { text };

			var res = typeof(Game).Assembly.GetType("CivOne.Graphics.Resources")!;
			object rinst = res.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Static)!.GetValue(null)!;
			var m = res.GetMethod("GetCivilopediaText", BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Instance)!;

			string[]? result = null;
			Exception? failure = null;
			Task t = Task.Run(() =>
			{
				try { result = (string[])m.Invoke(rinst, new object[] { "WRAPTEST/ENTRY" })!; }
				catch (Exception ex) { failure = ex; }
			});
			Assert.True(t.Wait(WatchdogMs),
				$"GetCivilopediaText did not return within {WatchdogMs}ms — it is looping");
			if (failure is not null) throw failure;
			return result!;
		}

		// The hang itself: a single token far wider than the column.
		[Fact]
		public void AWordWiderThanTheColumnDoesNotHang()
		{
			string[] lines = Wrap("short " + new string('W', 60) + " tail");

			Assert.NotEmpty(lines);
			Assert.Contains(lines, l => l.Contains(new string('W', 60)));
		}

		// ...and it must not silently swallow the rest of the entry either — everything after
		// the over-long word still has to appear.
		[Fact]
		public void TextAfterAnOverlongWordSurvives()
		{
			string[] lines = Wrap("alpha " + new string('X', 45) + " omega");

			Assert.Contains(lines, l => l.Contains("alpha"));
			Assert.Contains(lines, l => l.Contains("omega"));
		}

		// A wall of over-long words: every one of them must be consumed. The old loop
		// terminated on none of them.
		[Fact]
		public void SeveralOverlongWordsInARowAllTerminate()
		{
			string big = string.Join(" ", Enumerable.Range(0, 5).Select(i => new string((char)('A' + i), 40)));

			string[] lines = Wrap(big);

			Assert.True(lines.Length >= 5, $"expected a line per word, got {lines.Length}");
		}

		// Ordinary text must still wrap as before — a fix that made every word its own line
		// would be a regression dressed as a repair.
		[Fact]
		public void OrdinaryTextStillWraps()
		{
			string[] lines = Wrap(string.Join(" ", Enumerable.Repeat("word", 60)));

			Assert.True(lines.Length > 1, "no wrapping happened at all");
			Assert.True(lines.All(l => l.Length > 4), "lines came back empty or single-word");
			Assert.DoesNotContain(lines, l => l.Length == 0);
		}

		// No empty lines, ever. The old loop's signature was an unbounded run of them.
		[Fact]
		public void NoEmptyLinesAreEmitted()
		{
			string[] lines = Wrap("lead " + new string('Q', 50) + " follow " + new string('Z', 35));

			Assert.DoesNotContain(lines, string.IsNullOrEmpty);
		}
	}
}
