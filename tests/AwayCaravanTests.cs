// CivOne tests
//
// A Caravan on the road when its home city changes hands (the user, Sep 2026). Capture used
// to disband every unit the city supported, wherever it stood; a seizure by the Machines,
// the Registry or the Thing left it homeless, and a homeless Caravan can never open a route.
// Now each Caravan away from the city is rehomed to the nearest other city of its owner.

using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class AwayCaravanTests
	{
		// Two AI civs (a capture the human can SEE waits on a screen headless — see
		// UnarmedCaptureTests). The owner holds `lost` at 40,25 and `spare` at 30,25; its
		// Caravan, homed in `lost`, is out at 36,22.
		private static (Game g, Player owner, Player enemy, City lost, City spare, IUnit caravan) ACaravanOnTheRoad()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 25; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player owner = ps[0], enemy = ps[1];
			foreach (Player p in new[] { owner, enemy }) { p.Government = new Monarchy(); p.Explore(38, 25, range: 14); }
			City lost = g.AddCity(owner, 0, 40, 25)!;  lost.Size = 3;
			City spare = g.AddCity(owner, 1, 30, 25)!; spare.Size = 3;
			IUnit caravan = g.CreateUnit(UnitType.Caravan, 36, 22, g.PlayerNumber(owner))!;
			caravan.SetHome(lost);
			Sim.ClearTasks();
			return (g, owner, enemy, lost, spare, caravan);
		}

		private static bool Alive(IUnit u) => Game.Instance.GetUnits().Contains(u);

		[Fact]
		public void ACapturedCitysCaravanOnTheRoadGoesToTheNextCity()
		{
			(Game g, Player owner, Player enemy, City lost, City spare, IUnit caravan) = ACaravanOnTheRoad();
			IUnit legion = g.CreateUnit(UnitType.Legion, 41, 25, g.PlayerNumber(enemy))!;
			enemy.DeclareWar(owner);
			Sim.ClearTasks();

			legion.MoveTo(-1, 0);
			Sim.Settle();

			Assert.Equal(g.PlayerNumber(enemy), lost.Owner);   // fixture: it really was taken
			Assert.True(Alive(caravan), "the Caravan went down with the city");
			Assert.Same(spare, caravan.Home);
		}

		[Fact]
		public void TheMachinesDoNotStrandIt()
		{
			(Game g, Player owner, Player _, City lost, City spare, IUnit caravan) = ACaravanOnTheRoad();
			lost.AddBuilding(new CivOne.Buildings.NeuralLab());
			lost.RemoveBuilding<CivOne.Buildings.Palace>();   // the uprising spares a capital

			typeof(Game).GetMethod("ExecuteSkynetUprising", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(g, null);

			Assert.NotEqual(g.PlayerNumber(owner), lost.Owner);   // fixture: seized
			Assert.Same(spare, caravan.Home);
		}

		// The garrison is the city's; only a Caravan AWAY is rescued, and only to its own side.
		[Fact]
		public void ACaravanInTheCityIsNotMoved()
		{
			(Game _, Player _, Player _, City lost, City _, IUnit caravan) = ACaravanOnTheRoad();
			caravan.X = lost.X; caravan.Y = lost.Y;

			lost.RehomeAwayCaravans();

			Assert.Same(lost, caravan.Home);
		}

		// The nearest city to the one lost, not merely any.
		[Fact]
		public void ItGoesToTheNearestCity()
		{
			(Game g, Player owner, Player _, City lost, City spare, IUnit caravan) = ACaravanOnTheRoad();
			City far = g.AddCity(owner, 2, 48, 30)!;
			far.Size = 3;
			Assert.True(Common.DistanceToTile(lost.X, lost.Y, far.X, far.Y) < Common.DistanceToTile(lost.X, lost.Y, spare.X, spare.Y),
				"fixture: the 'far' city is meant to be the nearer one");

			lost.RehomeAwayCaravans();

			Assert.Same(far, caravan.Home);
		}
	}
}
