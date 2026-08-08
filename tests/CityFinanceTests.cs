// CivOne tests
//
// Three gaps around money and city death, all found in one 26-city play game at turn 244.
//
// 1. INSOLVENCY IS SILENT UNTIL IT TAKES SOMETHING. City.cs:2285 sells the
//    highest-maintenance improvement in any city whose bill the treasury cannot meet.
//    That rule is right — Civ 1's — but the only place the empire's income and upkeep
//    appear is the LAST page of the Trade Report, so a slow drain reads to the player as
//    a Sewer System vanishing for no reason. (Sewer maintenance is 4, joint-highest of
//    anything a mid-game empire owns, so it is always first out of the door.)
//
// 2. THE CITY SCREEN SAID NOTHING ABOUT GOLD. It carried shield upkeep and no way to
//    tell which cities were paying for themselves.
//
// 3. CITIES VANISHED WITH NO MESSAGE AT ALL. Setting City.Size to 0 calls
//    Game.DestroyCity (City.cs:81), and DestroyCity announces nothing — every caller
//    that wants a notice enqueues its own. Two paths cross zero without one:
//    starvation, and a size-1 city completing a Settlers. The AI is held back from the
//    second (City.cs:1752); the human is deliberately not, because relocating a town's
//    last citizens is a legitimate move — but the production was queued turns earlier at
//    a healthy size, so in practice towns just disappeared. Confirmed in the replay log
//    of that save: Kumbi Saleh (t230), Takrur (t240) and Mopti (t242) all destroyed with
//    no matching CityCaptured event, while every surviving city's queue was salted with
//    Settlers entries.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CityFinanceTests
	{
		// Grassland so a city can actually feed itself; Monarchy because Despotism caps
		// every tile at 2 food and no city can run any surplus at all under it.
		// Difficulty 2 (Prince), not the Sim default of 0. On CHIEFTAIN, City.cs:1745
		// refuses to complete a Settlers in a size-1 city at all — a deliberate guard for
		// beginners — so the abandonment scenario below cannot occur there and the test
		// would assert nothing. The play game that lost Takrur and Mopti was on Prince.
		private static (Game game, Player human, City city) ATown(int size)
		{
			Sim.NewGame(width: 80, height: 50, difficulty: 2);
			Settings.Instance.Autopilot = false;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Irrigation = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Government = new Monarchy();
			human.Explore(40, 25, range: 20);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.Size = (byte)size;
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, human, c);
		}

		// ─── 2. the city screen's gold line ──────────────────────────────────────

		// The field's whole job: taxes in, maintenance out. A Temple costs 1/turn, so it
		// must move the number by exactly 1.
		[Fact]
		public void NetGoldSubtractsMaintenanceFromTaxes()
		{
			var (_, _, city) = ATown(4);
			short before = city.NetGold;

			city.AddBuilding(new Temple());

			Assert.Equal(1, (int)city.TotalMaintenance);
			Assert.Equal(before - 1, city.NetGold);
		}

		// It has to agree with what NewTurn actually does to the treasury, or it is a
		// decoration. NewTurn adds Taxes and subtracts TotalMaintenance (City.cs:2271,
		// :2294) — and pays NO taxes while in disorder, which is exactly when a player
		// most wants to know what a city is costing.
		[Fact]
		public void NetGoldMatchesWhatTheTurnDoesToTheTreasury()
		{
			var (_, _, city) = ATown(4);
			city.AddBuilding(new Temple());
			city.AddBuilding(new Aqueduct());   // 2 more upkeep

			int expected = (city.IsInDisorder ? 0 : city.Taxes) - city.TotalMaintenance;

			Assert.Equal(expected, city.NetGold);
			Assert.Equal(3, (int)city.TotalMaintenance);
		}

		// A city with buildings and no trade is a net loss, and must read as one. Before
		// this field there was nowhere on the screen that said so.
		[Fact]
		public void ACityThatCostsMoreThanItEarnsReadsNegative()
		{
			var (_, _, city) = ATown(4);
			city.AddBuilding(new Cathedral());   // 3/turn, the expensive kind

			Assert.True(city.NetGold < 0, $"net gold was {city.NetGold}");
		}

		// ─── 1. the treasury warning ─────────────────────────────────────────────

		// The defect, stated directly: the player is told BEFORE a building is sold.
		// Three turns of runway at 4 upkeep is 12 gold, so 5 is comfortably inside it —
		// and comfortably above the bill, so nothing has been sold yet.
		[Fact]
		public void ALowTreasuryIsAnnouncedBeforeAnythingIsSold()
		{
			var (_, human, city) = ATown(4);
			city.AddBuilding(new Cathedral());   // 3
			city.AddBuilding(new Temple());      // 1  -> 4/turn
			human.Gold = 5;
			Sim.ClearTasks();

			human.NewTurn();

			Assert.Contains(Sim.PendingMessageLines(), l => l.Contains("Treasury running low"));
			Assert.Contains(city.Buildings, b => b is Cathedral);   // nothing sold yet
		}

		// The control. A healthy empire must never see it, or it is noise and the player
		// learns to dismiss the one that matters.
		[Fact]
		public void ASolventTreasuryIsNotWarnedAbout()
		{
			var (_, human, city) = ATown(4);
			city.AddBuilding(new Cathedral());
			human.Gold = 500;
			Sim.ClearTasks();

			human.NewTurn();

			Assert.DoesNotContain(Sim.PendingMessageLines(), l => l.Contains("Treasury"));
		}

		// Latched: it fires on the crossing, not every turn. A civ that lives poor for
		// fifty turns would otherwise open the same newspaper fifty times.
		[Fact]
		public void TheWarningDoesNotRepeatWhileTheTreasuryStaysLow()
		{
			var (_, human, city) = ATown(4);
			city.AddBuilding(new Cathedral());
			human.Gold = 2;

			Sim.ClearTasks();
			human.NewTurn();
			int first = Sim.PendingMessageLines().Count(l => l.Contains("Treasury running low"));

			Sim.ClearTasks();
			human.NewTurn();
			int second = Sim.PendingMessageLines().Count(l => l.Contains("Treasury running low"));

			Assert.Equal(1, first);
			Assert.Equal(0, second);
		}

		// ...and re-arms once it has been paid off, so the NEXT slide is announced too.
		[Fact]
		public void TheWarningReArmsAfterRecovery()
		{
			var (_, human, city) = ATown(4);
			city.AddBuilding(new Cathedral());

			human.Gold = 2;
			Sim.ClearTasks();
			human.NewTurn();

			human.Gold = 500;
			Sim.ClearTasks();
			human.NewTurn();          // solvent: clears the latch

			human.Gold = 2;
			Sim.ClearTasks();
			human.NewTurn();

			Assert.Contains(Sim.PendingMessageLines(), l => l.Contains("Treasury running low"));
		}

		// ─── 3. cities that vanish ───────────────────────────────────────────────

		// The defect that cost Takrur and Mopti: a size-1 city finishing a Settlers is
		// destroyed, and until now said nothing whatsoever.
		//
		// The city is NOT the player's only one — Game.AddCity a second town first —
		// because City.cs:1783 bumps a lone city's size to rescue it, and the whole
		// scenario would evaporate.
		[Fact]
		public void ATownAbandonedToASettlerSaysSo()
		{
			var (g, human, city) = ATown(1);
			g.AddCity(human, 1, 46, 25);
			string doomed = city.Name;
			city.SetProduction(new Settlers());
			city.Shields = city.ProductionCost(city.CurrentProduction);
			Sim.ClearTasks();

			city.NewTurn();

			Assert.Equal(0, (int)city.Size);
			Assert.Contains(Sim.PendingMessageLines(), l => l.Contains($"{doomed} abandoned"));
		}

		// The counterpart: a town that merely SHRINKS is not announced as lost. Without
		// this the notice could fire on every settler ever built.
		[Fact]
		public void ATownThatMerelyShrinksIsNotAnnouncedAsAbandoned()
		{
			var (g, human, city) = ATown(4);
			g.AddCity(human, 1, 46, 25);
			city.SetProduction(new Settlers());
			city.Shields = city.ProductionCost(city.CurrentProduction);
			Sim.ClearTasks();

			city.NewTurn();

			Assert.Equal(3, (int)city.Size);
			Assert.DoesNotContain(Sim.PendingMessageLines(), l => l.Contains("abandoned"));
		}

		// The other silent crossing. Famine art fired either way, so a city that starved
		// out of existence looked exactly like one that lost a citizen.
		[Fact]
		public void ATownThatStarvesOutOfExistenceSaysSo()
		{
			var (g, human, city) = ATown(1);
			g.AddCity(human, 1, 46, 25);
			string doomed = city.Name;
			// Starve it: no food anywhere in the city radius, and the stored box empty.
			// Mountains rather than Arctic — an Arctic tile with a Special is SEALS, worth
			// 2 food, and the generated map has enough of them to keep the town alive. The
			// first draft of this read "food income is 0" for exactly that reason.
			for (int y = 23; y <= 27; y++)
			for (int x = 38; x <= 42; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Mountains);
				Map.Instance[x, y].Irrigation = false;
			}
			city.Size = 1;              // re-runs the setter: invalidates the yield caches
			city.ResetResourceTiles();
			city.Food = 0;
			Sim.ClearTasks();

			Assert.True(city.FoodIncome < 0, $"scenario: food income is {city.FoodIncome}");
			city.NewTurn();

			Assert.Equal(0, (int)city.Size);
			Assert.Contains(Sim.PendingMessageLines(), l => l.Contains($"{doomed} is gone"));
		}
	}
}
