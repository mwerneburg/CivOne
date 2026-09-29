// CivOne tests
//
// The east-west figure-eight north of Chelyabinsk (the user, Sep 2026, autopilot). Replayed:
// settlers riding out to found cities on the eastern frontier were drafted onto pollution
// duty and sent 16-19 tiles back west, while others rode out past them; and a colonist that
// reached its site on a turn MayFoundCities said "no" (it flips on a civ sitting at its size
// threshold) could not found, and rode home. Three rules, measured on the save: long reversals
// 3 -> 0 over 8 turns, settler travel down about a quarter, cleaning crew unchanged.
//
//   1. A settler that reaches the site it was SENT to may found there on a "no" turn.
//   2. The pollution draft never takes a settler on its way to found a city.
//   3. Committing a settler to a city site releases it from pollution duty.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ColonistCommitmentTests : System.IDisposable
	{
		private readonly bool _auto;
		public ColonistCommitmentTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; Settings.Instance.Autopilot = false; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		// An AI civ on open grassland. `wide` gives it 21 size-1 cities, well past its city
		// target, so MayFoundCities says no; otherwise one city, so it says yes. The site at
		// 40,25 is more than 3 tiles from all of them.
		private static (Game g, Player p) AWorld(bool wide)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == g.PlayerNumber(p)).ToArray()) g.DisbandUnit(u);
			p.Explore(40, 25, range: 20);
			for (int y = 12; y <= 38; y++)
			for (int x = 26; x <= 58; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				((BaseTile)Map.Instance[x, y]).Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			int id = 0;
			if (wide)
				foreach (int y in new[] { 14, 18, 35 })
				for (int x = 28; x <= 56; x += 4)
					g.AddCity(p, id++, x, y)!.Size = 1;
			else
				g.AddCity(p, id++, 48, 25)!.Size = 4;
			Sim.ClearTasks();
			return (g, p);
		}

		private static Settlers ASettler(Game g, Player p, int x, int y) =>
			(Settlers)g.CreateUnit(UnitType.Settlers, x, y, g.PlayerNumber(p))!;

		// ── 1. a colonist that arrives may found ─────────────────────────────

		[Fact]
		public void AColonistAtItsSiteFoundsEvenOnANoTurn()
		{
			(Game g, Player p) = AWorld(wide: true);
			Assert.False(AI.Instance(p).MayFoundCities(), "fixture: the civ may still found");
			Settlers s = ASettler(g, p, 40, 25);
			s.SettleSite = (40, 25);

			AI.Instance(p).Move(s);
			Sim.Settle();

			Assert.Contains(g.GetCities(), c => c.X == 40 && c.Y == 25);
		}

		// Control: without the commitment the "no" still stands.
		[Fact]
		public void AnUncommittedSettlerDoesNot()
		{
			(Game g, Player p) = AWorld(wide: true);
			Settlers s = ASettler(g, p, 40, 25);

			AI.Instance(p).Move(s);
			Sim.Settle();

			Assert.DoesNotContain(g.GetCities(), c => c.X == 40 && c.Y == 25);
		}

		// ── the nearer site ──────────────────────────────────────────────────

		// A single row of grassland through mountains, so every site in the middle of the row
		// scores exactly the same and only distance can tell them apart. With no distance term
		// the scan took the first of the ties it met — eight tiles off — and neighbouring
		// colonists crossed each other on the rails (Riga, Novgorod — Sep 2026).
		[Fact]
		public void OfEqualSitesTheNearestIsChosen()
		{
			(Game g, Player p) = AWorld(wide: false);   // its city is at 48,25
			for (int y = 12; y <= 38; y++)
			for (int x = 14; x <= 44; x++)
			{
				Map.Instance.ChangeTileType(x, y, y == 25 && x >= 22 ? Terrain.Grassland1 : Terrain.Mountains);
				((BaseTile)Map.Instance[x, y]).Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			Settlers s = ASettler(g, p, 31, 25);

			ITile? site = AI.Instance(p).BestSettleSite(s);

			Assert.NotNull(site);
			Assert.Equal((31, 25), (site!.X, site.Y));
		}

		// ── 2. the draft leaves colonists alone ──────────────────────────────

		private static void Pollute(Game g, Player p)
		{
			City c = p.Cities.First();
			Map.Instance[c.X + 1, c.Y].Pollution = true;
			Assert.True(AI.Instance(p).PollutionBacklog() > 0, "fixture: no pollution backlog");
		}

		[Fact]
		public void AColonistOnItsWayIsNotDrafted()
		{
			(Game g, Player p) = AWorld(wide: true);
			Pollute(g, p);
			Settlers s = ASettler(g, p, 35, 25);
			s.SettleSite = (30, 25);
			s.Goto = new System.Drawing.Point(30, 25);

			AI.Instance(p).Move(s);

			Assert.False(s.AutoClean, "a colonist was turned round for the smog");
		}

		// Control: a settler with nowhere in particular to be is drafted. (The wide civ may
		// not found, so there is no site to release it to.)
		[Fact]
		public void AnIdleSettlerIsStillDrafted()
		{
			(Game g, Player p) = AWorld(wide: true);
			Pollute(g, p);
			City home = p.Cities.First();
			Settlers s = ASettler(g, p, home.X, home.Y);

			AI.Instance(p).Move(s);

			Assert.True(s.AutoClean, "an idle settler was not drafted");
		}

		// ── 3. committing releases ───────────────────────────────────────────

		// The draft runs before the site is chosen, so the same turn could draft a settler and
		// send it to found — and the next turn the smog won. The colonist now wins.
		[Fact]
		public void ASettlerSentToFoundIsNotAlsoACleaner()
		{
			(Game g, Player p) = AWorld(wide: false);
			Pollute(g, p);
			// On its home city's tile: it cannot found here, so it must be SENT somewhere.
			City home = p.Cities.First();
			Settlers s = ASettler(g, p, home.X, home.Y);

			AI.Instance(p).Move(s);

			Assert.NotNull(s.SettleSite);   // fixture: it was sent to found
			Assert.False(s.AutoClean, "sent to found AND drafted: the next turn sends it back");
		}
	}
}
