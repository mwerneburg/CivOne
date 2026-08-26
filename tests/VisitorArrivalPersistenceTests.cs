// CivOne tests
//
// The turn the visitors landed did not survive a save.
//
// VisitorsArrivedTurn is set in memory when the Olvir make landfall and is the gate on their
// gift of the exotic fuel: Game.cs requires `VisitorsArrivedTurn > 0` and then
// OlvirFuelGiftTurns of patience. It was never written to the .cos file, so every reload put
// it back to zero and the gift became unreachable — permanently, since nothing sets it again.
//
// The fuel is what unlocks spaceship construction at all (Player.cs), so in a Refugees game
// this is the entire opening of the science path. It went unnoticed because landfall and the
// gift usually fall inside one session: measured in game 3de868a5, landfall turn 470 and
// has_fuel true at turn 520 for three civilizations, all without a reload in between.

using System.IO;
using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class VisitorArrivalPersistenceTests
	{
		private static string SaveTo(string name)
		{
			string path = Path.Combine(Settings.Instance.SavesDirectory, name);
			Game.Instance.SaveCos(path);
			return path;
		}

		// The report: the landing date survives the round trip.
		[Fact]
		public void TheArrivalTurnSurvivesASave()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			g.VisitorsArrived = true;
			g.VisitorsArrivedTurn = 470;
			string path = SaveTo("visitorturn.cos");

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.Equal(470u, Game.Instance.VisitorsArrivedTurn);
		}

		// ...and it is actually in the file, not merely surviving because the object was
		// reused. A ResetState that missed a static would hide the whole defect.
		[Fact]
		public void TheArrivalTurnIsWrittenToTheFile()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game.Instance.VisitorsArrived = true;
			Game.Instance.VisitorsArrivedTurn = 470;

			Assert.Contains("VisitorsArrivedTurn: 470", File.ReadAllText(SaveTo("visitorturn2.cos")));
		}

		// An older save has the field nowhere, but it does record THAT the visitors arrived.
		// The landing is therefore in the past, and the wait must count as served — not owed
		// forever, which is the bug this fixes wearing different clothes.
		[Fact]
		public void AnOlderSaveTreatsTheLandingAsPast()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game.Instance.VisitorsArrived = true;
			Game.Instance.VisitorsArrivedTurn = 470;
			string path = SaveTo("visitorlegacy.cos");
			string text = File.ReadAllText(path);
			File.WriteAllText(path, string.Join("\n",
				text.Split('\n').Where(l => !l.Contains("VisitorsArrivedTurn"))));

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "an older save must still load");

			Game g = Game.Instance;
			Assert.True(g.VisitorsArrived);
			Assert.True(g.VisitorsArrivedTurn > 0, "the gift is gated on this being non-zero");
			Assert.True(g.GameTurn - g.VisitorsArrivedTurn >= Game.OlvirFuelGiftTurns,
				"an old save should have served its wait, not restarted it");
		}

		// A game where they have NOT arrived must not fabricate a landing date — that would
		// arm the gift before the Olvir are even in the sky.
		[Fact]
		public void ANoLandingSaveStaysAtZero()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Assert.False(Game.Instance.VisitorsArrived, "fixture: nobody has landed yet");
			string path = SaveTo("visitornone.cos");

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.Equal(0u, Game.Instance.VisitorsArrivedTurn);
		}

		// The gate itself, pinned at the source: if the condition stops reading the turn, the
		// three tests above are measuring a field nothing uses.
		[Fact]
		public void TheGiftIsStillGatedOnTheArrivalTurn()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(), "src", "Game.cs"));

			Assert.Contains("VisitorsArrivedTurn > 0 && _gameTurn - VisitorsArrivedTurn >= OlvirFuelGiftTurns", src);
		}
	}
}
