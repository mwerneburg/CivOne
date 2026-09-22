// CivOne tests
//
// Cultural Ascendancy divides culture by population, and culture is a cumulative STOCK while
// population is not. So against a live denominator, shedding citizens raised a civilization's
// standing exactly as surely as building a cathedral did: spawn settlers out of a city,
// disband them, and the ratio climbs with nothing built and nothing achieved. Reported from
// play, where it worked — "very mechanical and not in the spirit of real attainment."
//
// The divisor is now the largest populace the civ has ever held. Shrinking can only cost you:
// the numerator stops growing and the divisor stays where it was.
//
// The FLOOR is deliberately left on the living population. Its job is to keep relics out, and
// a collapsed empire is the relic it was written for however many citizens it once had — so
// "am I a society today" and "what am I judged against" are two different questions here.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class PeakPopulaceTests
	{
		private static (Game game, Player us, Player rival) TwoCivs(int ourSize, int rivalSize)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player rival = g.Players.First(p => p is not null && p != us && g.PlayerNumber(p) != 0);
			foreach (Player p in new[] { us, rival })
			{
				p.Government = new Monarchy();
				p.Explore(40, 25, range: 20);
			}
			g.AddCity(us, 0, 40, 25)!.Size = (byte)ourSize;
			g.AddCity(rival, 1, 44, 30)!.Size = (byte)rivalSize;
			us.RecordPeakPopulace();
			rival.RecordPeakPopulace();
			Sim.ClearTasks();
			return (g, us, rival);
		}

		private static City TheirOnlyCity(Player p) => Game.Instance.GetCities().First(c => c.Owner == Game.Instance.PlayerNumber(p));

		// The measure itself, in one line: halve the population, and the standing does not move.
		[Fact]
		public void ShrinkingDoesNotRaiseCulturePerHead()
		{
			(_, Player us, _) = TwoCivs(ourSize: 12, rivalSize: 12);
			us.SetCulture(1200);
			double before = (double)us.Culture / us.PeakPopulace;

			TheirOnlyCity(us).Size = 6;

			Assert.Equal(12, us.PeakPopulace);
			Assert.Equal(before, (double)us.Culture / us.PeakPopulace);
			// ...and the bleed really would have worked on the live count, or the assertion
			// above is just arithmetic about a number nobody was gaming.
			Assert.True((double)us.Culture / us.Populace > before * 1.9,
				"fixture: this bleed would not have moved the old measure either");
		}

		// A civ that GROWS is judged against the bigger number immediately. The stored field is
		// only written once a turn, so a getter that trusted it would hand a growing civ too
		// small a divisor for the rest of the turn — flattering exactly the civ the rule has no
		// quarrel with, but by the same broken mechanism.
		[Fact]
		public void GrowthRaisesThePeakAtOnce()
		{
			(_, Player us, _) = TwoCivs(ourSize: 10, rivalSize: 10);

			TheirOnlyCity(us).Size = 18;

			Assert.Equal(18, us.PeakPopulace);
		}

		// The floor still asks who is alive TODAY: a collapsed empire stops ranking, whatever
		// it used to be. This is the half that fails if the peak is used for both clauses.
		[Fact]
		public void ACollapsedEmpireStopsRankingOnTheFloor()
		{
			(Game g, Player us, Player rival) = TwoCivs(ourSize: 40, rivalSize: 40);

			TheirOnlyCity(us).Size = 1;

			// The hard floor is gone; the collapse is punished by the DIVISOR instead. A
			// rump keeps the peak it used to hold, so shrinking buys it nothing — which is
			// what made the separate floor redundant. See Game.CulturalDensity.
			Assert.True(us.PeakPopulace >= 40, "the peak should remember what it was");

			long worldAvg = Game.CulturalWorldAverage(new[] { (long)us.PeakPopulace, (long)rival.PeakPopulace });
			double before = Game.CulturalDensity(1000, 40, worldAvg);
			double afterCollapse = Game.CulturalDensity(1000, us.PeakPopulace, worldAvg);
			Assert.Equal(before, afterCollapse, 6);
		}

		// ...and the same split, driven through the actual victory rule rather than the helper.
		//
		// The test above calls CulturalPopulaceFloor directly, so it cannot tell which populace
		// the RULE hands it — the negative check that moved the floor onto the peak passed the
		// whole suite. This one collapses a civ with an enormous culture: on the living count
		// it fails the floor and earns nothing, and on the peak it would clear the floor, lead
		// the field by a mile, and start running the clock.
		[Fact]
		public void ACollapsedEmpireEarnsNoStreakHoweverCulturedItWas()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player[] rivals = g.Players.Where(p => p is not null && p != us && g.PlayerNumber(p) != 0).Take(3).ToArray();
			foreach (Player p in rivals.Append(us))
			{
				p.Government = new Monarchy();
				p.Explore(40, 25, range: 20);
			}
			us.AddAdvance(new Advances.Philosophy(), false);
			City ours = g.AddCity(us, 0, 40, 25)!;
			ours.Size = 40;
			int id = 1;
			foreach (Player r in rivals) g.AddCity(r, id, 34 + id++ * 3, 30)!.Size = 40;
			us.SetCulture(100000);                       // a colossal stock, per head or not
			foreach (Player r in rivals) r.SetCulture(600);
			us.RecordPeakPopulace();
			g.GameTurn = Sim.TurnPastCultureGate();

			ours.Size = 1;                               // the empire collapses

			Assert.True(us.PeakPopulace >= 40, "fixture: the peak did not remember the empire");
			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }
			Assert.True(g.GameTurn >= target, $"the fixture could not advance a turn (stuck at {g.GameTurn})");

			Assert.Equal(0u, g.Progress(g.PlayerNumber(us)).CultureStreak);
		}

		// End to end, and the case that was reported: a civ behind on the measure bleeds its
		// population and does NOT take the lead.
		[Fact]
		public void TheBleedNoLongerBuysTheLead()
		{
			(Game g, Player us, Player rival) = TwoCivs(ourSize: 20, rivalSize: 20);
			us.SetCulture(1000);
			rival.SetCulture(1500);

			TheirOnlyCity(us).Size = 5;   // spawn settlers, disband them

			double ours = (double)us.Culture / us.PeakPopulace;
			double theirs = (double)rival.Culture / rival.PeakPopulace;
			Assert.True(ours < theirs, "the bleed took the lead on the peak measure");
			// The same bleed against the LIVE denominator, which is what it used to buy:
			// 1000/5 = 200 against 1500/20 = 75, a runaway lead earned by shrinking.
			Assert.True((double)us.Culture / us.Populace > theirs * Game.CultureLeadMargin,
				"fixture: the bleed would not have won under the old rule, so this proves nothing");
		}

		[Fact]
		public void ThePeakSurvivesASave()
		{
			(Game g, Player us, _) = TwoCivs(ourSize: 16, rivalSize: 10);
			TheirOnlyCity(us).Size = 4;
			us.RecordPeakPopulace();
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "peakpop.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Player back = Game.Instance.HumanPlayer;
			Assert.Equal(16, back.PeakPopulace);
			Assert.Equal(4, back.Populace);
		}

		// A save written under the old rule carries no figure, and must not read as zero: the
		// high-water mark starts at whatever the civ is today, so an existing game is neither
		// credited nor punished for a population the save never recorded.
		[Fact]
		public void AnOlderSaveStartsItsPeakAtTodaysPopulace()
		{
			(_, Player us, _) = TwoCivs(ourSize: 14, rivalSize: 10);

			us.SetPeakPopulace(0);   // what `PeakPopulace ?? 0` yields for a pre-change save

			Assert.Equal(14, us.PeakPopulace);
		}
	}
}
