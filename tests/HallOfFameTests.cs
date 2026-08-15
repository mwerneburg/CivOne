// CivOne tests
//
// One entry per game.
//
// AddAndSave appended a row every time it was called, and it is called from EndSequence — so a
// game that reaches an ending more than once (a milestone ending and then a later one, or a
// save reloaded and finished again) filled the table with near-copies of itself, crowding out
// every other run.
//
// The key is the decision log's game id, which is usable for this only because it now survives
// a save/load (see DecisionLogger.BeginGame). Before that it changed on every reload, so the
// same run would have looked like several different games here too.

using System.IO;
using System.Linq;
using CivOne.Persistence;

namespace CivOne.Tests
{
	public class HallOfFameTests
	{
		private static string Path_ => System.IO.Path.Combine(Settings.Instance.SavesDirectory, "civone.hof");

		private static Player AGame()
		{
			Sim.NewGame(width: 80, height: 50);
			if (File.Exists(Path_)) File.Delete(Path_);
			return Game.Instance.HumanPlayer;
		}

		[Fact]
		public void OneGameLeavesOneEntryHoweverOftenItEnds()
		{
			Player p = AGame();

			HallOfFame.AddAndSave(p, "Conquest", "1500 AD");
			HallOfFame.AddAndSave(p, "Score", "2100 AD");

			Assert.Single(HallOfFame.Load());
		}

		// The later ending is the one that stands — it is the game's actual outcome.
		[Fact]
		public void TheLatestEndingIsTheOneKept()
		{
			Player p = AGame();

			HallOfFame.AddAndSave(p, "Conquest", "1500 AD");
			HallOfFame.AddAndSave(p, "Diaspora", "2200 AD");

			HofEntry only = HallOfFame.Load().Single();
			Assert.Equal("Diaspora", only.Victory);
			Assert.Equal("2200 AD", only.Year);
		}

		// Different games still get their own rows, or the table only ever holds one.
		[Fact]
		public void DifferentGamesEachKeepAnEntry()
		{
			Player first = AGame();
			HallOfFame.AddAndSave(first, "Conquest", "1500 AD");

			Sim.NewGame(width: 80, height: 50);   // a new game means a new id
			HallOfFame.AddAndSave(Game.Instance.HumanPlayer, "Score", "2100 AD");

			Assert.Equal(2, HallOfFame.Load().Count);
		}

		// A reloaded game is the SAME game, which is the whole reason the id had to survive
		// save/load. Without that this file would be testing nothing.
		[Fact]
		public void AGameFinishedAfterAReloadDoesNotAddASecondRow()
		{
			Player p = AGame();
			HallOfFame.AddAndSave(p, "Conquest", "1500 AD");
			string save = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "hof.cos");
			Game.Instance.SaveCos(save);

			Sim.ResetState();
			Assert.True(Game.LoadCos(save), "LoadCos should succeed");
			HallOfFame.AddAndSave(Game.Instance.HumanPlayer, "Score", "2100 AD");

			Assert.Single(HallOfFame.Load());
		}

		// Entries written before the id existed all carry an empty one, and matching on that
		// would collapse every historical run into a single row — so the dedupe is guarded on
		// a non-empty id.
		//
		// The guard bites only when the INCOMING entry's id is empty, which is why this test
		// blanks it. The first version staged three legacy rows and then added a normal entry,
		// and passed with the guard removed: a real id matches none of the empty ones, so
		// nothing was ever at risk. An empty id matches all of them.
		[Fact]
		public void AnEntryWithNoIdDoesNotWipeTheLegacyRows()
		{
			AGame();
			File.WriteAllLines(Path_, new[]
			{
				"Caesar|Romans|900|Conquest|1200 AD",
				"Hiawatha|Haudenosaunee|700|Score|2100 AD",
				"Mansa Musa|Malians|1100|Diaspora|2200 AD",
			});
			Assert.All(HallOfFame.Load(), e => Assert.Equal(string.Empty, e.GameId));

			// A logger that never started, or a caller that runs before BeginGame.
			var field = typeof(DecisionLogger).GetField("_gameId",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
			object? saved = field.GetValue(null);
			field.SetValue(null, string.Empty);
			try
			{
				HallOfFame.AddAndSave(Game.Instance.HumanPlayer, "Score", "2200 AD");
			}
			finally { field.SetValue(null, saved); }

			Assert.Equal(4, HallOfFame.Load().Count);
		}

		// A five-field file is what every existing installation has on disk.
		[Fact]
		public void AnOlderFileStillLoads()
		{
			AGame();
			File.WriteAllLines(Path_, new[] { "Caesar|Romans|900|Conquest|1200 AD" });

			HofEntry only = HallOfFame.Load().Single();

			Assert.Equal("Caesar", only.LeaderName);
			Assert.Equal(900, only.Score);
			Assert.Equal(string.Empty, only.GameId);
		}
	}
}
