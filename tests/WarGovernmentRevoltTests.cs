// CivOne tests
//
// A Republic or Democracy AI is disarmed three times over: it never declares war
// (AI.Strategy.ConsiderWar), never builds attackers (:3182) and never militarises (:3405).
// Since the AI actively climbs the government ladder, a world that develops peacefully
// stays that way — measured across six games, three flatlined to 0% at-war by turn 300 and
// never recovered.
//
// ConsiderWarFooting is the escape a human player has always had: revolt to a war
// government when you want a war. It sets an appetite; BestGovernment then scores against
// Militarize (Democracy rates 2 there, Communism 5+), the existing revolt logic fires, and
// the civ comes out of anarchy able to fight. The appetite decays so it climbs back after.
//
// Sibling of WarFootingTests, which covers the Militarize STANCE. This file is about the
// constitution.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class WarGovernmentRevoltTests
	{
		// A large warlike democracy beside a much smaller neighbour, nobody at the gates.
		private static (Player big, Player small, AI ai) ATemptedRepublic(bool warlike = true)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player[] ps = g.Players
				.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer)
				.ToArray();

			Player big = ps.First(p => warlike
				? (p.Civilization.Leader.Militarism == MilitarismLevel.Militaristic
				|| p.Civilization.Leader.Aggression == AggressionLevel.Aggressive)
				: (p.Civilization.Leader.Militarism == MilitarismLevel.Civilized
				&& p.Civilization.Leader.Aggression != AggressionLevel.Aggressive));
			Player small = ps.First(p => p != big);

			// A government worth revolting TO must actually be available — AvailableGovernments
			// gates on the tech. Without this the civ correctly refuses, which is the guard
			// working, not the feature failing.
			big.AddAdvance(new CivOne.Advances.Monarchy(), false);
			big.Government = new Democracy();
			for (int i = 0; i < 8; i++)
			{
				big.Explore(20 + i * 3, 25, range: 3);
				g.AddCity(big, i, 20 + i * 3, 25);
			}
			for (int i = 0; i < 2; i++)
			{
				small.Explore(46 + i * 3, 25, range: 3);
				g.AddCity(small, 20 + i, 46 + i * 3, 25);
			}
			Sim.ClearTasks();
			return (big, small, AI.Instance(big));
		}

		private static bool DriveUntilWarFooting(AI ai, int turns = 600)
		{
			for (int t = 0; t < turns; t++)
			{
				Game.Instance.GameTurn = (ushort)(200 + t);
				ai.ConsiderWar();
				if (ai.WantsWarFooting) return true;
			}
			return false;
		}

		[Fact]
		public void AWarlikeRepublicEventuallyWantsAWarFooting()
		{
			var (big, small, ai) = ATemptedRepublic();
			Assert.True(big.RepublicDemocratic, "setup must start as a republic/democracy");

			Assert.True(DriveUntilWarFooting(ai),
				"a warlike republic beside a much weaker neighbour should want a war footing");
		}

		// The government ladder must still pacify a peaceful world — this only stops that
		// being the ONLY outcome.
		[Fact]
		public void ACivilisedRepublicNeverDoes()
		{
			var (big, small, ai) = ATemptedRepublic(warlike: false);
			Assert.False(DriveUntilWarFooting(ai),
				"a civilised, non-aggressive leader should not tear up its constitution");
		}

		// No temptation, no revolution.
		[Fact]
		public void AnEvenlyMatchedNeighbourIsNoTemptation()
		{
			var (big, small, ai) = ATemptedRepublic();
			Game g = Game.Instance;
			for (int i = 0; i < 8; i++)
			{
				small.Explore(46 + i * 3, 32, range: 3);
				g.AddCity(small, 30 + i, 46 + i * 3, 32);
			}
			Assert.False(DriveUntilWarFooting(ai),
				"an equal-sized neighbour should not tempt a republic into revolution");
		}

		// The payoff: while the appetite stands, the best government is a war government.
		[Fact]
		public void WhileOnAWarFootingTheBestGovernmentHasNoWarWeariness()
		{
			var (big, small, ai) = ATemptedRepublic();
			Assert.True(DriveUntilWarFooting(ai));

			string? target = ai.BestGovernmentName();
			Assert.NotNull(target);
			Assert.NotEqual("Democracy", target);
			Assert.NotEqual("Republic", target);
		}

		// ...and it decays, so a civ does not stay a dictatorship for ever.
		[Fact]
		public void TheAppetiteDecays()
		{
			var (big, small, ai) = ATemptedRepublic();
			Assert.True(DriveUntilWarFooting(ai));

			// Out of the republic, as the revolt would leave it — now the appetite ticks down.
			big.Government = new Monarchy();
			for (int t = 0; t < 80 && ai.WantsWarFooting; t++)
			{
				Game.Instance.GameTurn = (ushort)(600 + t);
				ai.ConsiderWar();
			}
			Assert.False(ai.WantsWarFooting, "the war footing should not be permanent");
		}
	}
}
