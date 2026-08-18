// CivOne tests
//
// The zone-of-control refusal is the one notice that visibly stutters an unattended run.
//
// It is correctly gated to the human's own units — but under Autopilot the AI moves those
// units too, so a refusal a person would see ONCE (they make one move and read it) fires on
// every blocked attempt the AI makes. Each is a modal error the task queue must dwell on and
// dismiss, which is why this message alone pauses the game while every other notice passes
// unnoticed.
//
// Reported from a running autoplay loop: "a single popup that seems to linger for a couple of
// beats unlike all the rest... the whole game seems to pause while it is displaying — but only
// that one."
//
// A person playing normally must still be told: the rule stops a unit two tiles from a city
// it is at war with, and looks for all the world like a bug in the map if unexplained.

using System.Linq;

namespace CivOne.Tests
{
	public class ZocNoticeTests
	{
		private static string UnitSource()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Units", "BaseUnit.cs"));
		}

		// Suppressed when the AI is driving — nobody is reading it, and the AI re-plans the
		// move regardless.
		[Fact]
		public void TheNoticeIsSuppressedUnderAutopilot()
		{
			string src = UnitSource();
			int at = src.IndexOf("ERROR/ZOC");
			Assert.True(at > 0, "the zone-of-control notice has moved or been rewritten");
			string block = src.Substring(System.Math.Max(0, at - 300), 320);

			Assert.Contains("!Settings.Instance.Autopilot", block);
		}

		// ...but a person still gets told. Removing the notice altogether would leave a unit
		// refused two tiles from an enemy city with no explanation at all.
		[Fact]
		public void APersonIsStillTold()
		{
			string src = UnitSource();
			int at = src.IndexOf("ERROR/ZOC");
			string block = src.Substring(System.Math.Max(0, at - 300), 320);

			Assert.Contains("Human == Owner", block);
		}

		// The text itself must survive — asset-free mode ships no *.TXT, and an empty message
		// box is how this rule used to be explained to nobody.
		[Fact]
		public void TheNoticeStillHasWords()
		{
			Sim.EnsureRuntime();

			string[] lines = CivOne.IO.TextFile.Instance.GetGameText("ERROR/ZOC");

			Assert.NotEmpty(lines);
			Assert.Contains(lines, l => l.Length > 0);
		}
	}
}
