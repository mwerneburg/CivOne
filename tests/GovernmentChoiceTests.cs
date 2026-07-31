// CivOne tests
//
// Communism was unreachable. Scored flat by stance it came second in war (behind
// Monarchy) and third in peace (behind both republics), and since BestGovernment only
// returns something scoring STRICTLY higher than the current government, no AI civ
// could adopt it by any path. Its two real advantages — corruption charged as though
// every city sat ten tiles out, and +50% science in every city — are situational, so
// they are now scored situationally.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using Gov = CivOne.Governments;

namespace CivOne.Tests
{
	public class GovernmentChoiceTests
	{
		private static Player ModernCiv(int cities, int spread)
		{
			Sim.NewGame(width: 80, height: 50, seed: 4242);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			foreach (IAdvance a in Common.Advances) player.AddAdvance(a);

			int cx = 40, cy = 25;
			// River tiles, because corruption is charged on TRADE and grassland yields none —
			// a realm with no commerce has no graft to compare governments over.
			for (int dy = -18; dy <= 18; dy++)
			for (int dx = -20; dx <= 20; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();
			player.Explore(cx, cy, range: 30);

			// A capital, then cities laid out on a grid at `spread` intervals from it. The
			// spacing is the whole point: corruption under a republic scales with distance
			// from the capital, so a packed realm and a far-flung one are different games.
			Game.Instance.AddCity(player, 0, cx, cy);
			int placed = 1;
			for (int ring = 1; ring <= 4 && placed < cities; ring++)
			for (int dy = -ring; dy <= ring && placed < cities; dy++)
			for (int dx = -ring; dx <= ring && placed < cities; dx++)
			{
				if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != ring) continue;
				int x = cx + dx * spread, y = cy + dy * spread;
				if (y < 6 || y > Map.HEIGHT - 6) continue;
				City? c = Game.Instance.AddCity(player, placed, x, y);
				if (c is null) continue;
				// Working cities: AddCity makes them size 1, which trades almost nothing.
				c.Size = 4;
				c.ResetResourceTiles();
				placed++;
			}
			return player;
		}

		// The Republic and Democracy are still the right answer for a compact realm: there
		// is little graft for Communism's fixed-distance corruption to fix.
		[Fact]
		public void CompactRealm_StillPrefersARepublic()
		{
			Player player = ModernCiv(cities: 4, spread: 1);
			player.Government = new Gov.Monarchy();

			string? best = player.AI!.BestGovernmentName();

			Assert.NotEqual("Communism", best);
		}

		// Communism must at least be REACHABLE — that was the whole defect. Scored flat it
		// sat behind something in every stance, and BestGovernment only moves on a strictly
		// higher score, so no civ could adopt it by any path. Its niche is the LATE war
		// government: everything Monarchy offers, plus +50% science and corruption that
		// stops growing with distance.
		[Fact]
		public void Communism_BeatsMonarchy_ForALargeEmpireAtWar()
		{
			Player player = ModernCiv(cities: 24, spread: 8);
			player.Government = new Gov.Monarchy();
			Player enemy = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is CivOne.Civilizations.Barbarian));
			player.DeclareWar(enemy);
			// A war being fought, so the stance is genuinely Militarize.
			City capital = player.Cities.First();
			Game.Instance.CreateUnit(UnitType.Legion, capital.X + 2, capital.Y,
				Game.Instance.PlayerNumber(enemy));
			Assert.Equal("Militarize", player.AI!.CurrentStanceName());

			int communism = player.AI!.GovernmentScoreForTest(new Gov.Communism());
			int monarchy  = player.AI!.GovernmentScoreForTest(new Gov.Monarchy());

			Assert.True(communism > monarchy,
				$"a sprawling empire at war should prefer Communism ({communism}) to Monarchy ({monarchy}); "
				+ $"cities {player.Cities.Length}, spread {player.AI!.EmpireSpreadForTest()}");
		}

		// ...but a small, compact realm at war still wants Monarchy: Communism's fixed
		// corruption is a worse deal than Monarchy's lower multiplier when nothing is far away.
		[Fact]
		public void CompactRealmAtWar_StillPrefersMonarchy()
		{
			Player player = ModernCiv(cities: 3, spread: 2);
			player.Government = new Gov.Monarchy();
			Player enemy = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is CivOne.Civilizations.Barbarian));
			player.DeclareWar(enemy);
			City capital = player.Cities.First();
			Game.Instance.CreateUnit(UnitType.Legion, capital.X + 2, capital.Y,
				Game.Instance.PlayerNumber(enemy));

			Assert.True(player.AI!.GovernmentScoreForTest(new Gov.Monarchy())
			         >= player.AI!.GovernmentScoreForTest(new Gov.Communism()));
		}

		// The United Nations moved off Communism so an early Communism does not drag it
		// forward; it now hangs on Globalism, which carries Communism's old prerequisites.
		[Fact]
		public void UnitedNations_RequiresGlobalism_NotCommunism()
		{
			Sim.NewGame(width: 80, height: 50);
			var un = Reflect.GetWonders().First(w => w.Name == "United Nations");

			Assert.Equal("Globalism", un.RequiredTech?.Name);
		}

		// ...and Communism itself now sits where The Republic does, not behind industry.
		[Fact]
		public void Communism_SharesTheRepublicsPrerequisites()
		{
			Sim.NewGame(width: 80, height: 50);
			IAdvance communism = Common.Advances.First(a => a.Name == "Communism");
			IAdvance republic  = Common.Advances.First(a => a.Name == "The Republic");

			Assert.Equal(republic.RequiredTechs.Select(t => t.Name).OrderBy(n => n),
			             communism.RequiredTechs.Select(t => t.Name).OrderBy(n => n));
		}

		// The three wonders Communism used to retire keep their working lives: they now
		// obsolete on Industrialization, which was Communism's own old prerequisite.
		[Fact]
		public void WondersRetiredByCommunism_KeepTheirWorkingLives()
		{
			Sim.NewGame(width: 80, height: 50);
			foreach (string name in new[] { "Pyramids", "Hagia Sofia", "Michelangelo's Chapel" })
			{
				var w = Reflect.GetWonders().First(x => x.Name == name);
				Assert.Equal("Industrialization", w.ObsoleteTech?.Name);
			}
		}
	}
}
