// CivOne tests
//
// Guards the government upgrade path. BestGovernment only returns something
// scoring STRICTLY higher than the current government, so a scoring table that
// makes Monarchy the peak outside Develop turns Monarchy into a terminus: the
// search returns null, ConsiderGovernment exits, and no civ ever revolts again.
// Measured at 1973 AD before the fix, every surviving civ was in Monarchy — the
// Lakota on 65 cities and 76 advances.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;
using Gov = CivOne.Governments;

namespace CivOne.Tests
{
	public class GovernmentProgressionTests
	{
		// A civ holding Monarchy with the modern constitutions researched, and one city
		// on open land so its stance is Expand.
		private static Player ModernCiv()
		{
			// Seeded: these tests reason about who is near whom, and an unpinned map put a
			// rival city inside the threat radius on some runs and not others.
			Sim.NewGame(width: 80, height: 50, seed: 909);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;

			ITile site = Map.Instance.AllTiles().First(t => !t.IsOcean && t.Y > 6 && t.Y < Map.HEIGHT - 6);
			player.Explore(site.X, site.Y, range: 8);
			Assert.NotNull(Game.Instance.AddCity(player, 0, site.X, site.Y));

			player.AddAdvance(new CivOne.Advances.Monarchy(), false);
			player.AddAdvance(new CivOne.Advances.TheRepublic(), false);
			player.AddAdvance(new CivOne.Advances.Democracy(), false);
			player.Government = new Gov.Monarchy();
			return player;
		}

		// A civ at peace holding Monarchy, with the modern constitutions researched,
		// must want to move on. The stance here is Expand (a fresh one-city civ with
		// room), which is precisely the case the old table declared already optimal.
		[Fact]
		public void MonarchyIsNotATerminus_ForACivAtPeace()
		{
			Player player = ModernCiv();

			// A civ with a city and land around it sits in Expand — the exact stance the
			// old table called optimal. A city-less player lands in Develop instead,
			// where Democracy always won anyway, which would make this test vacuous.
			Assert.Equal("Expand", player.AI!.CurrentStanceName());
			Assert.Equal("Democracy", player.AI!.BestGovernmentName());
		}

		// Under arms, Monarchy remains the right answer — the change must not make a civ
		// swap constitution in the middle of a war.
		//
		// Sharpened after a later finding: "under arms" has to mean a war being FOUGHT, not
		// merely declared. AI wars are rarely concluded, only abandoned, so testing the
		// declaration alone made Monarchy a terminus by another route — measured at 1858 in
		// a real game, Japan held Democracy as a researched advance and sat in Monarchy
		// while at war with two civs that were nowhere near it. So the guard now keys on an
		// enemy actually at the gates, and this test pins both halves.
		[Fact]
		public void MonarchyStillWins_UnderArms()
		{
			Player player = ModernCiv();
			Player rival = Game.Instance.Players.First(p => p is not null && p != player
			                                             && Game.Instance.PlayerNumber(p) != 0);
			player.DeclareWar(rival);
			City capital = player.Cities.First();

			// A rival force at the gates: the constitution holds.
			IUnit besieger = Game.Instance.CreateUnit(UnitType.Legion, capital.X + 2, capital.Y,
				Game.Instance.PlayerNumber(rival))!;
			Assert.Equal("Militarize", player.AI!.CurrentStanceName());
			Assert.Null(player.AI!.BestGovernmentName());

			// The siege lifts, the war stays on the books: the civ may modernise again.
			Game.Instance.DisbandUnit(besieger);
			foreach (IUnit stray in Game.Instance.GetUnits()
				.Where(u => u.Owner != Game.Instance.PlayerNumber(player)
				         && Common.DistanceToTile(u.X, u.Y, capital.X, capital.Y) <= 16).ToArray())
				Game.Instance.DisbandUnit(stray);
			Assert.DoesNotContain(Game.Instance.GetCities(),
				c => c.Owner != Game.Instance.PlayerNumber(player)
				  && Common.DistanceToTile(c.X, c.Y, capital.X, capital.Y) <= 8);
			Assert.True(Game.Instance.Players.Any(p => p != player && player.IsAtWar(p)),
				"still formally at war");
			Assert.Equal("Democracy", player.AI!.BestGovernmentName());
		}
	}
}
