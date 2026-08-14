// CivOne tests
//
// Battlefield salvage and reverse engineering.
//
// A won attack destroyed the defender, always — there was no unit capture in the game at
// all. Now, when the loser is hardware we could not have built ourselves, there is a one-in-
// four chance we take it intact instead. Hold it twenty turns and the engineers work out how
// it is made; lose it before then and you learn nothing, which is the whole point — a captured
// MechInf is worth more parked in the rear than thrown back into the line.
//
// The gates matter more than the payout. Salvage fires ONLY on a lone land unit in the open,
// outside a city, whose RequiredTech we lack. Harvesters and the other unbuildable barbarian
// units carry RequiredTech null, so alien machinery is never salvageable no matter how long
// it is held — that falls out of the same check rather than needing a rule of its own.

using System.IO;
using System.Linq;
using System.Reflection;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SalvageTests
	{
		private static readonly MethodInfo _salvage = typeof(BaseUnit).GetMethod(
			"Salvage", BindingFlags.NonPublic | BindingFlags.Instance)!;

		private static bool TrySalvage(IUnit winner, IUnit loser)
			=> (bool)_salvage.Invoke(winner, new object[] { loser })!;

		// Two players on open grassland, far from any city.
		private static (Player attacker, Player defender) TwoSides()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player[] real = g.Players.Where(x => x is not null && g.PlayerNumber(x) != 0).ToArray();
			for (int y = 23; y <= 27; y++)
			for (int x = 38; x <= 42; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			return (real[0], real[1]);
		}

		private static IUnit Unit(Player owner, UnitType type, int x, int y)
			=> Game.Instance.CreateUnit(type, x, y, Game.Instance.PlayerNumber(owner))!;

		// The roll is one in four, so a single attempt proves nothing either way. Both
		// directions are asserted over many attempts: 200 consecutive failures at 25% is
		// about 1e-25, and a gate that leaks shows up as any success at all.
		private static int SalvagesIn200Attempts(System.Func<(IUnit winner, IUnit loser)> stage)
		{
			int taken = 0;
			for (int i = 0; i < 200; i++)
			{
				(IUnit winner, IUnit loser) = stage();
				if (TrySalvage(winner, loser)) taken++;
				Game.Instance.DisbandUnit(loser);
				Game.Instance.DisbandUnit(winner);
			}
			return taken;
		}

		[Fact]
		public void SuperiorHardwareIsSometimesTakenIntact()
		{
			(Player att, Player def) = TwoSides();
			def.AddAdvance(new Conscription(), false);   // Riflemen

			int taken = SalvagesIn200Attempts(() =>
				(Unit(att, UnitType.Militia, 40, 25), Unit(def, UnitType.Riflemen, 41, 25)));

			Assert.True(taken > 0, "nothing was ever salvaged in 200 wins over superior hardware");
		}

		// ...and the unit really changes hands, with the clock started on it.
		[Fact]
		public void ASalvagedUnitChangesHandsAndStartsItsClock()
		{
			(Player att, Player def) = TwoSides();
			def.AddAdvance(new Conscription(), false);
			Game g = Game.Instance;

			IUnit? taken = null;
			for (int i = 0; i < 200 && taken is null; i++)
			{
				IUnit winner = Unit(att, UnitType.Militia, 40, 25);
				IUnit loser  = Unit(def, UnitType.Riflemen, 41, 25);
				if (TrySalvage(winner, loser)) taken = loser;
				else g.DisbandUnit(loser);
				g.DisbandUnit(winner);
			}
			Assert.NotNull(taken);

			Assert.Equal(g.PlayerNumber(att), taken!.Owner);
			Assert.Equal(Game.Instance.GameTurn, taken.CapturedOn);
			Assert.Null(taken.Home);
			Assert.Equal(0, taken.MovesLeft);
		}

		// Nothing to learn: we already build these.
		[Fact]
		public void HardwareWeAlreadyBuildIsNotWorthSalvaging()
		{
			(Player att, Player def) = TwoSides();
			att.AddAdvance(new Conscription(), false);
			def.AddAdvance(new Conscription(), false);

			int taken = SalvagesIn200Attempts(() =>
				(Unit(att, UnitType.Militia, 40, 25), Unit(def, UnitType.Riflemen, 41, 25)));

			Assert.Equal(0, taken);
		}

		// Alien machinery teaches nobody anything. RequiredTech is null on the Harvester, so
		// this is the same gate as above rather than a special case — but it is the case the
		// setting cares about, so it is pinned separately.
		[Fact]
		public void AlienMachineryIsNeverSalvageable()
		{
			(Player att, Player _) = TwoSides();
			Game g = Game.Instance;

			int taken = 0;
			for (int i = 0; i < 200; i++)
			{
				IUnit winner  = Unit(att, UnitType.Militia, 40, 25);
				IUnit harvest = g.CreateUnit(UnitType.Harvester, 41, 25, 0)!;
				if (TrySalvage(winner, harvest)) taken++;
				g.DisbandUnit(harvest);
				g.DisbandUnit(winner);
			}

			Assert.Equal(0, taken);
		}

		// A garrison is not salvage — taking the city is the prize, and city capture already
		// has its own advance-stealing path.
		[Fact]
		public void AGarrisonIsNotSalvage()
		{
			(Player att, Player def) = TwoSides();
			def.AddAdvance(new Conscription(), false);
			Game.Instance.AddCity(def, 0, 41, 25);

			int taken = SalvagesIn200Attempts(() =>
				(Unit(att, UnitType.Militia, 40, 25), Unit(def, UnitType.Riflemen, 41, 25)));

			Assert.Equal(0, taken);
		}

		// Flipping one unit's flag inside an enemy stack would leave it standing among units
		// still at war with it.
		[Fact]
		public void NothingIsTakenOutOfAStack()
		{
			(Player att, Player def) = TwoSides();
			def.AddAdvance(new Conscription(), false);
			Game g = Game.Instance;

			int taken = 0;
			for (int i = 0; i < 200; i++)
			{
				IUnit winner = Unit(att, UnitType.Militia, 40, 25);
				IUnit loser  = Unit(def, UnitType.Riflemen, 41, 25);
				IUnit escort = Unit(def, UnitType.Riflemen, 41, 25);
				if (TrySalvage(winner, loser)) taken++;
				g.DisbandUnit(escort);
				g.DisbandUnit(loser);
				g.DisbandUnit(winner);
			}

			Assert.Equal(0, taken);
		}

		// ── the wiring ───────────────────────────────────────────────────────────

		// Everything above calls Salvage directly. This one goes through a real attack, so
		// that the rule is known to be REACHED — a correct predicate wired to nothing is the
		// failure mode these tests exist to catch.
		//
		// Driven from an AI-owned unit: a human unit's move is a visible animation that never
		// completes headless (see AutopilotSymmetryTests), and salvage happens in the
		// movement's Done handler. Armor against Riflemen wins most of the time, so ~40
		// attacks is plenty for a one-in-four roll to land at least once.
		[Fact]
		public void ARealAttackCanTakeTheDefenderIntact()
		{
			bool everTaken = false;
			for (int i = 0; i < 40 && !everTaken; i++)
			{
				(Player att, Player def) = TwoSides();
				Game g = Game.Instance;
				att.AddAdvance(new Automobile(), false);     // Armor
				def.AddAdvance(new Conscription(), false);    // Riflemen — and we lack it
				att.DeclareWar(def);
				att.Explore(40, 25, range: 6);

				IUnit attacker = Unit(att, UnitType.Armor, 40, 25);
				IUnit defender = Unit(def, UnitType.Riflemen, 41, 25);
				attacker.MovesLeft = attacker.Move;
				Sim.ClearTasks();

				attacker.MoveTo(1, 0);
				Sim.Settle();

				everTaken = defender.Owner == g.PlayerNumber(att) && defender.CapturedOn is not null;
			}

			Assert.True(everTaken, "40 real attacks and not one defender was ever taken intact");
		}

		// ── the twenty-turn clock ────────────────────────────────────────────────

		private static IUnit ACapturedRifleman(int heldFor)
		{
			(Player att, Player def) = TwoSides();
			IUnit u = Unit(att, UnitType.Riflemen, 40, 25);
			u.CapturedOn = Game.Instance.GameTurn - heldFor;
			return u;
		}

		[Fact]
		public void NineteenTurnsTeachesNothing()
		{
			IUnit u = ACapturedRifleman(heldFor: BaseUnit.ReverseEngineerTurns - 1);

			u.NewTurn();

			Assert.False(Game.Instance.GetPlayer(u.Owner).HasAdvance<Conscription>());
			Assert.NotNull(u.CapturedOn);
		}

		[Fact]
		public void TwentyTurnsPaysOutTheAdvance()
		{
			IUnit u = ACapturedRifleman(heldFor: BaseUnit.ReverseEngineerTurns);

			u.NewTurn();

			Assert.True(Game.Instance.GetPlayer(u.Owner).HasAdvance<Conscription>(),
				"the engineers never finished taking it apart");
			// ...and the clock stops, or every later turn re-checks it forever.
			Assert.Null(u.CapturedOn);
		}

		// The advance is learned, not discovered.
		//
		// Pinned on an UNCLAIMED origin, which is the only case that can distinguish the two:
		// SetAdvanceOrigin is first-writer-wins, so once a real discoverer is on record the
		// flag cannot matter, and the first version of this test passed with the fix removed.
		// The live case is a tech that reached its owner without a discovery — a hut, a
		// handicap grant, an earlier act of salvage. Credit drives the Great Library, and
		// looting a unit must not make you the civ that invented it.
		[Fact]
		public void ReverseEngineeringDoesNotClaimDiscovery()
		{
			(Player att, Player def) = TwoSides();
			def.AddAdvance(new Conscription(), false);   // granted, no origin recorded
			Assert.False(Game.Instance.GetAdvanceOrigin(new Conscription(), def),
				"fixture: the origin must be unclaimed, or this test proves nothing");

			IUnit u = Unit(att, UnitType.Riflemen, 40, 25);
			u.CapturedOn = Game.Instance.GameTurn - BaseUnit.ReverseEngineerTurns;

			u.NewTurn();

			Assert.True(att.HasAdvance<Conscription>());
			Assert.False(Game.Instance.GetAdvanceOrigin(new Conscription(), att),
				"the looter was credited with inventing what it took apart");
		}

		// A unit we BUILT never runs the clock, however long we hold it.
		[Fact]
		public void OurOwnUnitsTeachUsNothing()
		{
			(Player att, Player _) = TwoSides();
			IUnit u = Unit(att, UnitType.Riflemen, 40, 25);
			Assert.Null(u.CapturedOn);

			for (int i = 0; i < BaseUnit.ReverseEngineerTurns + 2; i++) u.NewTurn();

			Assert.False(att.HasAdvance<Conscription>());
		}

		// The clock is worthless if it resets on reload — twenty turns is longer than most
		// people play in one sitting.
		[Fact]
		public void TheClockSurvivesSaveAndLoad()
		{
			(Player att, Player _) = TwoSides();
			IUnit u = Unit(att, UnitType.Riflemen, 40, 25);
			u.CapturedOn = 7;

			string path = Path.Combine(Settings.Instance.SavesDirectory, "salvage.cos");
			Game.Instance.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "LoadCos should succeed");

			IUnit reloaded = Game.Instance.GetUnits().Single(x => x.Type == UnitType.Riflemen);
			Assert.Equal(7, reloaded.CapturedOn);
		}
	}
}
