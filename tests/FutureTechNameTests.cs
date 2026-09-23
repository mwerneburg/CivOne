// CivOne tests
//
// Future Techs have names now: "complete Future Tech #7: Silent Velcro!" (the user's list,
// Sep 2026). Shuffled per game, stable across a reload, and a full round before any repeat,
// which comes back as the next mark.

using System.Linq;
using CivOne.Advances;

namespace CivOne.Tests
{
	public class FutureTechNameTests
	{
		private static int Count => FutureTech.Breakthroughs.Length;

		[Fact]
		public void EveryNameIsUsedOnceBeforeAnyRepeats()
		{
			string[] round = Enumerable.Range(1, Count).Select(n => FutureTech.Breakthrough(n, "game-a")).ToArray();

			Assert.Equal(Count, round.Distinct().Count());
			Assert.Empty(round.Except(FutureTech.Breakthroughs));
		}

		[Fact]
		public void TheSecondRoundIsMarkTwo()
		{
			Assert.Equal(FutureTech.Breakthrough(1, "game-a") + " Mk II", FutureTech.Breakthrough(Count + 1, "game-a"));
			Assert.EndsWith(" Mk 9", FutureTech.Breakthrough(Count * 8 + 1, "game-a"));
		}

		// A reload must not reshuffle: the same game id gives the same order.
		[Fact]
		public void TheOrderIsStableForAGameAndDiffersBetweenGames()
		{
			string[] a1 = Enumerable.Range(1, Count).Select(n => FutureTech.Breakthrough(n, "game-a")).ToArray();
			string[] a2 = Enumerable.Range(1, Count).Select(n => FutureTech.Breakthrough(n, "game-a")).ToArray();
			string[] b  = Enumerable.Range(1, Count).Select(n => FutureTech.Breakthrough(n, "game-b")).ToArray();

			Assert.Equal(a1, a2);
			Assert.NotEqual(a1, b);
		}

		// The newspaper grows to its widest line; a headline plus a mark must stay short.
		[Fact]
		public void HeadlinesFitTheNewspaper()
		{
			Assert.All(FutureTech.Breakthroughs, name => Assert.True(name.Length <= 26, $"too long: {name}"));
		}

		// Pinned at the source: the notice is a screen, which cannot be opened headless.
		[Fact]
		public void TheNoticeNamesTheBreakthrough()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(Sim.RepoRoot(), "src", "Tasks", "ProcessScience.cs"));

			Assert.Contains("FutureTech.Breakthrough(n, DecisionLogger.GameId)", src);
		}
	}
}
