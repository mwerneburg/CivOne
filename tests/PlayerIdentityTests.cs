// CivOne tests
//
// Game.PlayerNumber returns the index in _players, and 0 when the player is not in the
// list at all — but 0 is ALSO the Barbarians. The two were indistinguishable, so a Player
// belonging to no game read as the Barbarian player: `p == 0` was true, and since
// Player.Cities filters on `this == c.Owner`, such a Player inherited the Barbarians'
// cities. IsDestroyed() then took its `if (this == 0) return false` branch and reported a
// cityless civ as alive forever.
//
// The Barbarian guard itself is load-bearing and correct — the Barbarians hold no cities,
// so without it IsDestroyed would disband every barbarian unit and mark them destroyed on
// the first call. These pin both halves.

using System.Linq;
using CivOne;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class PlayerIdentityTests
	{
		private static Player AnAICiv()
		{
			Game g = Game.Instance;
			return g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			                         && p != g.HumanPlayer);
		}

		// Slot 0 is the Barbarians. Everything below depends on this.
		[Fact]
		public void SlotZero_IsTheBarbarians()
		{
			Sim.NewGame(width: 80, height: 50);
			Assert.IsType<Barbarian>(Game.Instance.GetPlayer(0).Civilization);
		}

		// A Player from a previous game belongs to no current one, and must not be mistaken
		// for player 0. This is the defect.
		[Fact]
		public void APlayerFromAnotherGame_IsNotTheBarbarians()
		{
			Sim.NewGame(width: 80, height: 50);
			Player stale = AnAICiv();

			Sim.NewGame(width: 80, height: 50);      // stale now belongs to no game

			Assert.False(Game.Instance.TryGetPlayerNumber(stale, out _));
			Assert.True(stale != 0);
			Assert.False(stale == 0);
		}

		// The consequence that actually bit: ownership filtering. A detached Player used to
		// match every barbarian city because both read as owner 0.
		[Fact]
		public void APlayerFromAnotherGame_OwnsNoCities()
		{
			Sim.NewGame(width: 80, height: 50);
			Player stale = AnAICiv();

			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player barbarians = g.GetPlayer(0);
			g.AddCity(barbarians, 0, 30, 20);

			Assert.NotEmpty(barbarians.Cities);
			Assert.Empty(stale.Cities);
		}

		// The Barbarians must never be reported destroyed, however few cities they hold —
		// IsDestroyed disbands every unit of a civ it considers gone.
		[Fact]
		public void TheBarbarians_AreNeverDestroyed()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player barbarians = g.GetPlayer(0);
			IUnit raider = g.CreateUnit(UnitType.Legion, 30, 20, 0)!;

			Assert.Empty(barbarians.Cities);
			Assert.False(barbarians.IsDestroyed());
			Assert.Contains(g.GetUnits(), u => u == raider);   // and its units survive
		}

		// The test dropped two days ago, now that the premise underneath it holds: a real
		// civ with no cities and no free Settlers is gone.
		[Fact]
		public void ACivWithNoCitiesAndNoSettlers_CountsAsDestroyed()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player civ = AnAICiv();
			foreach (City c in civ.Cities.ToArray()) g.DestroyCity(c);
			foreach (IUnit u in g.GetUnits().Where(u => civ == u.Owner).ToArray()) g.DisbandUnit(u);

			Assert.Empty(civ.Cities);
			Assert.True(civ.IsDestroyed());
		}
	}
}
