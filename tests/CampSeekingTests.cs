// CivOne tests
//
// Walking to a resource deposit, rather than tripping over one.
//
// Camps were opportunistic only: a settler standing on an unclaimed Iron/Coal/Oil deposit
// claimed it, and nothing ever walked to one. Measured over a finished 750-turn game that was
// 0.7% of all AI tile work, so a civ with no iron in any city radius paid +50% shields on
// everything needing it (City.ProductionCost) for the rest of the game.
//
// The gate is what makes this cheap: Game.HasResource is satisfied by a city working the
// deposit OR a camp on it, so a camp is worth walking to only for a material the civ does NOT
// already hold. BestCampSite returns null the moment all three are held, which is most civs
// most of the game.
//
// Settlers.CanCampOn is shared by the scan and the builder deliberately. The two halves of
// every other settler job here have drifted apart at least twice — the settle scan against the
// founder (six settlers converging on a mountain), the work scan against the irrigator — and
// both times the symptom was a settler walking somewhere it could not do the job.
//
// FIXTURE TRAP, paid for twice while writing these: `Special` is POSITIONAL.
// Map.ChangeTileType recomputes it from Map.TileIsSpecial(x, y) on every terrain change, so
// setting `.Special = false` is undone by any later type change, and any Mountains tile whose
// coordinates hit the pattern carries iron whether the fixture wanted one there or not.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CampSeekingTests
	{
		// A civ with one city and a deposit some distance away, outside every city radius.
		private static (Game g, Player p, City c, ITile deposit) AWorldWithADeposit(
			bool giveTheCivTheResource = false)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 20);
			// Cleared WELL past CampSearchRange (12) in every direction: a stray peak just
			// outside a narrower box gave the scan a different deposit to find, and three of
			// these tests failed against correct code.
			for (int y = 8; y <= 42; y++)
			for (int x = 24; x <= 60; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				((BaseTile)Map.Instance[x, y]).Special = false;
			}
			// Hills carrying a special is Coal (Game.ResourceAt). Set AFTER the type change,
			// which would otherwise recompute it away.
			Map.Instance.ChangeTileType(46, 25, Terrain.Hills);
			((BaseTile)Map.Instance[46, 25]).Special = true;
			Map.Instance.RecalculateContinentsIfDirty();

			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 4;
			if (giveTheCivTheResource)
			{
				// A camp of our own elsewhere satisfies HasResource just as a worked tile does.
				Map.Instance.ChangeTileType(34, 30, Terrain.Hills);
				((BaseTile)Map.Instance[34, 30]).Special = true;
				g.ResourceCamps[(34, 30)] = g.PlayerNumber(p);
			}
			Sim.ClearTasks();
			return (g, p, c, Map.Instance[46, 25]);
		}

		private static ITile? CampSite(Player p, IUnit settler)
			=> (ITile?)typeof(AI).GetMethod("BestCampSite",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), new object[] { settler });

		private static void MoveAI(Player p, IUnit unit)
			=> typeof(AI).GetMethod("Move",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), new object[] { unit });

		private static IUnit ASettler(Game g, Player p, int x, int y)
		{
			IUnit s = g.CreateUnit(UnitType.Settlers, x, y, g.PlayerNumber(p))!;
			s.MovesLeft = s.Move;
			return s;
		}

		[Fact]
		public void TheFixtureIsARealDeposit()
		{
			(Game g, Player p, City c, ITile deposit) = AWorldWithADeposit();

			Assert.NotEqual(StrategicResource.None, Game.ResourceAt(deposit));
			Assert.True(Settlers.CanCampOn(deposit));
		}

		// The change: a civ short of the material walks to it.
		[Fact]
		public void ACivShortOfTheMaterialSeeksTheDeposit()
		{
			(Game g, Player p, City c, ITile deposit) = AWorldWithADeposit();
			IUnit settler = ASettler(g, p, 41, 25);

			ITile? site = CampSite(p, settler);

			Assert.NotNull(site);
			Assert.Equal((deposit.X, deposit.Y), (site!.X, site.Y));
		}

		// ...and a civ that already holds it does not. A second iron deposit removes no
		// penalty, and this is what keeps the scan inert for most civs most of the game.
		[Fact]
		public void ACivThatAlreadyHoldsTheMaterialIgnoresIt()
		{
			(Game g, Player p, City c, ITile deposit) = AWorldWithADeposit(giveTheCivTheResource: true);
			IUnit settler = ASettler(g, p, 41, 25);
			Assert.True(g.HasResource(p, Game.ResourceAt(deposit)), "fixture: the civ should already hold it");

			Assert.Null(CampSite(p, settler));
		}

		[Fact]
		public void AClaimedDepositIsNotSought()
		{
			(Game g, Player p, City c, ITile deposit) = AWorldWithADeposit();
			Player rival = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                                 && g.PlayerNumber(x) != 0);
			g.ResourceCamps[(deposit.X, deposit.Y)] = g.PlayerNumber(rival);
			IUnit settler = ASettler(g, p, 41, 25);

			Assert.Null(CampSite(p, settler));
		}

		// One settler per deposit. Without this every idle settler in the empire converges on
		// the same hill — the pathology six Malian settlers showed against a mountain.
		[Fact]
		public void TwoSettlersDoNotConvergeOnOneDeposit()
		{
			(Game g, Player p, City c, ITile deposit) = AWorldWithADeposit();
			IUnit first = ASettler(g, p, 41, 25);
			ITile? firstSite = CampSite(p, first);
			Assert.NotNull(firstSite);
			first.Goto = new System.Drawing.Point(firstSite!.X, firstSite.Y);

			IUnit second = ASettler(g, p, 42, 26);

			Assert.Null(CampSite(p, second));
		}

		// The scan and the builder must agree, which is the whole reason CanCampOn exists.
		[Fact]
		public void TheBuilderAcceptsWhatTheScanChose()
		{
			(Game g, Player p, City c, ITile deposit) = AWorldWithADeposit();
			IUnit settler = ASettler(g, p, 41, 25);
			ITile site = CampSite(p, settler)!;

			IUnit arrived = ASettler(g, p, site.X, site.Y);

			Assert.True(((Settlers)arrived).BuildCamp(), "the builder refused the site the scan chose");
		}

		// ── the scan reaching the settler ────────────────────────────────────────

		// A correct predicate wired to nothing is the failure this project keeps producing —
		// the Longboat rule sat in the plan for three complete games without ever being built.
		//
		// Three pieces of precedence had to be worked around here, and all three are correct
		// behaviour that earlier versions of this test tripped over:
		//
		//   1. A settler improves the tile it is STANDING on before any site search runs.
		//   2. FOUNDING outranks camping, so nothing in reach may be a legal city site.
		//   3. The deposit must lie OUTSIDE every city radius, or BestImproveSite wants the
		//      same mountain — it is minable — and the test passes with camp-seeking disabled.
		//      That is precisely what the first version did.
		//
		// Hence a corridor of ARCTIC: walkable, carries no resource, and not a legal city site.
		[Fact]
		public void TheSettlerIsActuallySentToTheDeposit()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 20);
			for (int y = 8; y <= 42; y++)
			for (int x = 24; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);

			// The fixture cannot plant a special, so it goes looking for a spot where a
			// mountain would carry ore, and builds the corridor out to THAT row.
			int depositX = -1, row = -1;
			for (int y = 20; y <= 30 && row < 0; y++)
			for (int x = 46; x <= 58 && row < 0; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Mountains);
				if (Game.ResourceAt(Map.Instance[x, y]) != StrategicResource.None) { depositX = x; row = y; }
				else Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			}
			Assert.True(row > 0, "fixture: found nowhere in the box that carries ore");

			for (int x = 40; x < depositX; x++)
				Map.Instance.ChangeTileType(x, row, Terrain.Arctic);
			Map.Instance.RecalculateContinentsIfDirty();

			City c = g.AddCity(p, 0, 40, row)!;
			c.Size = 4;
			// Pre-roaded: precedence (1) above. Arctic takes a road, so without this the
			// settler starts one where it stands and never reaches the site search.
			Map.Instance[41, row].Road = true;
			IUnit settler = ASettler(g, p, 41, row);
			Sim.ClearTasks();

			MoveAI(p, settler);

			Assert.Equal(new System.Drawing.Point(depositX, row), settler.Goto);
		}

		// ...and the arrival half: standing on it, the settler claims it. That path already
		// existed (the opportunistic block) and sits ahead of the Goto logic, which is why no
		// extra routing was needed — pinned so a reshuffle cannot quietly break it.
		[Fact]
		public void ArrivingOnTheDepositClaimsIt()
		{
			(Game g, Player p, City c, ITile deposit) = AWorldWithADeposit();
			IUnit settler = ASettler(g, p, deposit.X, deposit.Y);
			Sim.ClearTasks();

			MoveAI(p, settler);

			Assert.True(((Settlers)settler).BuildingCamp > 0, "the settler stood on the deposit and did nothing");
		}

		// One statement of the rule: the builder must not restate the conditions the scan
		// uses, or the two drift.
		[Fact]
		public void TheRuleLivesInOnePlace()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Units", "Settlers.cs"));

			int at = src.IndexOf("public bool BuildCamp()");
			Assert.True(at > 0, "BuildCamp has moved or been rewritten");
			string body = src.Substring(at, src.IndexOf("BuildingCamp = 3;", at) - at);
			Assert.Contains("CanCampOn(tile)", body);
			Assert.DoesNotContain("ResourceCamps.ContainsKey", body);
		}
	}
}
