// CivOne tests
//
// Confront is a chain of if/else-if checked in order, and the first match returns.
// A nuclear missile aimed at an UNDEFENDED enemy city matched the empty-city capture
// branch first, was asked "are you a land unit?", and was refused with "Only land
// units can capture a city" — so a nuke could wipe out a garrison but not an empty
// town. The Nuclear branch sat below and never ran.
//
// Confront is the funnel for every combat interaction in the game, so these also pin
// down the cases that must NOT change.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class NuclearStrikeTests
	{
		// Our unit one tile west of their city, at war so the Senate stays out of it.
		private static (Player mine, Player theirs, City target) Standoff()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			// Neither side may be the human: MoveIsVisible is true for the human's own
			// units, and a visible MoveUnit is an ANIMATION that never completes headless —
			// its Done handler is where capture and detonation actually happen.
			Player[] ps = Game.Instance.Players
				.Where(p => p is not null && Game.Instance.PlayerNumber(p) != 0
				         && p != Game.Instance.HumanPlayer).ToArray();
			Player mine = ps[0], theirs = ps[1];

			mine.Explore(42, 25, range: 8);
			theirs.Explore(44, 25, range: 6);
			City target = Game.Instance.AddCity(theirs, 0, 44, 25)!;
			target.Size = 8;

			mine.DeclareWar(theirs);
			Sim.ClearTasks();
			return (mine, theirs, target);
		}

		// Screen tasks park headless, and the detonation runs in the screen's Done
		// handler — Sim.Settle drains the queue and drops anything that stalls.
		private static void Pump() => Sim.Settle();

		private static IUnit Send(Player mine, UnitType type, int fromX, int fromY, int toX, int toY)
		{
			IUnit u = Game.Instance.CreateUnit(type, fromX, fromY,
				Game.Instance.PlayerNumber(mine))!;
			u.MovesLeft = u.Move;
			u.MoveTo(toX - fromX, toY - fromY);
			Pump();
			return u;
		}

		private static void Strike(Player mine, int fromX, int fromY, int toX, int toY)
			=> Send(mine, UnitType.Nuclear, fromX, fromY, toX, toY);

		// The finding: an UNDEFENDED enemy city is a valid target.
		//
		// The blast itself cannot be asserted headless — it runs in the Done handler of
		// the "nuclearbombdetonation" EventArt screen, and with no renderer that screen
		// never closes. What IS observable is which task the attempt produced: a refusal
		// enqueues a Message ("Only land units can capture a city"), an accepted strike
		// enqueues the MoveUnit that carries the detonation. That is exactly the branch
		// this change moved, so it is the right thing to pin.
		[Fact]
		public void ANukeOnAnUndefendedCity_IsAcceptedNotRefused()
		{
			var (mine, _, target) = Standoff();
			IUnit nuke = Game.Instance.CreateUnit(UnitType.Nuclear, 43, 25,
				Game.Instance.PlayerNumber(mine))!;
			nuke.MovesLeft = nuke.Move;

			nuke.MoveTo(target.X - 43, target.Y - 25);
			string[] queued = Sim.PendingTaskTypes();

			Assert.DoesNotContain(queued, t => t.Contains("Message"));
			Assert.Contains(queued, t => t.Contains("Move"));
		}

		// ...and a defended one behaves identically, which it always did.
		[Fact]
		public void ANukeOnADefendedCity_IsAlsoAccepted()
		{
			var (mine, theirs, target) = Standoff();
			Game.Instance.CreateUnit(UnitType.Musketeers, target.X, target.Y,
				Game.Instance.PlayerNumber(theirs));
			IUnit nuke = Game.Instance.CreateUnit(UnitType.Nuclear, 43, 25,
				Game.Instance.PlayerNumber(mine))!;
			nuke.MovesLeft = nuke.Move;

			nuke.MoveTo(target.X - 43, target.Y - 25);
			string[] queued = Sim.PendingTaskTypes();

			Assert.DoesNotContain(queued, t => t.Contains("Message"));
			Assert.Contains(queued, t => t.Contains("Move"));
		}

		// The city is NOT captured by the strike — a missile is not an occupier. This is
		// the thing the old branch ordering was confused about, in the other direction.
		[Fact]
		public void ANuke_DoesNotCaptureTheCity()
		{
			var (mine, theirs, target) = Standoff();
			byte theirId = Game.Instance.PlayerNumber(theirs);

			Strike(mine, 43, 25, target.X, target.Y);

			Assert.Equal(theirId, target.Owner);
		}

		// ── what the blast does ──────────────────────────────────────────────
		// Tested through Game.ApplyNuclearStrike directly, because in the game it runs
		// inside the detonation screen's Done handler and no screen closes headless.

		// The Civilopedia's first promise: "halves the population of a city".
		[Fact]
		public void AStrike_HalvesTheCityPopulation()
		{
			var (mine, _, target) = Standoff();
			target.Size = 8;

			Game.Instance.ApplyNuclearStrike(target.X, target.Y, mine);

			Assert.Equal(4, target.Size);
		}

		// Halved, not razed — and never below 1, or a missile becomes a map-clearer.
		[Fact]
		public void AStrike_NeverErasesACityOutright()
		{
			var (mine, _, target) = Standoff();
			target.Size = 1;

			Game.Instance.ApplyNuclearStrike(target.X, target.Y, mine);

			Assert.True(target.Size >= 1, "a nuke halves a city, it does not raze it");
		}

		// The second promise: "the ground it touches is left POLLUTED" — across the whole
		// 3x3, and now worth half a tile's yield.
		[Fact]
		public void AStrike_LeavesFalloutAcrossTheBlast()
		{
			var (mine, _, target) = Standoff();

			Game.Instance.ApplyNuclearStrike(target.X, target.Y, mine);

			Assert.True(Map.Instance[target.X - 1, target.Y].Pollution);
			Assert.True(Map.Instance[target.X + 1, target.Y + 1].Pollution);
		}

		// ...but not on the city tile itself, matching the ordinary pollution roll, and
		// never on water.
		[Fact]
		public void Fallout_SparesTheCityTileAndTheSea()
		{
			var (mine, _, target) = Standoff();
			Map.Instance.ChangeTileType(target.X, target.Y - 1, Terrain.Ocean);

			Game.Instance.ApplyNuclearStrike(target.X, target.Y, mine);

			Assert.False(Map.Instance[target.X, target.Y].Pollution, "the city tile is spared");
			Assert.False(Map.Instance[target.X, target.Y - 1].Pollution, "the sea is spared");
		}

		// Units in the blast still die — the effect that always worked must survive the
		// refactor out of the screen handler.
		[Fact]
		public void AStrike_StillDestroysEveryUnitInTheBlast()
		{
			var (mine, theirs, target) = Standoff();
			byte theirId = Game.Instance.PlayerNumber(theirs);
			IUnit garrison = Game.Instance.CreateUnit(UnitType.Musketeers, target.X, target.Y, theirId)!;
			IUnit neighbour = Game.Instance.CreateUnit(UnitType.Musketeers, target.X + 1, target.Y, theirId)!;

			Game.Instance.ApplyNuclearStrike(target.X, target.Y, mine);

			Assert.DoesNotContain(garrison, Game.Instance.GetUnits());
			Assert.DoesNotContain(neighbour, Game.Instance.GetUnits());
		}

		// The guard that must not have moved: a Bomber still cannot walk into an empty
		// enemy city. Only the Nuclear case was meant to change.
		[Fact]
		public void ABomber_StillCannotTakeAnEmptyCity()
		{
			var (mine, theirs, target) = Standoff();
			byte theirId = Game.Instance.PlayerNumber(theirs);

			IUnit bomber = Send(mine, UnitType.Bomber, 43, 25, target.X, target.Y);

			Assert.Equal(theirId, target.Owner);
			Assert.Contains(bomber, Game.Instance.GetUnits());
		}

		// And a land unit still CAN take an empty city — the capture path is untouched.
		[Fact]
		public void ALandUnit_StillTakesAnEmptyCity()
		{
			var (mine, theirs, target) = Standoff();
			byte mineId = Game.Instance.PlayerNumber(mine);

			Send(mine, UnitType.Legion, 43, 25, target.X, target.Y);

			Assert.Equal(mineId, target.Owner);
		}
	}
}
