// CivOne tests
//
// Two ways a civ used to be locked out of ever growing, both found in a 751-turn
// autoplayed island game where Japan finished on 18 advances against a field of
// 70-89, with no Granary or Aqueduct in any of its ten cities:
//
//   1. Autopilot ran the AI's economic logic against the HUMAN's research cost
//      curve, which that logic is not tuned for.
//   2. The speculative Militarize clauses (a barbarian city nearby, a neighbour we
//      out-gun) never expire, and Militarize is the one stance that weights no
//      growth tech at all.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Civilizations;
using CivOne.Advances;

namespace CivOne.Tests
{
	public class WarFootingTests
	{
		// Difficulty 2 game, so a human pays diffFactor 5 and an AI pays 3.
		private static Player FreshHuman()
		{
			Sim.NewGame(width: 80, height: 50, difficulty: 2);
			Settings.Instance.Autopilot = false;
			return Game.Instance.HumanPlayer;
		}

		// The cost formula has a floor of 12, and at zero advances both rates clamp to
		// it — so the surcharge is only observable once a civ has some tech.
		private static void GiveAdvances(Player p, int count)
		{
			foreach (IAdvance advance in Common.Advances.Take(count))
				p.AddAdvance(advance);
		}

		private static Player Barbarians
			=> Game.Instance.Players.First(p => p.Civilization is Civilizations.Barbarian);

		// Both directions on the same player, so nothing depends on what tech another
		// civ happens to start with: a human at the keyboard keeps paying the difficulty
		// surcharge (that is what the setting is for), and the same player under
		// Autopilot pays the flat AI rate. At difficulty 2 that ratio is exactly 5:3 —
		// enough on its own to turn 32 turns per advance into 54.
		[Fact]
		public void TheDifficultySurcharge_AppliesOnlyWhenAHumanIsSteering()
		{
			Player human = FreshHuman();
			GiveAdvances(human, 5);

			short handPlayed = human.ScienceCost;
			Settings.Instance.Autopilot = true;
			short autopiloted = human.ScienceCost;

			Assert.True(autopiloted < handPlayed,
				$"autopilot {autopiloted} should undercut hand-played {handPlayed}");
			Assert.Equal(handPlayed * 3 / 5, autopiloted);
		}

		// A barbarian city parked next door justifies arming — once. It does not justify
		// a permanent war economy, because nothing ever expels it and Militarize weights
		// no growth tech. Past three combat units per city the civ has enough.
		[Fact]
		public void PeacetimeCiv_LeavesMilitarize_OnceArmyIsSaturated()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;   // Player.AI is null for a hand-played human
			City own = Game.Instance.AddCity(player, 0, 40, 25)!;
			Assert.NotNull(own);

			// A visible barbarian city 5 tiles off, and no war with anyone.
			Game.Instance.AddCity(Barbarians, 1, 45, 25);
			player.Explore(45, 25, range: 3);
			Assert.False(Game.Instance.Players.Any(p => p != player && player.IsAtWar(p)),
				"precondition: this civ is at peace");
			Assert.Equal("Militarize", player.AI!.CurrentStanceName());

			// One city, so the ceiling is three combat units. The fourth ends it.
			for (int i = 0; i < 4; i++)
				Game.Instance.CreateUnit(UnitType.Legion, own.X, own.Y, Game.Instance.PlayerNumber(player));

			Assert.NotEqual("Militarize", player.AI!.CurrentStanceName());
		}

		// Non-combat units are not an army: nine Diplomats and seven Caravans (which is
		// what Japan actually held alongside its Legions) must not read as "armed enough"
		// and talk a genuinely threatened civ out of defending itself.
		[Fact]
		public void CiviliansDoNotCountAsAnArmy()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;
			City own = Game.Instance.AddCity(player, 0, 40, 25)!;
			Game.Instance.AddCity(Barbarians, 1, 45, 25);
			player.Explore(45, 25, range: 3);
			Assert.Equal("Militarize", player.AI!.CurrentStanceName());

			for (int i = 0; i < 6; i++)
				Game.Instance.CreateUnit(UnitType.Diplomat, own.X, own.Y, Game.Instance.PlayerNumber(player));

			Assert.Equal("Militarize", player.AI!.CurrentStanceName());
		}
	}
}
