// CivOne tests
//
// A nuclear strike had no diplomatic consequence at all. ApplyNuclearStrike killed units, laid
// fallout, halved the city, sterilised grey goo and woke Gozira — and nobody who was not in the
// blast radius noticed. That was not an oversight in the nuclear code so much as a hole in the
// diplomacy model: Player._attitudeBonus is a goodwill TIMER with no opposite sign, so a civ
// could buy friendship and never earn a grudge. And the one ledger that records atrocities
// declines to record the player's:
//
//     if (aggressor is null || aggressor == HumanPlayer) return false;   // Game.RecordProvocation
//
// The rule: nukes is nukes. Every civ that keeps embassies cuts the detonator off — trade
// severed, treaties torn up, pacts ended, goodwill spent — for twenty turns, doubled to forty
// where a United Nations exists to condemn them in. Already being at war is neither an excuse
// nor an exemption. Striking the organism, the Registry, the machines or the barbarians is pest
// control and costs nothing.

using System.Linq;
using CivOne;
using CivOne.Civilizations;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class NuclearCondemnationTests
	{
		private static (Game, Player bomber, Player victim, Player witness) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			return (g, ps[0], ps[1], ps[2]);
		}

		private static City ACity(Player owner, int x, int y = 25)
		{
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(x, y, range: 3);
			City c = g.AddCity(owner, x + y, x, y)!;
			c.Size = 8;
			return c;
		}

		// The defect, stated directly: the world notices.
		[Fact]
		public void UsingANukeOnPeopleMakesYouAPariah()
		{
			(Game g, Player bomber, Player victim, Player witness) = AWorld();
			Assert.False(g.IsNuclearPariah(bomber));

			g.CondemnNuclearStrike(bomber, victim);

			Assert.True(g.IsNuclearPariah(bomber));
		}

		[Fact]
		public void GoodwillTreatiesAndAlliancesAllEnd()
		{
			(Game g, Player bomber, Player victim, Player witness) = AWorld();
			witness.SetAttitudeBonus(bomber, 50);
			witness.SetDefensePact(bomber, 50);
			witness.SetPeaceTreaty(bomber, 50);
			bomber.SetPeaceTreaty(witness, 50);

			g.CondemnNuclearStrike(bomber, victim);

			Assert.False(witness.HasAttitudeBonus(bomber), "goodwill does not survive a mushroom cloud");
			Assert.False(witness.HasDefensePact(bomber), "the alliance is over");
			Assert.False(witness.HasPeaceTreaty(bomber), "ties are broken");
			Assert.False(bomber.HasPeaceTreaty(witness), "...both ways");
		}

		[Fact]
		public void TradeRoutesAreSevered()
		{
			(Game g, Player bomber, Player victim, Player witness) = AWorld();
			City mine = ACity(bomber, 30);
			City theirs = ACity(witness, 40);
			mine.AddTradeRoute(theirs, "silk");
			theirs.AddTradeRoute(mine, "silk");
			Assert.True(mine.TradeRouteCount > 0 && theirs.TradeRouteCount > 0);

			g.CondemnNuclearStrike(bomber, victim);

			Assert.Equal(0, mine.TradeRouteCount);
			Assert.Equal(0, theirs.TradeRouteCount);
		}

		// Being at war already is neither an excuse nor an exemption.
		[Fact]
		public void AlreadyBeingAtWarIsNoExcuse()
		{
			(Game g, Player bomber, Player victim, Player witness) = AWorld();
			bomber.DeclareWar(victim);

			g.CondemnNuclearStrike(bomber, victim);

			Assert.True(g.IsNuclearPariah(bomber));
		}

		// ── who counts as people ────────────────────────────────────────────

		[Fact]
		public void StrikingTheStoryFactionsCostsNothing()
		{
			foreach (System.Type civType in new[]
			         { typeof(TheThing), typeof(TheOthers), typeof(Skynet) })
			{
				(Game g, Player bomber, Player victim, Player witness) = AWorld();
				Player faction = new Player(Common.Civilizations.First(c => c.GetType() == civType));
				g.AddPlayer(faction);

				g.CondemnNuclearStrike(bomber, faction);

				Assert.False(g.IsNuclearPariah(bomber),
					$"striking {faction.Civilization.Name} is pest control");
			}
		}

		// The Olvir are refugees, not vermin. They count.
		[Fact]
		public void TheOlvirCount()
		{
			(Game g, Player bomber, Player victim, Player witness) = AWorld();
			Player olvir = g.Players.FirstOrDefault(p => p is not null && p.Civilization is Olvir)!;
			if (olvir is null)
			{
				olvir = new Player(Common.Civilizations.First(c => c is Olvir));
				g.AddPlayer(olvir);
			}

			g.CondemnNuclearStrike(bomber, olvir);

			Assert.True(g.IsNuclearPariah(bomber), "the refugees are people");
		}

		// ── the United Nations ──────────────────────────────────────────────

		// A world with a forum to condemn you in condemns you for twice as long.
		[Fact]
		public void TheUnitedNationsDoublesTheSentence()
		{
			(Game g, Player bomber, Player victim, Player witness) = AWorld();
			g.CondemnNuclearStrike(bomber, victim);
			int withoutUN = g.NuclearPariah[g.PlayerNumber(bomber)];

			(g, bomber, victim, witness) = AWorld();
			ACity(witness, 50).AddWonder(new CivOne.Wonders.UnitedNations());
			g.CondemnNuclearStrike(bomber, victim);
			int withUN = g.NuclearPariah[g.PlayerNumber(bomber)];

			Assert.Equal(Game.PariahTurns, withoutUN);
			Assert.Equal(Game.PariahTurnsWithUN, withUN);
			Assert.True(withUN > withoutUN);
		}

		// ── it ends ─────────────────────────────────────────────────────────

		// Twenty turns, not for ever. The sentence has to run out or a single strike would
		// close the diplomatic game permanently.
		[Fact]
		public void TheSentenceRunsOut()
		{
			(Game g, Player bomber, Player victim, Player witness) = AWorld();
			g.CondemnNuclearStrike(bomber, victim);
			byte b = g.PlayerNumber(bomber);

			// Drive the counter the way Game.NewTurn does.
			for (int t = 0; t < Game.PariahTurns; t++)
				if (g.NuclearPariah.ContainsKey(b) && --g.NuclearPariah[b] <= 0)
					g.NuclearPariah.Remove(b);

			Assert.False(g.IsNuclearPariah(bomber));
		}
	}
}
