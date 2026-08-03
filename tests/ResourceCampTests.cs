// CivOne tests
//
// Reported: a human settler ordered to build a coal camp never seems to finish — the
// "Build Coal Camp" order is still offered on the same tile afterwards. AI camps do
// appear. These walk the order from BuildCamp() to Game.ResourceCamps.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ResourceCampTests
	{
		// Game.ResourceAt reads Coal off any hills tile carrying a special.
		private static (Player owner, Settlers unit) ASettlerOnCoal(bool human)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			// Specials are position-derived, so lay hills down until one comes up special.
			for (int x = 25; x < 40; x++) Map.Instance.ChangeTileType(x, 20, Terrain.Hills);
			Map.Instance.RecalculateContinentsIfDirty();
			ITile coal = Enumerable.Range(25, 15).Select(x => Map.Instance[x, 20])
				.First(t => Game.ResourceAt(t) == StrategicResource.Coal);

			Player p = human
				? g.HumanPlayer
				: g.Players.First(x => x is not null && g.PlayerNumber(x) != 0 && x != g.HumanPlayer);
			p.Explore(coal.X, coal.Y, range: 4);
			var u = (Settlers)g.CreateUnit(UnitType.Settlers, coal.X, coal.Y, g.PlayerNumber(p))!;
			Sim.ClearTasks();
			return (p, u);
		}

		[Fact]
		public void TheTileUnderTheSettler_CarriesACoalDeposit()
		{
			var (_, unit) = ASettlerOnCoal(human: false);
			Assert.Equal(StrategicResource.Coal, Game.ResourceAt(Map.Instance[unit.X, unit.Y]));
		}

		// The order is accepted and takes three turns, as for the AI.
		[Fact]
		public void AnAIsCampCompletesAfterThreeTurns()
		{
			var (owner, unit) = ASettlerOnCoal(human: false);

			Assert.True(unit.BuildCamp());
			for (int i = 0; i < 3; i++) unit.NewTurn();

			Assert.True(Game.Instance.ResourceCamps.ContainsKey((unit.X, unit.Y)));
			Assert.True(Game.Instance.HasResource(owner, StrategicResource.Coal));
		}

		// The same order given by the human player. This is the reported failure.
		[Fact]
		public void AHumansCampCompletesAfterThreeTurns()
		{
			var (owner, unit) = ASettlerOnCoal(human: true);

			Assert.True(unit.BuildCamp());
			for (int i = 0; i < 3; i++) unit.NewTurn();

			Assert.True(Game.Instance.ResourceCamps.ContainsKey((unit.X, unit.Y)));
			Assert.True(Game.Instance.HasResource(owner, StrategicResource.Coal));
		}

		// Once the camp exists the order must stop being offered. This was the reported
		// symptom, and it does NOT reproduce — the gate in MenuItems is correct, so a
		// still-offered order means the camp never landed, not that the menu is stale.
		[Fact]
		public void OnceACampExists_TheOrderIsNoLongerOffered()
		{
			var (_, unit) = ASettlerOnCoal(human: true);
			unit.BuildCamp();
			for (int i = 0; i < 3; i++) unit.NewTurn();

			Assert.True(Game.Instance.ResourceCamps.ContainsKey((unit.X, unit.Y)), "camp did not land");
			Assert.DoesNotContain(unit.MenuItems, o => o is not null && o.Text is not null && o.Text.Contains("Camp"));
		}

		// The camp's tile yield is shipped to the nearest owned city. A Hills special
		// is 2 shields, and until this change a remote camp produced nothing at all.
		[Fact]
		public void ACampShipsItsTileShieldsToTheNearestOwnedCity()
		{
			var (owner, unit) = ASettlerOnCoal(human: false);
			Game g = Game.Instance;
			owner.Explore(unit.X + 6, 30, range: 4);
			City near = g.AddCity(owner, 0, unit.X + 6, 30)!;

			int before = near.ShieldIncome;
			unit.BuildCamp();
			for (int i = 0; i < 3; i++) unit.NewTurn();
			foreach (City c in g.GetCities()) c.InvalidateCache();

			Assert.Equal(before + Map.Instance[unit.X, unit.Y].Shield, near.ShieldIncome);
		}

		// "Nearest" means nearest: a second, closer city takes the delivery instead.
		[Fact]
		public void ACloserCityTakesTheDeliveryInstead()
		{
			var (owner, unit) = ASettlerOnCoal(human: false);
			Game g = Game.Instance;
			owner.Explore(unit.X + 10, 30, range: 4);
			owner.Explore(unit.X + 2, 22, range: 4);
			City far   = g.AddCity(owner, 0, unit.X + 10, 30)!;
			City close = g.AddCity(owner, 1, unit.X + 2, 22)!;

			int farBefore = far.ShieldIncome, closeBefore = close.ShieldIncome;
			unit.BuildCamp();
			for (int i = 0; i < 3; i++) unit.NewTurn();
			foreach (City c in g.GetCities()) c.InvalidateCache();

			Assert.Equal(farBefore, far.ShieldIncome);
			Assert.Equal(closeBefore + Map.Instance[unit.X, unit.Y].Shield, close.ShieldIncome);
		}

		// A rival's camp must not pay into our cities.
		[Fact]
		public void ARivalsCampPaysNothingToUs()
		{
			var (mine, unit) = ASettlerOnCoal(human: false);
			Game g = Game.Instance;
			Player theirs = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			                                  && p != g.HumanPlayer && p != mine);
			theirs.Explore(unit.X + 3, 22, range: 4);
			City ours = g.AddCity(theirs, 0, unit.X + 3, 22)!;

			int before = ours.ShieldIncome;
			unit.BuildCamp();                       // owned by `mine`, who has no cities
			for (int i = 0; i < 3; i++) unit.NewTurn();
			foreach (City c in g.GetCities()) c.InvalidateCache();

			Assert.Equal(before, ours.ShieldIncome);
		}
	}
}
