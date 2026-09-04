// CivOne tests
//
// A payment into a nearly-full treasury emptied it.
//
// Player.Gold was a short. `Gold += x` compiles to `Gold = (short)(Gold + x)`: the sum is
// computed in int and TRUNCATED before the setter is called, so any credit that took the
// total past short.MaxValue wrapped negative — and the setter's `if (value < 0) value = 0`,
// written as a floor for insolvency, turned the wrap into a total loss.
//
// Reported from a game at 1896 AD: a hundred and thirty cities, a treasury reading 0, and a
// caravan that announced "Revenue: 10000" without crediting anything. It credited: 25,000 +
// 10,000 = 35,000, truncated to -30,536, floored to nought.
//
// It is not a corner case for a large empire. City.NewTurn adds each city's taxes to the
// treasury ONE CITY AT A TIME, so the running total crosses short.MaxValue most turns on a
// big map — which is why the empire had no money at all rather than merely less than it
// earned. Gold is an int now; the save format already stored one.

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class TreasuryOverflowTests
	{
		// These cases reproduce a truncation at short.MaxValue, so they need a ceiling above
		// the value they build to. If the cap is ever lowered under them, that is a decision
		// about the game and not a bug in the treasury — rescale the case, do not delete it.
		private const string HeadroomNote =
			"this case needs a GoldCap above the sum it builds to; rescale it rather than dropping it";

		private static Player Rich(int gold)
		{
			Sim.NewGame(width: 40, height: 30, competition: 3);
			Player p = Game.Instance.HumanPlayer;
			p.Gold = gold;
			Sim.ClearTasks();
			return p;
		}

		// The reported case, in the arithmetic that produced it.
		//
		// Asserts the SUM, not the cap. It read Assert.Equal(GoldCap, ...) while the cap was
		// 30,000 — below short.MaxValue — so the clamp caught this credit before the addition
		// could be checked at all, and the test could not tell a treasury that had done the
		// arithmetic correctly from one that had merely been clamped. With the ceiling at
		// 100,000 there is room to state the real number, which is what the player was
		// actually owed.
		[Fact]
		public void ALargePaymentIntoAFullTreasuryDoesNotEmptyIt()
		{
			Assert.True(Player.GoldCap >= 35000, HeadroomNote);
			Player p = Rich(25000);

			p.Gold += 10000;

			Assert.Equal(35000, p.Gold);
		}

		// The boundary, both sides of it. From 30,000: 2,767 was fine and 2,768 was ruin,
		// which is the signature of a truncation rather than of any rule. Both must now land
		// on their exact total — 32,767 and 32,768 — because the second one is the first
		// value a short cannot hold, and that is the whole defect in one number.
		[Theory]
		[InlineData(2767, 32767)]
		[InlineData(2768, 32768)]
		public void EveryCreditCrossingTheShortBoundarySurvivesIntact(int credit, int expected)
		{
			Assert.True(Player.GoldCap >= expected, HeadroomNote);
			Player p = Rich(30000);

			p.Gold += credit;

			Assert.Equal(expected, p.Gold);
		}

		// Past the ceiling it clamps, and the ceiling is where it clamps to. Stated relative
		// to GoldCap so that moving the cap moves the test with it.
		[Theory]
		[InlineData(1)]
		[InlineData(50000)]
		public void ACreditPastTheCeilingClampsToIt(int overshoot)
		{
			Player p = Rich(Player.GoldCap - 1);

			p.Gold += overshoot;

			Assert.Equal(Player.GoldCap, p.Gold);
		}

		// The cap is a rule and still holds — this is not a licence to bank more. The value
		// itself moved (30,000 to 100,000), so this reads the constant rather than repeating
		// it: the ceiling being a rule is the thing worth pinning, not the number.
		//
		// The second assertion is the one that matters. A ceiling above short.MaxValue is
		// what makes the treasury's own stored value unrepresentable in the type that caused
		// this file to exist, so every path that touches gold is now obliged to be wide. Drop
		// the cap back under 32,767 and the defect becomes unreachable by accident again —
		// which is exactly how it hid for as long as it did.
		[Fact]
		public void TheCeilingHoldsAndClearsShortRange()
		{
			Player p = Rich(0);

			p.Gold = Player.GoldCap * 10;

			Assert.Equal(Player.GoldCap, p.Gold);
			Assert.True(Player.GoldCap > short.MaxValue,
				$"the ceiling is {Player.GoldCap}, inside short range — see the note above");
		}

		// The insolvency floor is unchanged too: a treasury cannot go negative. That behaviour
		// is load-bearing (City.cs sells a building when the bill cannot be met) and the fix
		// must not quietly turn it into a debt.
		[Fact]
		public void TheFloorStillHolds()
		{
			Player p = Rich(100);

			p.Gold -= 500;

			Assert.Equal(0, p.Gold);
		}

		// The caravan credit itself, pinned at the source. Widening Player.Gold does NOT save
		// this line on its own: `Gold += (short)revenue` truncates its own operand before the
		// addition, so an int treasury would still be paid a wrapped number. The first draft
		// of this test did the arithmetic itself and passed with the cast restored, which is
		// the negative check earning its keep.
		[Fact]
		public void TheCaravanCreditCarriesNoNarrowingCast()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(Sim.RepoRoot(),
				"src", "Units", "CaravanActions.cs"));
			int at = src.IndexOf(".Gold +=");
			Assert.True(at > 0, "the caravan credit has moved or been rewritten");
			string line = src.Substring(at, src.IndexOf('\n', at) - at);

			Assert.DoesNotContain("(short)", line);
		}

		// ...and the same for every other place gold is credited or spent: a narrowing cast on
		// gold arithmetic is the whole defect, and it reads as harmless at each site.
		[Theory]
		[InlineData("src/Units/CaravanActions.cs")]
		[InlineData("src/City.cs")]
		[InlineData("src/Game.cs")]
		[InlineData("src/Game.Cos.cs")]
		[InlineData("src/Screens/King.cs")]
		[InlineData("src/Screens/CityView.cs")]
		[InlineData("src/Screens/Dialogs/DiplomatBribe.cs")]
		[InlineData("src/Screens/Dialogs/DiplomatIncite.cs")]
		public void NoGoldArithmeticTruncates(string relative)
		{
			string[] lines = System.IO.File.ReadAllLines(System.IO.Path.Combine(
				Sim.RepoRoot(), relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));

			string[] offenders = lines
				.Where(l => l.Contains(".Gold +=") || l.Contains(".Gold -=") || l.Contains(".Gold ="))
				.Where(l => l.Contains("(short)"))
				.ToArray();

			Assert.Empty(offenders);
		}

		// Every civilization, not just the human. There is one Player.Gold and no separate AI
		// path — an AI caravan is credited by the same CaravanActions line — so the defect and
		// the fix both applied to the whole field. It bit the human first only because the
		// human was the only one rich enough: measured across the saves of this game, the AI
		// treasuries ran 79 to 6,281 gold while Charlemagne sat on 28,513, and the wrap needs
		// 22,768 before a 10,000 payment can reach it.
		[Fact]
		public void ARivalTreasuryClampsToo()
		{
			Sim.NewGame(width: 40, height: 30, competition: 4);
			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Gold = 25000;
			Sim.ClearTasks();

			ai.Gold += 10000;

			Assert.Equal(35000, ai.Gold);
		}

		// The mechanism that made it a permanent condition rather than a one-off: taxes are
		// added city by city, so a big empire crosses the old ceiling mid-loop every turn.
		// Simulated here rather than staged with 130 real cities.
		// The count is derived from the cap rather than fixed at 200, which stopped short of
		// the ceiling the moment the ceiling moved: enough cities to carry the total past it,
		// so the loop still crosses both the short boundary on the way and the cap at the end.
		[Fact]
		public void AccumulatingCityByCityNeverWrapsTheTreasury()
		{
			const int perCity = 400;
			int cities = Player.GoldCap / perCity + 50;
			Player p = Rich(0);

			for (int city = 0; city < cities; city++)
			{
				p.Gold += perCity;
				Assert.True(p.Gold >= 0, $"the treasury went to {p.Gold} after {city + 1} cities");
			}

			Assert.Equal(Player.GoldCap, p.Gold);
		}

		// Gold survives a save/load at a value a short could not hold on the way through. The
		// COS field was always an int; the load path cast it back down to a short.
		// Saves at the cap, whatever the cap is. It saved a literal 30,000, which was the cap
		// when it was written; once the ceiling rose, 30,000 was a value a short holds
		// comfortably, so the test round-tripped happily through the very cast it exists to
		// catch. It passed and proved nothing.
		[Fact]
		public void TheTreasurySurvivesASaveAtTheCap()
		{
			Assert.True(Player.GoldCap > short.MaxValue, HeadroomNote);
			Player p = Rich(Player.GoldCap);
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "treasury.cos");
			Game.Instance.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.Equal(Player.GoldCap, Game.Instance.HumanPlayer.Gold);
		}
	}
}
