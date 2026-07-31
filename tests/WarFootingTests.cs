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
using CivOne.Units;

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

		// An aggressive leader who out-guns a neighbour arms speculatively — once. It does
		// not justify a permanent war economy, so past three combat units per city the civ
		// has enough. (Frederick of the Germans is Aggressive; the harness's own civ, Caesar,
		// is not — this clause has to be tested on a leader who actually has it.)
		[Fact]
		public void AggressiveCiv_LeavesMilitarize_OnceArmyIsSaturated()
		{
			FreshHuman();
			Settings.Instance.Autopilot = true;
			Player germans = Game.Instance.Players.First(p => p.TribeNamePlural == "Germans");
			Player rival = Game.Instance.Players.First(p => p.TribeNamePlural == "Babylonians");
			Assert.Equal(AggressionLevel.Aggressive, germans.Civilization.Leader.Aggression);

			City home = Game.Instance.AddCity(germans, 0, 40, 25)!;
			Game.Instance.AddCity(rival, 0, 48, 25);          // a neighbour, inside 15 tiles
			byte gid = Game.Instance.PlayerNumber(germans);
			Game.Instance.CreateUnit(UnitType.Legion, home.X, home.Y, gid);   // out-guns them
			Assert.False(Game.Instance.Players.Any(p => p != germans && germans.IsAtWar(p)),
				"precondition: at peace with everyone");
			Assert.Equal("Militarize", AI.Instance(germans).CurrentStanceName());

			// One city, so the ceiling is three combat units. The fourth ends it.
			for (int i = 0; i < 3; i++)
				Game.Instance.CreateUnit(UnitType.Legion, home.X, home.Y, gid);

			Assert.NotEqual("Militarize", AI.Instance(germans).CurrentStanceName());
		}

		// Non-combat units are not an army: diplomats and caravans must not read as "armed
		// enough" and talk a civ out of defending itself.
		[Fact]
		public void CiviliansDoNotCountAsAnArmy()
		{
			FreshHuman();
			Settings.Instance.Autopilot = true;
			Player germans = Game.Instance.Players.First(p => p.TribeNamePlural == "Germans");
			Player rival = Game.Instance.Players.First(p => p.TribeNamePlural == "Babylonians");

			City home = Game.Instance.AddCity(germans, 0, 40, 25)!;
			Game.Instance.AddCity(rival, 0, 48, 25);
			byte gid = Game.Instance.PlayerNumber(germans);
			Game.Instance.CreateUnit(UnitType.Legion, home.X, home.Y, gid);
			Assert.Equal("Militarize", AI.Instance(germans).CurrentStanceName());

			for (int i = 0; i < 6; i++)
				Game.Instance.CreateUnit(UnitType.Diplomat, home.X, home.Y, gid);

			Assert.Equal("Militarize", AI.Instance(germans).CurrentStanceName());
		}

		// Barbarians are a raiding nuisance, not a rival power. They hold no diplomacy and
		// never sue for peace, so treating a nearby horde as a war put civs on a permanent
		// war footing — no growth tech, tax target 6, colonisation capped, and Monarchy
		// pinned at the top of the government table. The answer belongs at the threatened
		// city, not in the empire's economy.
		[Fact]
		public void Barbarians_DoNotPutTheEmpireOnAWarFooting()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;
			City own = Game.Instance.AddCity(player, 0, 40, 25)!;
			player.Explore(40, 25, range: 6);

			// A barbarian city five tiles off, and a raiding party at the gates.
			Game.Instance.AddCity(Barbarians, 1, 45, 25);
			Game.Instance.CreateUnit(UnitType.Legion, own.X + 2, own.Y, 0);
			player.Explore(45, 25, range: 3);

			Assert.NotEqual("Militarize", player.AI!.CurrentStanceName());

			// ...but the threatened city still answers for itself: a hostile within 3 tiles
			// earns a garrison, whatever the empire's posture.
			Sim.ClearTasks();
			AI.Instance(player).CityProduction(own);
			var plan = new System.Collections.Generic.List<IProduction>();
			if (own.CurrentProduction is not null) plan.Add(own.CurrentProduction);
			plan.AddRange(own.ProductionQueue);
			Assert.Contains(plan, p => p is IUnit u && u.Role == UnitRole.Defense);
		}

		// A war being FOUGHT blocks a revolt; a war merely declared does not. AI wars are
		// rarely concluded, only abandoned, so testing the Militarize stance as well as the
		// enemy-at-the-gates test left civs holding an advance they never used — Japan sat
		// in Monarchy with Democracy researched, at war with two civs it was not fighting.
		[Fact]
		public void DormantWar_DoesNotFreezeTheConstitution()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;
			City own = Game.Instance.AddCity(player, 0, 40, 25)!;
			Assert.NotNull(own);
			player.Government = new CivOne.Governments.Monarchy();
			foreach (IAdvance a in Common.Advances)
				player.AddAdvance(a);   // Democracy available, so a better government exists

			Player enemy = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is Civilizations.Barbarian));
			player.DeclareWar(enemy);

			// Nobody at the gates: the war is on paper only.
			foreach (IUnit stray in Game.Instance.GetUnits()
				.Where(u => u.Owner != Game.Instance.PlayerNumber(player)
				         && Common.DistanceToTile(u.X, u.Y, own.X, own.Y) <= 8).ToArray())
				Game.Instance.DisbandUnit(stray);
			Assert.Equal("Militarize", player.AI!.CurrentStanceName());

			for (int i = 0; i < 40 && player.Government is CivOne.Governments.Monarchy; i++)
				player.AI!.ConsiderGovernment();

			Assert.False(player.Government is CivOne.Governments.Monarchy,
				"a civ at war with nobody nearby should still be able to change government");
		}

		// A rival's Caravan or Diplomat loitering on the border is not a siege. They count
		// as hostile only once a war is on the books, and even then they cannot take or
		// hold ground — so they must not be what keeps an empire mobilised.
		[Fact]
		public void EnemyCaravansAndDiplomats_AreNotASiege()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;
			City own = Game.Instance.AddCity(player, 0, 40, 25)!;
			player.Government = new CivOne.Governments.Monarchy();
			foreach (IAdvance a in Common.Advances) player.AddAdvance(a);

			Player enemy = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is Civilizations.Barbarian));
			player.DeclareWar(enemy);
			foreach (IUnit stray in Game.Instance.GetUnits()
				.Where(u => u.Owner != Game.Instance.PlayerNumber(player)
				         && Common.DistanceToTile(u.X, u.Y, own.X, own.Y) <= 16).ToArray())
				Game.Instance.DisbandUnit(stray);

			// Two non-combatants parked right on the doorstep.
			byte eid = Game.Instance.PlayerNumber(enemy);
			Game.Instance.CreateUnit(UnitType.Caravan,  own.X + 1, own.Y, eid);
			Game.Instance.CreateUnit(UnitType.Diplomat, own.X + 2, own.Y, eid);

			for (int i = 0; i < 40 && player.Government is CivOne.Governments.Monarchy; i++)
				player.AI!.ConsiderGovernment();

			Assert.False(player.Government is CivOne.Governments.Monarchy,
				"a caravan and a diplomat on the border must not freeze the constitution");
		}

		// ...but with an enemy actually at the gates it holds its constitution.
		[Fact]
		public void EnemyAtTheGates_StillBlocksARevolt()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;
			City own = Game.Instance.AddCity(player, 0, 40, 25)!;
			player.Government = new CivOne.Governments.Monarchy();
			foreach (IAdvance a in Common.Advances) player.AddAdvance(a);

			Player enemy = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is Civilizations.Barbarian));
			player.DeclareWar(enemy);
			Game.Instance.CreateUnit(UnitType.Legion, own.X + 2, own.Y,
				Game.Instance.PlayerNumber(enemy));   // within the 4-tile threat radius

			for (int i = 0; i < 40; i++) player.AI!.ConsiderGovernment();

			Assert.True(player.Government is CivOne.Governments.Monarchy,
				"with an enemy 2 tiles from the capital, no revolt");
		}

		// Monarchy gives 3 free unit-supports per city, Republic and Democracy give ZERO.
		// A big army therefore evaporates the moment a civ modernises: City.NewTurn disbands
		// the furthest-from-home unit of every city whose shields have gone negative, one per
		// city per turn, silently for an AI. The AI should shed what it cannot carry
		// deliberately, before the revolt, rather than bleed it afterwards.
		[Fact]
		public void BeforeModernising_TheArmyIsDrawnDownDeliberately()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;
			City city = Game.Instance.AddCity(player, 0, 40, 25)!;
			player.Explore(40, 25, range: 3);
			player.Government = new CivOne.Governments.Monarchy();
			foreach (IAdvance a in Common.Advances) player.AddAdvance(a);
			byte id = Game.Instance.PlayerNumber(player);

			// An army far larger than a small city's shields can carry once support is free
			// no longer: they are homed here and standing well away from it.
			for (int i = 0; i < 12; i++)
			{
				IUnit u = Game.Instance.CreateUnit(UnitType.Legion, 40 + 3, 25, id)!;
				u.SetHome(city);
			}
			int before = Game.Instance.GetUnits().Count(u => u.Owner == id);

			player.AI!.ConsiderGovernment();

			int after = Game.Instance.GetUnits().Count(u => u.Owner == id);
			Assert.True(after < before,
				$"expected a deliberate drawdown before the revolt, units went {before} -> {after}");
			Assert.True(player.Government is CivOne.Governments.Monarchy,
				"the drawdown turn does not also revolt");
		}

		// ...and it can NEVER block reform indefinitely. An earlier version read
		// FreeUnitSupport as a hard cap, so a civ had to disband its entire army before it
		// was allowed to modernise — measured over 500 turns that cost half the world's
		// research and killed two civs outright.
		[Fact]
		public void TheDrawdownCannotBlockReformForever()
		{
			Player player = FreshHuman();
			Settings.Instance.Autopilot = true;
			City city = Game.Instance.AddCity(player, 0, 40, 25)!;
			player.Explore(40, 25, range: 3);
			player.Government = new CivOne.Governments.Monarchy();
			foreach (IAdvance a in Common.Advances) player.AddAdvance(a);
			byte id = Game.Instance.PlayerNumber(player);

			// An army so large it can never be paid for: the cap has to give way.
			for (int i = 0; i < 60; i++)
			{
				IUnit u = Game.Instance.CreateUnit(UnitType.Legion, 43, 25, id)!;
				u.SetHome(city);
			}

			// Well past MaxDrawdownTurns, plus room for the revolt's own random roll.
			for (int t = 0; t < 200 && player.Government is CivOne.Governments.Monarchy; t++)
				player.AI!.ConsiderGovernment();

			Assert.False(player.Government is CivOne.Governments.Monarchy,
				"reform must proceed once the drawdown budget is spent, army or no army");
			Assert.True(Game.Instance.GetUnits().Count(u => u.Owner == id) > 0,
				"and it must not have disbanded the entire army to get there");
		}

	}
}
