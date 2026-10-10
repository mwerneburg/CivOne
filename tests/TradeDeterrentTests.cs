// CivOne tests
//
// Trade deters a war; it must not veto one. Late in game d721724c every pair of civs traded
// enough to hit the deterrent's cap of 40, and 40 was more than every other factor together:
// Sitting Bull, aggressive, six times the Aztecs' army and holding a grudge, revolted to
// Communism to attack them and rolled 6 + 8 + 10 + 15 - 40 = -1% a turn for 25 turns. No AI
// started a war in the last 320 turns of that game.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class TradeDeterrentTests
	{
		[Fact]
		public void AnAggressorWithEveryReasonStillGoesToWarOnARichPartner()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 10; y <= 40; y++)
			for (int x = 10; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ais = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player us = ais.First(p => p.Civilization.Leader.Aggression == AggressionLevel.Aggressive);
			Player them = ais.First(p => p != us);
			us.Government = new Monarchy();
			them.Government = new Monarchy();

			// Built out (no expansion penalty), neighbours, and a rich web of routes between them.
			int id = 0;
			City[] ours = Enumerable.Range(0, 12).Select(i => g.AddCity(us, id++, 14 + (i % 6) * 4, 14 + (i / 6) * 4)!).ToArray();
			City[] theirs = Enumerable.Range(0, 3).Select(i => g.AddCity(them, id++, 18 + i * 6, 30)!).ToArray();
			foreach (City c in ours.Concat(theirs)) { c.Size = 10; c.ResetResourceTiles(); }
			foreach (City c in ours)
				foreach (City t in theirs) { c.AddTradeRoute(t, "Silk"); t.AddTradeRoute(c, "Silk"); }
			for (int i = 0; i < 4; i++) g.CreateUnit(UnitType.Armor, 14, 14, g.PlayerNumber(us));
			us.AddGrudge(them, 100);
			Sim.ClearTasks();

			byte un = g.PlayerNumber(us), tn = g.PlayerNumber(them);
			int trade = ours.SelectMany(c => c.TradeRoutes).Where(r => r.Partner.Owner == tn).Sum(r => r.Value)
			          + theirs.SelectMany(c => c.TradeRoutes).Where(r => r.Partner.Owner == un).Sum(r => r.Value);
			Assert.True(trade / 2 >= 40, $"fixture: routes worth only {trade}, below the cap");
			Assert.False(us.IsAtWar(them));

			AI ai = AI.Instance(us)!;
			for (int turn = 0; turn < 60 && !us.IsAtWar(them); turn++)
				ai.ConsiderWar();

			Assert.True(us.IsAtWar(them), "maxed trade vetoed a war every other factor favoured");
		}
	}
}
