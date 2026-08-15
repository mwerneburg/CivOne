// CivOne tests
//
// One id per game, one per load.
//
// A reload used to mint a new game id unconditionally, which cut a single run into unrelated
// pieces. EndGame writes `game_outcome` under whatever id is current when the game ends, so
// only the LAST segment was ever scored — and the notebook weights every decision by its
// game's score.
//
// Measured on the live log before the fix: four game ids, every one starting mid-game (turns
// 311, 673, 696) — two runs, each cut in half by a reload — and 62,772 of 69,491 records
// belonging to a segment with no outcome at all. Ninety percent of the training signal was
// dropped or median-filled, and the part lost was the EARLY game, which is where the founding
// and expansion decisions live.
//
// Reusing the id on its own would not do, because branching is real: load one save twice, play
// differently, and those are two genuine futures that must stay tellable apart. Hence a stable
// GAME id and a SESSION id that changes on every load.

using System.IO;
using System.Linq;

namespace CivOne.Tests
{
	public class DecisionLogIdentityTests
	{
		private static string Save(string name)
		{
			string path = Path.Combine(Settings.Instance.SavesDirectory, name);
			Game.Instance.SaveCos(path);
			return path;
		}

		// The whole point: the run keeps its identity across a save/load.
		[Fact]
		public void AReloadedGameKeepsItsGameId()
		{
			Sim.NewGame(width: 80, height: 50);
			string before = DecisionLogger.GameId;
			Assert.False(string.IsNullOrEmpty(before), "fixture: a new game should have an id");
			string path = Save("logid.cos");

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "LoadCos should succeed");

			Assert.Equal(before, DecisionLogger.GameId);
		}

		// ...and the session does not, which is what keeps two divergent loads of one save
		// distinguishable.
		[Fact]
		public void EachLoadIsANewSession()
		{
			Sim.NewGame(width: 80, height: 50);
			string path = Save("logsession.cos");
			string firstSession = SessionId();

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "LoadCos should succeed");

			Assert.NotEqual(firstSession, SessionId());
		}

		// Two loads of the SAME save share a game id and differ by session — the branch case.
		[Fact]
		public void TwoBranchesOfOneSaveShareAGameIdAndDifferBySession()
		{
			Sim.NewGame(width: 80, height: 50);
			string path = Save("logbranch.cos");
			string game = DecisionLogger.GameId;

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));
			string sessionA = SessionId();

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));
			string sessionB = SessionId();

			Assert.Equal(game, DecisionLogger.GameId);
			Assert.NotEqual(sessionA, sessionB);
		}

		// A NEW game is a new run, not a continuation.
		[Fact]
		public void ANewGameGetsAFreshId()
		{
			Sim.NewGame(width: 80, height: 50);
			string first = DecisionLogger.GameId;

			Sim.NewGame(width: 80, height: 50);

			Assert.NotEqual(first, DecisionLogger.GameId);
		}

		// Saves written before the field existed carry no id. They must still load, and get a
		// fresh one rather than an empty string that would collapse every old run into one
		// bucket in the log.
		[Fact]
		public void ASaveWithoutTheFieldStillLoads()
		{
			Sim.NewGame(width: 80, height: 50);
			string path = Save("loglegacy.cos");
			string text = File.ReadAllText(path);
			Assert.Contains("DecisionGameId", text);
			File.WriteAllText(path, string.Join("\n",
				text.Split('\n').Where(l => !l.Contains("DecisionGameId"))));

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "an older save must still load");

			Assert.False(string.IsNullOrEmpty(DecisionLogger.GameId));
		}

		private static string SessionId()
			=> (string)typeof(DecisionLogger)
				.GetField("_sessionId", System.Reflection.BindingFlags.NonPublic
				                      | System.Reflection.BindingFlags.Static)!
				.GetValue(null)!;
	}
}
