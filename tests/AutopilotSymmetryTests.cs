// CivOne tests
//
// The AI plays asymmetrically against a person: diplomats put the human's cities first,
// ConsiderDiplomacy aims every demand at them, and HumanIsDominant steers attacks their
// way. Difficulty design when someone is playing; a dogpile when nobody is.
//
// Measured across four consecutive unattended 750-turn games: the human slot was the
// only civ to collapse. In the 2026-08-03 run the Russians went 4 cities -> 10 by turn
// 100 -> 4 -> 2 -> 1 -> 0, while every other civ grew monotonically (Arabs 2 -> 75,
// French 4 -> 76).
//
// Both directions matter. Turning the dogpile off under autopilot is the fix; leaving a
// played game exactly as it was is the constraint, because the play-tester stops
// autopilot to look around and must be treated as the human again immediately.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class AutopilotSymmetryTests
	{
		// An AI civ, the human's city to its west, and a rival AI city nearer to its east.
		// Which one a diplomat picks is the test.
		private static (Player ai, City humanCity, City rivalCity) TwoTargets()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 28; x <= 56; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			Player[] ais = g.Players
				.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != human).ToArray();
			Player ai = ais[0], rival = ais[1];

			ai.Explore(42, 25, range: 20);
			g.AddCity(ai, 0, 42, 25);
			// The rival is CLOSER (4 tiles) than the human (6). Nearest-first therefore picks
			// the rival, so choosing the human can only be the human-priority clause — an
			// equidistant pair would be settled by city enumeration order instead.
			City humanCity = g.AddCity(human, 1, 36, 25)!;
			City rivalCity = g.AddCity(rival, 2, 46, 25)!;
			Sim.ClearTasks();
			return (ai, humanCity, rivalCity);
		}

		// AI.Move both sets Goto and moves the unit, so a reused diplomat carries position and
		// order state into the next decision. Each call gets a fresh one on the start tile.
		private static (int X, int Y) TargetOf(Player ai)
		{
			Game g = Game.Instance;
			IUnit dip = g.CreateUnit(UnitType.Diplomat, 42, 25, g.PlayerNumber(ai))!;
			dip.MovesLeft = dip.Move;
			// IdleRetryTurn defers most idle units; align the turn so this one is processed.
			g.GameTurn = (ushort)((dip.X + dip.Y) & 7);
			AI.Instance(ai).Move(dip);
			var target = (dip.Goto.X, dip.Goto.Y);
			if (g.GetUnits().Contains(dip)) g.DisbandUnit(dip);
			return target;
		}

		// Autopilot ON: the diplomat must not single out the human.
		[Fact]
		public void UnderAutopilot_DiplomatsDoNotPreferTheHumansCities()
		{
			var (ai, humanCity, rivalCity) = TwoTargets();
			Settings.Instance.Autopilot = true;
			try
			{
				var target = TargetOf(ai);
				Assert.NotEqual((humanCity.X, humanCity.Y), target);
			}
			finally { Settings.Instance.Autopilot = false; }
		}

		// Autopilot OFF: the played game is unchanged — the human is still the priority.
		// This is the half that protects the play-tester's experience.
		[Fact]
		public void WithAPlayerAtTheControls_DiplomatsStillPreferTheHumansCities()
		{
			var (ai, humanCity, _) = TwoTargets();
			Settings.Instance.Autopilot = false;

			var target = TargetOf(ai);

			Assert.Equal((humanCity.X, humanCity.Y), target);
		}

		// The gate is read live, not latched: stopping autopilot to look around restores
		// normal behaviour on the very next decision, and resuming hands it back.
		[Fact]
		public void TheGate_FollowsAutopilotWithinASingleGame()
		{
			var (ai, humanCity, _) = TwoTargets();
			try
			{
				Settings.Instance.Autopilot = false;
				Assert.Equal((humanCity.X, humanCity.Y), TargetOf(ai));

				Settings.Instance.Autopilot = true;
				Assert.NotEqual((humanCity.X, humanCity.Y), TargetOf(ai));

				Settings.Instance.Autopilot = false;
				Assert.Equal((humanCity.X, humanCity.Y), TargetOf(ai));
			}
			finally { Settings.Instance.Autopilot = false; }
		}

		// The diplomacy channel is human-directed with no AI-to-AI equivalent, so under
		// autopilot every civ's tribute demands and cede-city ultimatums land on one player.
		[Fact]
		public void UnderAutopilot_NoDiplomaticDemandsAreAimedAtTheHuman()
		{
			var (ai, _, _) = TwoTargets();
			Settings.Instance.Autopilot = true;
			try
			{
				Sim.ClearTasks();
				AI.Instance(ai).ConsiderDiplomacy();
				Assert.Empty(Sim.PendingTaskTypes());
			}
			finally { Settings.Instance.Autopilot = false; }
		}
	}
}
