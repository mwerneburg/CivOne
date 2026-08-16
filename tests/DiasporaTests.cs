// CivOne tests
//
// Diaspora — the sixth victory path, and the one the retired Space Race insta-win became.
//
// Arriving at Alpha Centauri founds a colony; it does not win anything. The colony is not
// self-sufficient, it is being resupplied, and the resupply runs from one city on Earth. So
// the win is twenty consecutive turns with Mission Control standing — a known city, on the
// map, that anybody who wants to stop you can march on.
//
// Unlike the other paths there is no rivals clause and no war clause: this measures nothing
// about standing, only whether the lifeline held.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class DiasporaTests
	{
		// Colony founded, Mission Control built, nothing else going on.
		private static (Game g, Player human, City hq) AColonyAndItsLifeline(bool lifeline = true)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			Map.Instance.ChangeTileType(38, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			human.Explore(38, 25, range: 5);
			human.AddAdvance(new SpaceFlight(), false);

			City hq = g.AddCity(human, 0, 38, 25)!;
			hq.Size = 4;
			if (lifeline) hq.AddBuilding(new MissionControl());

			g.ColonyFounded[g.PlayerNumber(g.HumanPlayer)] = true;
			return (g, human, hq);
		}

		// The streak is counted in EndTurn's phase B, which runs once _currentPlayer wraps.
		// Driven directly for the same reason SpaceRaceNotAVictoryTests does: Sim.RunTurns
		// plays the game, and this fixture gives it almost nothing to play.
		//
		// Counted by GameTurn rather than by EndTurn calls: _currentPlayer keeps its offset
		// across calls, so a fixed "Players.Count + 1" burst wraps once usually and twice
		// sometimes — which is exactly how an early draft saw a 2-round streak read 3.
		private static void PlayRounds(Game g, int rounds)
		{
			uint target = g.GameTurn + (uint)rounds;
			while (g.GameTurn < target)
			{
				Sim.ClearTasks();
				g.EndTurn();
			}
		}

		[Fact]
		public void TheStreakRunsWhileMissionControlStands()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();

			PlayRounds(g, 3);

			Assert.Equal(3u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);
		}

		// No colony, no clock — Mission Control on its own counts for nothing.
		[Fact]
		public void MissionControlWithoutAColonyCountsNothing()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();
			g.ColonyFounded[g.PlayerNumber(g.HumanPlayer)] = false;

			PlayRounds(g, 3);

			Assert.Equal(0u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);
		}

		// Losing the city resets the clock. This is the whole point of the building: a
		// spaceship already under way used to be untouchable, and now there is somewhere to
		// attack.
		[Fact]
		public void LosingMissionControlResetsTheStreak()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();
			PlayRounds(g, 3);
			Assert.Equal(3u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);

			hq.RemoveBuilding<MissionControl>();
			PlayRounds(g, 1);

			Assert.Equal(0u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);
		}

		// ...and rebuilding starts a fresh twenty, not a resumed one.
		[Fact]
		public void RebuildingStartsTheClockOverRatherThanResuming()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();
			PlayRounds(g, 5);
			hq.RemoveBuilding<MissionControl>();
			PlayRounds(g, 1);

			hq.AddBuilding(new MissionControl());
			PlayRounds(g, 2);

			Assert.Equal(2u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);
		}

		[Fact]
		public void TwentyTurnsWinsIt()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();
			int before = human.MilestoneScore;

			// Three rounds PAST the target on purpose. The win enqueues a screen chain ending
			// in Runtime.Quit(), which headlessly never arrives — and in the real game the
			// queue takes several rounds to drain either way, during which the block keeps
			// seeing a streak of 20. An unlatched win awarded the 200 once per round: the
			// first draft of this test read +600.
			PlayRounds(g, (int)Game.DiasporaStreakTarget + 3);

			Assert.True(g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)] >= Game.DiasporaStreakTarget,
				$"streak reached only {g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]}");
			// 400, not 200: the first colony in a world now carries the first-mover premium
			// (Game.DiasporaAward). A fixture that sets ColonyFounded directly leaves
			// ColonyOrder at 0, which DiasporaAward reads as "the only colony we know of".
			Assert.Equal(before + 400, human.MilestoneScore);
		}

		// The collision the twenty turns buys: the Ascension can finish inside the clock, and
		// the Vessel's destination is the one other inhabited place anybody ever broadcast to.
		[Fact]
		public void TheVesselTakesTheColonyWithIt()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();
			PlayRounds(g, 3);
			Assert.Equal(3u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);

			// Give the organism something to leave from. InfectCity is how a city changes
			// hands to it; the departure razes whatever it holds.
			City taken = g.AddCity(g.Players.First(p => p is not null && p != human && g.PlayerNumber(p) != 0),
				1, 44, 25)!;
			taken.Size = 4;
			g.InfectCity(taken);
			Player thing = g.Players.First(p => p is not null && p.Civilization is Civilizations.TheThing);

			Sim.ClearTasks();
			g.ExecuteThingDeparture(thing.Cities.First());

			Assert.False(g.ColonyFounded[g.PlayerNumber(g.HumanPlayer)], "the colony survived the organism");
			Assert.Equal(0u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);
		}

		// ...and it does not keep counting afterwards. Founding it again means another ship.
		[Fact]
		public void AfterTheBreachTheClockDoesNotRestartByItself()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();
			g.ColonyFounded[g.PlayerNumber(g.HumanPlayer)] = false;   // as ExecuteThingDeparture leaves it

			PlayRounds(g, 5);

			Assert.Equal(0u, g.DiasporaStreak[g.PlayerNumber(g.HumanPlayer)]);
		}

		[Fact]
		public void TheStreakRoundTripsThroughASave()
		{
			(Game g, Player human, City hq) = AColonyAndItsLifeline();
			PlayRounds(g, 4);
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "diaspora.cos");

			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.True(Game.Instance.ColonyFounded[Game.Instance.PlayerNumber(Game.Instance.HumanPlayer)], "the colony did not survive the save");
			Assert.Equal(4u, Game.Instance.DiasporaStreak[Game.Instance.PlayerNumber(Game.Instance.HumanPlayer)]);
		}

		// The breach plate. EventArtScreen.FindPath returns null on a miss and the event
		// simply shows no picture — silent, which is why this file is demanded rather than
		// trusted, exactly as ProbeContactArtTests and LeaderPortraitTests do.
		[Fact]
		public void TheBreachArtIsShipped()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string path = System.IO.Path.Combine(dir!.FullName, "runtime", "sdl", "Resources",
				"defaults", "data", "event_art", "ColonyBreached.png");

			Assert.True(System.IO.File.Exists(path), $"colony breach art is missing: {path}");
		}
	}
}
