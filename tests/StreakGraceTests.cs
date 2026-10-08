// CivOne tests
//
// A streak victory lost on its last turn by a hair, with no warning. The user's Pax Mercatoria
// run (Oct 2026) held 50.80% of world output on turn 67 of 75 and fell to 49.71% by turn 72;
// one turn under the bar zeroed the lot. Now a dip of up to Game.StreakGraceTurns turns
// pauses a streak instead of breaking it, the same for Cultural Ascendancy, and the merchant
// is warned once when the lead thins to within a point of half.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class StreakGraceTests
	{
		// ── Cultural Ascendancy, on CultureBlockedByWarTests' admired world ─────────

		// One game turn: the victory checks run once per full round.
		private static void ATurn(Game g)
		{
			uint target = g.GameTurn + 1u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) g.EndTurn();
			Assert.True(g.GameTurn >= target, $"the fixture could not advance a turn (stuck at {g.GameTurn})");
		}

		private static uint CultureStreak(Game g, Player us) => g.Progress(g.PlayerNumber(us)).CultureStreak;

		// Admired for two turns, unadmired for `dip`, admired again.
		private static (uint before, uint after) ACultureDip(int dip)
		{
			(Game g, Player us, Player[] _) = CultureBlockedByWarTests.AWorldWhereWeAreAdmired();
			ATurn(g);
			ATurn(g);
			uint before = CultureStreak(g, us);

			us.SetCulture(1);
			for (int i = 0; i < dip; i++) ATurn(g);
			us.SetCulture(6000);
			ATurn(g);
			return (before, CultureStreak(g, us));
		}

		[Fact]
		public void AShortCultureDipPausesTheStreak()
		{
			(uint before, uint after) = ACultureDip((int)Game.StreakGraceTurns);
			Assert.True(before > 0, "fixture: no streak to dip from");
			Assert.Equal(before + 1, after);   // paused through the dip, then counting again
		}

		[Fact]
		public void ALongCultureDipBreaksIt()
		{
			(uint before, uint after) = ACultureDip((int)Game.StreakGraceTurns + 1);
			Assert.True(before > 1, "fixture: no streak to lose");
			Assert.Equal(1u, after);   // started over
		}

		// A war of our making is a decision, not a dip: no grace.
		[Fact]
		public void AWarOfOurMakingStillBreaksItAtOnce()
		{
			(Game g, Player us, Player[] rivals) = CultureBlockedByWarTests.AWorldWhereWeAreAdmired();
			ATurn(g);
			Assert.True(CultureStreak(g, us) > 0, "fixture: no streak");

			us.DeclareWar(rivals[0]);
			ATurn(g);

			Assert.Equal(0u, CultureStreak(g, us));
		}

		// ── Pax Mercatoria ────────────────────────────────────────────────────────

		// Three rivals on river land, each paying us tribute: the tribute both binds them and
		// counts in our output, so the share is set by `tribute` alone.
		private static (Game g, Player us, Player[] rivals) AMerchantWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player[] rivals = g.Players.Where(p => p is not null && p != us && g.PlayerNumber(p) != 0).Take(3).ToArray();
			us.AddAdvance(new Banking(), false);
			g.AddCity(us, 0, 40, 25)!.Size = 4;
			int id = 1;
			foreach (Player r in rivals)
			{
				City c = g.AddCity(r, id, 24 + id * 8, 30)!;
				id++;
				c.Size = 12;
				c.ResetResourceTiles();
				r.Gold = 30000;
			}
			Sim.ClearTasks();
			return (g, us, rivals);
		}

		private static void Tribute(Player us, Player[] rivals, int total)
		{
			for (int i = 0; i < rivals.Length; i++)
			{
				rivals[i].DissolveTribute(us);
				int share = total / rivals.Length + (i < total % rivals.Length ? 1 : 0);
				if (share > 0) rivals[i].EstablishTribute(us, share);
			}
		}

		private static uint EconStreak(Game g, Player us) => g.Progress(g.PlayerNumber(us)).EconStreak;

		private static (uint before, uint after) AnEconomicDip(int dip)
		{
			(Game g, Player us, Player[] rivals) = AMerchantWorld();
			Tribute(us, rivals, 3000);
			ATurn(g);
			ATurn(g);
			uint before = EconStreak(g, us);

			Tribute(us, rivals, 0);
			for (int i = 0; i < dip; i++) ATurn(g);
			Tribute(us, rivals, 3000);
			ATurn(g);
			return (before, EconStreak(g, us));
		}

		[Fact]
		public void AShortEconomicDipPausesTheStreak()
		{
			(uint before, uint after) = AnEconomicDip((int)Game.StreakGraceTurns);
			Assert.True(before > 0, "fixture: no streak to dip from");
			Assert.Equal(before + 1, after);
		}

		[Fact]
		public void ALongEconomicDipBreaksIt()
		{
			(uint before, uint after) = AnEconomicDip((int)Game.StreakGraceTurns + 1);
			Assert.True(before > 1, "fixture: no streak to lose");
			Assert.Equal(1u, after);
		}

		// Grace is per dip: two short dips with a recovery between them are two pauses, not
		// one long one.
		[Fact]
		public void EachDipGetsItsOwnGrace()
		{
			(Game g, Player us, Player[] rivals) = AMerchantWorld();
			Tribute(us, rivals, 3000);
			ATurn(g);
			for (int dip = 0; dip < 2; dip++)
			{
				Tribute(us, rivals, 0);
				ATurn(g); ATurn(g);
				Tribute(us, rivals, 3000);
				ATurn(g);
			}
			Assert.Equal(3u, EconStreak(g, us));
		}

		[Fact]
		public void AWarOfOurMakingBreaksTheMarketsAtOnce()
		{
			(Game g, Player us, Player[] rivals) = AMerchantWorld();
			Tribute(us, rivals, 3000);
			ATurn(g);
			Assert.True(EconStreak(g, us) > 0, "fixture: no streak");

			Player victim = g.Players.First(p => p is not null && p != us && g.PlayerNumber(p) != 0 && !rivals.Contains(p));
			us.DeclareWar(victim);
			ATurn(g);

			Assert.Equal(0u, EconStreak(g, us));
		}

				// The warning band: within a point of half, not at it or over it.
		[Theory]
		[InlineData(5050, 10000, true)]
		[InlineData(5099, 10000, true)]
		[InlineData(5100, 10000, false)]
		[InlineData(6000, 10000, false)]
		public void TheWarningBandIsTheLastPoint(int ours, int world, bool slipping) =>
			Assert.Equal(slipping, Game.MarketsSlipping(ours, world));

		// ...and the merchant is told (latched: EconSlipWarned). The share is tuned with the tribute to just over
		// half of whatever the rivals' cities produce.
		[Fact]
		public void TheMerchantIsWarnedNearTheBar()
		{
			(Game g, Player us, Player[] rivals) = AMerchantWorld();
			Tribute(us, rivals, 3000);
			ATurn(g);
			PlayerProgress pr = g.Progress(g.PlayerNumber(us));
			Assert.False(pr.EconSlipWarned, "warned while holding 3000 in tribute");

			// The rivals' cities produce too little to tune against, so one rival pays another:
			// tribute counts in the world's output. Then just over half: A = R + R/50 + 1.
			rivals[1].EstablishTribute(rivals[2], 1000);
			int R = rivals.Sum(r => g.GrossOutputOf(r));
			int ownCities = g.GrossOutputOf(us) - 3000;
			Tribute(us, rivals, R + R / 50 + 1 - ownCities);
			ATurn(g);

			int worldNow = g.GrossOutputOf(us) + rivals.Sum(r => g.GrossOutputOf(r));
			Assert.True(Game.MarketsSlipping(g.GrossOutputOf(us), worldNow) && g.GrossOutputOf(us) * 2 > worldNow,
				$"fixture: tuned share drifted to {g.GrossOutputOf(us)} of {worldNow}");
			Assert.True(pr.EconStreak > 1, "fixture: the streak broke");
			Assert.True(pr.EconSlipWarned, "not warned at the edge of the bar");
		}

		[Fact]
		public void TheGraceCountersSurviveASave()
		{
			(Game g, Player us, Player[] _) = AMerchantWorld();
			byte n = g.PlayerNumber(us);
			g.Progress(n).EconSlip = 2;
			g.Progress(n).CultureSlip = 3;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "grace.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));

			Assert.Equal(2u, Game.Instance.Progress(n).EconSlip);
			Assert.Equal(3u, Game.Instance.Progress(n).CultureSlip);
		}
	}
}
