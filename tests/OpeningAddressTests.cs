// CivOne tests
//
// The first screen of a new game. It has been showing a fragment — "Alphabet, and Roads." —
// with nothing before it, for two independent reasons:
//
//   1. The address itself comes from KING.TXT, a DOS asset. Asset-free mode ships no *.TXT,
//      so GetGameText returned an empty array and the entire lead-in silently vanished. The
//      same class of silent degradation the ERROR/* fallbacks were written for.
//   2. The list was built as "{name}, " per advance plus a hard-coded "and Roads.", which
//      only reads as English at three or more starting advances.
//
// Both are pinned here, because both fail QUIETLY: no exception, no missing-file warning in
// Release, just a sentence that doesn't parse.

using System.Linq;
using CivOne.IO;
using CivOne.Screens;

namespace CivOne.Tests
{
	public class OpeningAddressTests
	{
		// ── the address ──────────────────────────────────────────────────────────

		// Without DOS assets this must still produce text. An empty array here is the whole
		// bug: the screen draws nothing and starts mid-sentence.
		[Fact]
		public void TheOpeningAddressSurvivesWithoutDosAssets()
		{
			Sim.EnsureRuntime();

			string[] lines = TextFile.Instance.GetGameText("KING/INIT");

			Assert.NotEmpty(lines);
			Assert.Contains(lines, l => l.Length > 0);
		}

		// NewGame substitutes both tokens. A fallback that hard-coded a leader or tribe would
		// address the wrong person, which is worse than saying nothing.
		[Fact]
		public void TheAddressNamesTheLeaderAndTheTribe()
		{
			Sim.EnsureRuntime();

			string all = string.Join("\n", TextFile.Instance.GetGameText("KING/INIT"));

			Assert.Contains("$RPLC1", all);
			Assert.Contains("$US", all);
		}

		// Its last line runs straight into the advances sentence, so it must not be
		// terminated — the two are one sentence split across a screen-drawing boundary.
		[Fact]
		public void TheAddressRunsIntoTheAdvancesList()
		{
			Sim.EnsureRuntime();

			string last = TextFile.Instance.GetGameText("KING/INIT")
				.Where(l => l.Length > 0).Last();

			Assert.False(last.EndsWith("."), $"the lead-in terminates itself: \"{last}\"");
		}

		// ── the sentence ─────────────────────────────────────────────────────────

		// The shapes that were wrong. One advance gave "Alphabet, and Roads." and none gave a
		// sentence opening with "and".
		[Theory]
		[InlineData(new string[0],                        "Roads.")]
		[InlineData(new[] { "Alphabet" },                 "Alphabet and Roads.")]
		[InlineData(new[] { "Alphabet", "Masonry" },      "Alphabet, Masonry and Roads.")]
		[InlineData(new[] { "Alphabet", "Masonry", "Pottery" }, "Alphabet, Masonry, Pottery and Roads.")]
		public void TheListReadsAsASentence(string[] advances, string expected)
		{
			Assert.Equal(expected, Sentence(advances));
		}

		// Roads is not an advance and is never absent — it is what every tribe can do on turn
		// one — so it is always the final item however many advances there are.
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(5)]
		public void RoadsIsAlwaysLast(int count)
		{
			string s = Sentence(Enumerable.Range(0, count).Select(i => $"Tech{i}").ToArray());

			Assert.EndsWith("Roads.", s);
			Assert.Equal(1, s.Split(new[] { "Roads" }, System.StringSplitOptions.None).Length - 1);
		}

		private static string Sentence(string[] advances) => NewGame.KnownSentence(advances);

		// The source is the thing that ships, so pin that it uses the width wrap rather than
		// the old every-second-advance break, which split "Advanced Flight, Bronze Working"
		// and "Code, Trade" at the same place regardless of width.
		[Fact]
		public void TheSentenceIsWrappedOnWidthNotOnCount()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Screens", "NewGame.cs"));

			Assert.Contains("WrapToWidth(sentence", src);
			Assert.Contains("KnownSentence(Human.Advances", src);
			Assert.DoesNotContain("sb.Append(\"and Roads.\")", src);
			Assert.DoesNotContain("if (i % 2 == 0) sb.Append(\"|\")", src);
		}

		// ── the wrap ─────────────────────────────────────────────────────────────

		// Never zero lines: an empty sentence that drew nothing would reintroduce exactly the
		// silence this whole file exists to stop.
		[Fact]
		public void TheWrapAlwaysYieldsALine()
		{
			Sim.EnsureRuntime();

			Assert.Single(NewGame.WrapToWidth("", 100));
		}

		// A word longer than the line is kept whole rather than dropped or looped on.
		[Fact]
		public void AnOverlongWordIsNotLost()
		{
			Sim.EnsureRuntime();

			string[] lines = NewGame.WrapToWidth("Philosophy", 1);

			Assert.Single(lines);
			Assert.Equal("Philosophy", lines[0]);
		}

		// The point of measuring: a narrow column must break more often than a wide one.
		[Fact]
		public void ANarrowerColumnBreaksMoreOften()
		{
			Sim.EnsureRuntime();
			const string s = "Alphabet, Bronze Working, Ceremonial Burial, Masonry and Roads.";

			Assert.True(NewGame.WrapToWidth(s, 60).Length > NewGame.WrapToWidth(s, 300).Length,
				"the wrap is not responding to width");
		}
	}
}
