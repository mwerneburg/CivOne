// CivOne tests
//
// A city must not start a wonder it cannot finish in a lifetime.
//
// SelectWonder gates on IsTopProductionCity, which is RELATIVE to the civilization's own
// cities — so a one-city civ's only city is always "top" however feeble. Measured in run
// f991d45c: Tenochtitlan, the Aztec capital and their only city for all 631 turns, made 2
// shields a turn and was handed Adam Smith's Trading House (600 shields) at turn 357. By the
// end of the game it held 288 of them — 274 turns for under half — while its civilization
// finished on ONE city with 30,000 gold it could not spend, because the rush-buy clinch for a
// wonder requires 70% completion and they stood at 48%.
//
// Not a freeze and not a hard lock: the city was building the whole time, just at a pace that
// consumed the entire game. That is the shape this guard exists to refuse.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Wonders;
using CivOne.Advances;

namespace CivOne.Tests
{
	public class WonderHorizonTests
	{
		// One city, so it is trivially its civ's "top production city" — the exact hole the
		// relative test leaves open. Terrain is what sets its shield income.
		private static (Game game, Player owner, City city) ALoneCity(Terrain terrain, int size)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, terrain);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0);
			p.Government = new CivOne.Governments.Monarchy();
			p.Explore(40, 25, range: 12);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = (byte)size;
			foreach (IAdvance a in Common.Advances) p.AddAdvance(a, false);   // nothing gated on tech
			c.InvalidateCache();
			Sim.ClearTasks();
			return (g, p, c);
		}

		// The rule, stated directly: nothing in the plan may be a wonder this city could not
		// finish inside the horizon. Asserting "no wonder at all" was wrong — the guard
		// correctly admits CHEAPER wonders a feeble city can still manage, and the Great
		// Library slipping through a blanket assertion is the guard working, not failing.
		[Fact]
		public void NoPlannedWonderIsBeyondReach()
		{
			(Game g, Player p, City c) = ALoneCity(Terrain.Grassland1, size: 2);
			Assert.True(c.ShieldIncome > 0, "fixture stalled entirely; that is a different bug");

			foreach (IProduction x in PlanFor(p, c).Where(x => x is IWonder))
			{
				int turns = c.ProductionCost(x) / c.ShieldIncome;
				Assert.True(turns <= 100,
					$"{x.GetType().Name} would take {turns} turns at {c.ShieldIncome} shields");
			}
		}

		// The Aztec case itself: Adam Smith's at 600 shields is beyond a city this size, and
		// must not be planned however "top" it is among its civ's one city.
		[Fact]
		public void TheAztecWonderIsRefused()
		{
			(Game g, Player p, City c) = ALoneCity(Terrain.Grassland1, size: 2);
			int cost = c.ProductionCost(new AdamSmithsTradingHouse());

			Assert.True(cost / System.Math.Max(1, c.ShieldIncome) > 100,
				$"fixture is not feeble enough: {cost} shields at {c.ShieldIncome}/turn");
			Assert.DoesNotContain(PlanFor(p, c), x => x is AdamSmithsTradingHouse);
		}

		// ...and a city that can actually build one still gets the chance. A guard that
		// refused every wonder would be a regression dressed as a fix.
		[Fact]
		public void AStrongCityIsStillOfferedAWonder()
		{
			(Game g, Player p, City c) = ALoneCity(Terrain.Hills, size: 12);

			Assert.True(c.ShieldIncome > 0, "fixture makes nothing at all");
			Assert.Contains(PlanFor(p, c), x => x is IWonder);
		}

		// The horizon is generous on purpose — a wonder SHOULD be a long project. It is the
		// life sentence that is refused.
		[Fact]
		public void TheHorizonIsGenerous()
		{
			var f = typeof(AI).GetField("WonderHorizonTurns",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			Assert.NotNull(f);
			int horizon = (int)f!.GetRawConstantValue()!;

			Assert.InRange(horizon, 50, 200);
		}

		private static IProduction[] PlanFor(Player p, City c)
		{
			AI ai = AI.Instance(p);
			var m = typeof(AI).GetMethod("PlanProduction",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			var stance = typeof(AI).GetMethod("GetStance",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(ai, null);
			var list = (System.Collections.Generic.IEnumerable<IProduction>)m.Invoke(ai, new[] { c, stance })!;
			return list.ToArray();
		}
	}
}
