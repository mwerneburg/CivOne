// CivOne tests
//
// Flight time, hull sizing, and the ETA that read 4000 BC.
//
// The old crossing formula was (4445 + mass) / (100 * engines). Because the mass term
// barely moved against that fixed 4445, each extra engine bought most of its own speed
// back: a maxed hull crossed in 6 years — 0.73c to Alpha Centauri — while the minimum
// ship took a defensible 45. The ceiling was the broken end. Anchoring the best possible
// hull at 0.2c fixes it and lets everything else fall where the physics puts it.

using System.Linq;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class SpaceshipFlightTests
	{
		// The formula tests touch Game's static members without ever building a game, which
		// runs its static constructor — and that reaches Resources and NREs with no runtime
		// registered. They passed initially only because they happened to run after a test
		// that called Sim.NewGame; alone, the whole class failed. Isolation is not optional
		// for a file whose first four tests never construct a Game.
		public SpaceshipFlightTests() => Sim.EnsureRuntime();

		private const float LightYearsToAlphaCentauri = 4.4f;

		// Years -> fraction of c, for asserting on the thing we actually care about.
		private static double FractionOfC(float years) => LightYearsToAlphaCentauri / years;

		// The ceiling. Nothing this game can build may exceed 0.2c.
		[Fact]
		public void NoHullEverExceedsTwoTenthsOfLightSpeed()
		{
			for (int comp = 2; comp <= Game.MAX_SS_COMPONENT; comp += 2)
			for (int mod = 3; mod <= Game.MAX_SS_MODULE; mod += 3)
			{
				int str = Game.SpaceshipStructuresNeeded(comp, mod);
				double c = FractionOfC(Game.SpaceshipFlightYears(str, comp, mod));
				Assert.True(c <= 0.2001, $"hull {str}/{comp}/{mod} cruises at {c:F3}c");
			}
		}

		// ...and some hull actually reaches it, or the cap is just a nerf. Note this is NOT
		// the maxed hull — see below.
		[Fact]
		public void TheFastestBuildableHullReachesExactlyTwoTenthsOfLightSpeed()
		{
			float fastest = float.MaxValue;
			for (int comp = 2; comp <= Game.MAX_SS_COMPONENT; comp += 2)
			for (int mod = 3; mod <= Game.MAX_SS_MODULE; mod += 3)
				fastest = System.Math.Min(fastest,
					Game.SpaceshipFlightYears(Game.SpaceshipStructuresNeeded(comp, mod), comp, mod));

			Assert.Equal(22.0, fastest, 1);
			Assert.Equal(0.2, FractionOfC(fastest), 3);
		}

		// Habitation modules are mass. Eight engines carrying three modules outruns eight
		// engines carrying twelve, so the fastest ship is not the biggest one — which is why
		// anchoring the cap on the maxed hull let a lighter configuration breach it at 0.202c.
		// The trade is the interesting part of the design: colonists cost you speed.
		[Fact]
		public void CarryingMoreColonistsCostsSpeed()
		{
			float light = Game.SpaceshipFlightYears(Game.SpaceshipStructuresNeeded(16, 3), 16, 3);
			float laden = Game.SpaceshipFlightYears(Game.SpaceshipStructuresNeeded(16, 12), 16, 12);

			Assert.True(laden > light,
				$"a full hull ({laden:F1}y) should be slower than a light one ({light:F1}y)");
		}

		// The worst hull is meant to be genuinely bad — a century and a half in transit is
		// the cost of a poor design, and it is what makes configuration a decision.
		[Fact]
		public void TheMinimumHullIsRuinouslySlow()
		{
			float years = Game.SpaceshipFlightYears(15, 2, 3);

			Assert.True(years > 150, $"minimum hull crosses in {years:F0} years, expected >150");
			Assert.True(FractionOfC(years) < 0.03, "a one-engine ship should be well under 0.03c");
		}

		// Monotonic in the direction that matters: more thrust is always faster.
		[Fact]
		public void MoreEnginesIsAlwaysFaster()
		{
			float prev = float.MaxValue;
			for (int comp = 2; comp <= Game.MAX_SS_COMPONENT; comp += 2)
			{
				float years = Game.SpaceshipFlightYears(Game.SpaceshipStructuresNeeded(comp, 3), comp, 3);
				Assert.True(years < prev, $"{comp} components was not faster than {comp - 2}");
				prev = years;
			}
		}

		// ── hull sizing against the deadline ──────────────────────────────────────

		private static (Game game, Player p) ACivWithOutput(int cities, int turn)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0);
			p.Government = new Monarchy();
			p.Explore(45, 25, range: 40);
			for (int i = 0; i < cities; i++)
				g.AddCity(p, i, 25 + (i % 20) * 2, 18 + (i / 20) * 2)!.Size = 12;
			g.GameTurn = (ushort)turn;
			Sim.ClearTasks();
			return (g, p);
		}

		// 2200 AD is turn 750 — the game's own backstop ending.
		[Fact]
		public void TheDeadlineIsTheEndOfTheGame()
		{
			Assert.Equal(750, AI.EndOfGameTurn);
			Assert.True(Common.TurnToYear((ushort)AI.EndOfGameTurn) >= 2200);
		}

		// ...but the deadline that matters leaves room for the Diaspora countdown. Landing is
		// not winning: the colony must then be resupplied for DiasporaStreakTarget turns.
		[Fact]
		public void TheArrivalDeadlineLeavesRoomForTheCountdown()
		{
			Assert.Equal(AI.EndOfGameTurn - (int)Game.DiasporaStreakTarget, AI.ArrivalDeadline);
			Assert.Equal(730, AI.ArrivalDeadline);
		}

		// The boundary pair, and the reason it is these two turns: a minimum hull crosses in
		// 173 turns, so a poor civ starting at 550 lands at 723 and completes the countdown,
		// while one starting at 560 lands at 733 — in time to arrive, too late to win. Under
		// the old arrive-by-750 rule both built a ship; only the second changes.
		[Fact]
		public void APoorCivStillTriesWhenTheMinimumHullCanFinishTheCountdown()
		{
			(Game g, Player p) = ACivWithOutput(cities: 1, turn: 550);

			(int comp, int module) = AI.Instance(p).SpaceshipTarget();

			Assert.Equal(2, comp);
			Assert.Equal(3, module);
		}

		[Fact]
		public void APoorCivAbandonsWhenTheMinimumHullCannotFinishTheCountdown()
		{
			(Game g, Player p) = ACivWithOutput(cities: 1, turn: 560);

			(int comp, int module) = AI.Instance(p).SpaceshipTarget();

			Assert.Equal(0, comp);
			Assert.Equal(0, module);
		}

		// A civ with time in hand still aims for a real ship.
		[Fact]
		public void AnEarlyCivStillPlansAHull()
		{
			(Game g, Player p) = ACivWithOutput(cities: 20, turn: 300);

			(int comp, int module) = AI.Instance(p).SpaceshipTarget();

			Assert.True(comp >= 2 && module >= 3, $"expected a launchable hull, got {comp}/{module}");
		}

		// The point of the change: starting too late means no ship at all, rather than an
		// empire's production poured into a hull still in transit when the game ends.
		[Fact]
		public void ACivThatCannotLandInTimeAbandonsTheProgramme()
		{
			(Game g, Player p) = ACivWithOutput(cities: 20, turn: 740);

			(int comp, int module) = AI.Instance(p).SpaceshipTarget();

			Assert.Equal(0, comp);
			Assert.Equal(0, module);
		}

		// ...and an abandoned programme actually stops the building. Guards the trap that
		// SpaceshipStructuresNeeded(0, 0) is 15, not 0 — falling through would have the civ
		// dutifully building a hull for a ship it has decided not to fly.
		[Fact]
		public void AnAbandonedProgrammeBuildsNoParts()
		{
			(Game g, Player p) = ACivWithOutput(cities: 20, turn: 740);
			var strategy = AI.Instance(p);
			var wants = typeof(AI).GetMethod("WantsSpaceshipPart",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert.NotNull(wants);

			foreach (IProduction part in new IProduction[]
				{ new Buildings.SSStructural(), new Buildings.SSComponent(), new Buildings.SSModule() })
				Assert.False((bool)wants!.Invoke(strategy, new object[] { part })!,
					$"{part.GetType().Name} was still wanted by an abandoned programme");
		}
	}
}
