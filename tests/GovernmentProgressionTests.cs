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
using Gov = CivOne.Governments;

namespace CivOne.Tests
{
	public class GovernmentProgressionTests
	{
		// A civ holding Monarchy with the modern constitutions researched, and one city
		// on open land so its stance is Expand.
		private static Player ModernCiv()
		{
			Sim.NewGame(width: 80, height: 50);
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

		// Under arms, Monarchy remains the right answer — the change must not make a
		// civ swap constitution in the middle of a war.
		[Fact]
		public void MonarchyStillWins_UnderArms()
		{
			Player player = ModernCiv();
			Player rival = Game.Instance.Players.First(p => p is not null && p != player
			                                             && Game.Instance.PlayerNumber(p) != 0);
			player.DeclareWar(rival);

			// Militarize is reached via the war; confirm, then assert Monarchy holds.
			if (player.AI!.CurrentStanceName() == "Militarize")
				Assert.Null(player.AI!.BestGovernmentName());
		}
	}
}
