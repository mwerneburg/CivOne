// CivOne tests
//
// The upgrade ladder ran downhill.
//
// Reported from a game: "all of my knights steadily decayed into Cavalry", TypeId 7 to 6,
// which looked like the work of a cursed wonder. It was Leonardo's Workshop, working
// backwards. The chain carried
//
//     (UnitType.Knights,  UnitType.Cavalry,  new HorsebackRiding()),
//
// and Knights are 4/2/2 behind Chivalry while Cavalry are 2/1/2 behind Horseback Riding — an
// ancient advance every civilization holds. So the rung pointed the wrong way AND its gate was
// always open: one knight per turn, three with the Nanobot Factory, until the mounted arm was
// gone.
//
// The table was duplicated between the workshop and the factory, which is how one wrong rung
// became two. It is one table now, and the first test here walks every rung of it.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class UnitUpgradeChainTests
	{
		private static IUnit Blueprint(UnitType type) => Game.PeekUnit(type)!;

		// The general rule, over the whole ladder: no rung may lead to a weaker unit. This is
		// the test that would have caught the original inversion, and catches the next one
		// wherever it is added.
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void NoRungLeadsDownhill(bool nanobot)
		{
			var chain = nanobot ? Game.NanobotUpgradeChain : Game.UnitUpgradeChain;
			Assert.NotEmpty(chain);

			foreach (var (from, to, _) in chain)
			{
				IUnit a = Blueprint(from), b = Blueprint(to);
				Assert.True(b.Attack + b.Defense >= a.Attack + a.Defense,
					$"{from} ({a.Attack}/{a.Defense}) would 'upgrade' to {to} ({b.Attack}/{b.Defense})");
			}
		}

		// The reported case, named so a regression reads as itself rather than as a table row.
		[Fact]
		public void KnightsAreNeverTurnedIntoCavalry()
		{
			Assert.DoesNotContain(Game.NanobotUpgradeChain,
				r => r.from == UnitType.Knights && r.to == UnitType.Cavalry);
			Assert.DoesNotContain(Game.UnitUpgradeChain,
				r => r.from == UnitType.Knights && r.to == UnitType.Cavalry);
		}

		// ...and the ladder still goes UP at that rung: a cavalryman with Chivalry becomes a
		// knight. Deleting the rung entirely would also pass the two tests above.
		[Fact]
		public void CavalryBecomesKnightsWithChivalry()
		{
			Assert.Contains(Game.UnitUpgradeChain,
				r => r.from == UnitType.Cavalry && r.to == UnitType.Knights && r.req is Chivalry);
		}

		// End to end through the wonder itself, because a correct table wired to nothing is
		// still a decaying army.
		[Fact]
		public void TheWorkshopUpgradesCavalryAndLeavesKnightsAlone()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0 && q != g.HumanPlayer);
			p.Government = new CivOne.Governments.Monarchy();
			foreach (IAdvance a in new IAdvance[] { new HorsebackRiding(), new Chivalry() })
				p.AddAdvance(a, false);
			City c = g.AddCity(p, 0, 30, 20)!;
			c.AddWonder(new CivOne.Wonders.LeonardosWorkshop());

			g.CreateUnit(UnitType.Cavalry, 30, 20, g.PlayerNumber(p), false);
			g.CreateUnit(UnitType.Knights, 31, 20, g.PlayerNumber(p), false);
			Sim.ClearTasks();

			var upgrade = typeof(Game).GetMethod("ApplyLeonardoUpgrade",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			for (int i = 0; i < 5; i++) upgrade.Invoke(g, new object[] { p });

			Assert.DoesNotContain(g.GetUnits(), u => u.Owner == g.PlayerNumber(p) && u is Cavalry);
			Assert.True(g.GetUnits().Count(u => u.Owner == g.PlayerNumber(p) && u is Knights) >= 1,
				"the knights are gone; something is still marching the ladder downwards");
		}
	}
}
