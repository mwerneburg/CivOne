// CivOne tests
//
// Cruise missiles and reaper drones.
//
// The nuclear arsenal in this game is built and never used: 160 shields, irradiated ground,
// and a chance of waking something worse than warming. The Cruise Missile is the conventional
// answer — a bomber's strike at a third of the price, consumed on use, no fallout. The Reaper
// Drone replaces the Fighter outright: longer legs, cheaper, and it sees two tiles because
// seeing is most of what it is for.
//
// Both are AIR class, which buys two properties without writing them: cargo capacity counts
// only land units (BaseUnitLand.cs:292), so a missile rides a warship without displacing
// troops, and neither is billed for war weariness — there is nobody aboard for anyone at home
// to worry about.
//
// SAM Batteries are now graded rather than binary: under one, an air attacker still strips the
// defender's TERRAIN bonus but no longer its FORTIFICATION.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class UnmannedUnitTests
	{
		private static (Game g, Player p, City c) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 12);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			Sim.ClearTasks();
			return (g, p, c);
		}

		// ── the units themselves ─────────────────────────────────────────────────

		// The whole point of the missile: a bomber's punch, a third of the price.
		[Fact]
		public void TheMissileHitsLikeABomberAtAFractionOfTheCost()
		{
			AWorld();   // constructing a unit touches the palette, so a game must exist
			IUnit missile = new CruiseMissile();
			IUnit bomber = new Bomber();

			Assert.Equal(bomber.Attack, missile.Attack);
			Assert.True(missile.Price * 3 <= bomber.Price,
				$"missile {missile.Price} against bomber {bomber.Price} is not a third");
		}

		// The drone beats the aircraft it replaces on the three things it was asked to beat.
		[Fact]
		public void TheDroneOutclassesTheFighterItReplaces()
		{
			AWorld();
			IUnit drone = new ReaperDrone();
			IUnit fighter = new Fighter();

			Assert.True(drone.Move > fighter.Move, "the drone should fly further");
			Assert.True(drone.Price < fighter.Price, "the drone should cost less");
		}

		// ...and retires it. Obsolescence is how the game withdraws a unit from production
		// without touching the ones already flying.
		[Fact]
		public void RoboticsRetiresTheFighter()
		{
			(Game g, Player p, City c) = AWorld();
			p.AddAdvance(new Flight(), false);
			Assert.True(p.ProductionAvailable(new Fighter()), "fixture: Flight should offer a Fighter");

			p.AddAdvance(new Robotics(), false);

			Assert.False(p.ProductionAvailable(new Fighter()), "the Fighter outlived its replacement");
			Assert.True(p.ProductionAvailable(new ReaperDrone()));
		}

		// The two-tile look the drone was asked for turns out to be the AIR CLASS default:
		// Fighter, Bomber and Nuclear each already override Explore() to radius 2. So this is
		// pinned against a LAND unit, which is the comparison that means anything, and the
		// drone deliberately carries no override of its own.
		[Theory]
		[InlineData(UnitType.ReaperDrone, true)]
		[InlineData(UnitType.Fighter, true)]
		[InlineData(UnitType.Militia, false)]
		public void AircraftSeeTwoTilesOutAndGroundUnitsDoNot(UnitType type, bool expected)
		{
			(Game g, Player p, City c) = AWorld();
			Player blind = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                                 && g.PlayerNumber(x) != 0);
			// KNOWN flat ground: BaseUnit.Explore gives ANY unit a two-tile look from a
			// mountain, and an arbitrary tile on a generated map may be one.
			IUnit unit = g.CreateUnit(type, 36, 22, g.PlayerNumber(blind))!;
			Assert.False(blind.RawVisible(38, 22), "fixture: the far tile must start unseen");

			unit.Explore();

			Assert.True(blind.RawVisible(37, 22), "one tile out should always be seen");
			Assert.Equal(expected, blind.RawVisible(38, 22));
		}

		// ── SAM Batteries, graded ────────────────────────────────────────────────

		// DefendStrength is private, and the whole point is the number it returns.
		private static int DefenceAgainst(IUnit attacker, IUnit defender)
			=> (int)typeof(BaseUnit).GetMethod("DefendStrength",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(attacker, new object[] { defender, attacker })!;

		// A fortified defender in a SAM city, a fortified defender in a city without one, and
		// the same defender fortified against a LAND attacker — the unstripped ceiling.
		private static (int noSam, int sam, int ceiling) ThreeDefences(UnitType airType)
		{
			(Game g, Player p, City c) = AWorld();
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			IUnit air = g.CreateUnit(airType, 39, 25, g.PlayerNumber(foe))!;
			IUnit land = g.CreateUnit(UnitType.Musketeers, 39, 24, g.PlayerNumber(foe))!;
			IUnit defender = g.CreateUnit(UnitType.Musketeers, c.X, c.Y, g.PlayerNumber(p))!;
			defender.Fortify = true;

			int noSam = DefenceAgainst(air, defender);
			c.AddBuilding(new SamBattery());
			c.InvalidateCache();
			int sam = DefenceAgainst(air, defender);
			int ceiling = DefenceAgainst(land, defender);
			return (noSam, sam, ceiling);
		}

		// The grading, for everything that flies at a city. Under a SAM the defender keeps
		// its FORTIFICATION but still loses its TERRAIN bonus — a mountainside does not hide
		// a city from the air. Previously this was all-or-nothing: a SAM restored both, so an
		// aircraft attacking a SAM city was treated exactly like infantry.
		[Theory]
		[InlineData(UnitType.Bomber)]
		[InlineData(UnitType.ReaperDrone)]
		[InlineData(UnitType.CruiseMissile)]
		public void ASamBluntsAnAirAttackWithoutCancellingIt(UnitType airType)
		{
			(int noSam, int sam, int ceiling) = ThreeDefences(airType);

			Assert.True(sam > noSam, $"the SAM did nothing: {noSam} then {sam}");
			Assert.True(sam < ceiling,
				$"the SAM cancelled the air attack entirely: {sam} against a land attacker's {ceiling}");
		}

		// ── the SAM gate ─────────────────────────────────────────────────────────

		private static IProduction[] Plan(Player p, City c)
		{
			var plan = new System.Collections.Generic.List<IProduction>();
			System.Type stance = typeof(AI).GetNestedType("StrategyStance",
				System.Reflection.BindingFlags.NonPublic)!;
			typeof(AI).GetMethod("PlanProductionInto",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { plan, c, System.Enum.Parse(stance, "Develop") });
			return plan.ToArray();
		}

		// The backlog item this began as: the SAM Battery is the one building with no
		// city-output basis at all, so EarnsItsKeep has no honest break-even to compute for
		// it and waved it through on Rocketry alone. It is consulted in exactly one place —
		// when the ATTACKER is air class — so in a world with no aircraft it is 150 shields
		// and 3 gold a turn spent against nothing.
		[Fact]
		public void NoSamBatteryInAWorldWithNoAircraft()
		{
			(Game g, Player p, City c) = AWorld();
			p.AddAdvance(new Rocketry(), false);
			Assert.DoesNotContain(g.GetUnits(), u => u.Class == UnitClass.Air);

			Assert.DoesNotContain(Plan(p, c), x => x is SamBattery);
		}

		// ...and it appears the moment anything flies. Deliberately world-wide rather than
		// "a civ I am at war with": aircraft take a long time to reach, wars here start
		// faster than a battery can be built, and waiting for the war means waiting until
		// it is too late.
		[Fact]
		public void ASamBatteryOnceSomebodyIsFlying()
		{
			(Game g, Player p, City c) = AWorld();
			p.AddAdvance(new Rocketry(), false);
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			g.CreateUnit(UnitType.Bomber, 44, 29, g.PlayerNumber(foe));

			Assert.Contains(Plan(p, c), x => x is SamBattery);
		}

		// ── spent on use ─────────────────────────────────────────────────────────

		// A missile that survives its own strike is a bomber that never needs fuel, at a
		// third of the price. Driven through a real attack from an AI-owned unit: a human
		// unit's move is a visible animation that never completes headless.
		[Fact]
		public void TheMissileIsConsumedByItsOwnStrike()
		{
			(Game g, Player p, City c) = AWorld();
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			p.DeclareWar(foe);
			IUnit missile = g.CreateUnit(UnitType.CruiseMissile, 40, 24, g.PlayerNumber(p))!;
			g.CreateUnit(UnitType.Militia, 40, 23, g.PlayerNumber(foe));
			missile.MovesLeft = missile.Move;
			Sim.ClearTasks();

			missile.MoveTo(0, -1);
			Sim.Settle();

			Assert.DoesNotContain(g.GetUnits(), u => u == missile);
		}

		// ...and the drone is not, or "reusable" means nothing.
		[Fact]
		public void TheDroneSurvivesItsAttack()
		{
			(Game g, Player p, City c) = AWorld();
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			p.DeclareWar(foe);
			bool survived = false;
			for (int i = 0; i < 30 && !survived; i++)
			{
				IUnit drone = g.CreateUnit(UnitType.ReaperDrone, 40, 24, g.PlayerNumber(p))!;
				IUnit target = g.CreateUnit(UnitType.Militia, 40, 23, g.PlayerNumber(foe))!;
				drone.MovesLeft = drone.Move;
				Sim.ClearTasks();

				drone.MoveTo(0, -1);
				Sim.Settle();

				// Only a WON attack proves anything: a drone that loses is destroyed like any
				// attacker, so the test retries until one wins.
				if (!g.GetUnits().Contains(target)) survived = g.GetUnits().Contains(drone);
				if (g.GetUnits().Contains(drone)) g.DisbandUnit(drone);
				if (g.GetUnits().Contains(target)) g.DisbandUnit(target);
			}

			Assert.True(survived, "the drone never survived a won attack in 30 tries");
		}

		// ── no cargo, no weariness ───────────────────────────────────────────────

		// Falls out of being air class rather than being written anywhere, so it is pinned
		// here: if either unit ever becomes a land unit, warships lose their capacity.
		[Fact]
		public void NeitherUnmannedUnitConsumesCargoSpace()
		{
			AWorld();
			Assert.NotEqual(UnitClass.Land, new CruiseMissile().Class);
			Assert.NotEqual(UnitClass.Land, new ReaperDrone().Class);
		}

		// A republic pays one unhappy citizen per unit in the field. Nobody is aboard these.
		[Theory]
		[InlineData(UnitType.ReaperDrone)]
		[InlineData(UnitType.CruiseMissile)]
		public void UnmannedUnitsInTheFieldCauseNoWarWeariness(UnitType type)
		{
			(Game g, Player p, City c) = AWorld();
			p.Government = new Governments.Republic();
			c.Size = 8;
			int before = c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale);

			for (int i = 0; i < 3; i++)
			{
				IUnit u = g.CreateUnit(type, 44, 29, g.PlayerNumber(p))!;   // well away from home
				u.SetHome(c);
			}
			c.InvalidateCache();

			Assert.Equal(before, c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale));
		}

		// ...and a manned one still does, or the exemption is not an exemption.
		[Fact]
		public void AMannedUnitInTheFieldStillDoes()
		{
			(Game g, Player p, City c) = AWorld();
			p.Government = new Governments.Republic();
			c.Size = 8;
			int before = c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale);

			for (int i = 0; i < 3; i++)
			{
				IUnit u = g.CreateUnit(UnitType.Musketeers, 44, 29, g.PlayerNumber(p))!;
				u.SetHome(c);
			}
			c.InvalidateCache();

			Assert.True(c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale) > before,
				"fixture: three musketeers abroad should cost a republic something");
		}
	}
}
