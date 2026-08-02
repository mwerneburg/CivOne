// CivOne tests
//
// A recent epic game left the entire New World unoccupied. The cause was not
// motivation, it was reachability: AI.cs only looked for a boat when
// BestSettleSite AND BestImproveSite both came back empty, and BestImproveSite
// wants any unimproved tile within 6 of a city. Games finish at 42-45% improved
// land, so it was never empty and no settler ever boarded anything.
//
// Colonists are now designated ahead of local gardening, the hull rule no longer
// closes permanently once the map is charted, and the port sends a defender along.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ColonistCallUpTests
	{
		// A civ with cities, a settler, and a transport in port — plus plenty of
		// unimproved ground, which is exactly what used to keep the settler at home.
		private static (Player p, IUnit settler, IUnit hull, ITile berth) Port(int cities = 5)
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 44; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			// Open water east of the coast...
			for (int y = 18; y <= 32; y++)
			for (int x = 45; x <= 51; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			// ...and a New World on the far side of it, which is the whole point.
			for (int y = 22; y <= 28; y++)
			for (int x = 52; x <= 58; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = Game.Instance.Players.First(x => Game.Instance.PlayerNumber(x) != 0);
			byte id = Game.Instance.PlayerNumber(p);
			p.Explore(42, 25, range: 10);
			p.Explore(55, 25, range: 6);   // charted, so the register has somewhere to record
			for (int i = 0; i < cities; i++)
				Game.Instance.AddCity(p, (byte)i, 44 - i * 2, 25 - (i % 2));

			IUnit hull = Game.Instance.CreateUnit(UnitType.Trireme, 45, 25, id)!;
			IUnit settler = Game.Instance.CreateUnit(UnitType.Settlers, 44, 25, id)!;
			Sim.ClearTasks();
			return (p, settler, hull, Map.Instance[45, 25]);
		}

		// Without a charted site there is nowhere to go, and nothing should change.
		[Fact]
		public void WithNowhereCharted_NoColonistIsCalledUp()
		{
			var (p, _, _, _) = Port();
			Assert.False(AI.Instance(p).WantsColonist());
		}

		// A young civ does not ship out its citizens.
		[Fact]
		public void AYoungCiv_DoesNotColonise()
		{
			var (p, _, _, _) = Port(cities: 2);
			Assert.False(AI.Instance(p).WantsColonist());
		}

		// The finding: with a hull, a charted site and unimproved land everywhere, the
		// settler heads for the boat instead of staying home to irrigate.
		[Fact]
		public void ADesignatedColonist_WalksToTheBoat()
		{
			var (p, settler, hull, berth) = Port();
			AI ai = AI.Instance(p);
			// Survey: this is what fills the colony register in a real game.
			ai.BestOverseasSite(hull);
			Assert.True(ai.KnownColonySites() > 0, "scenario is broken: no overseas site was surveyed");

			ai.Move(settler);

			Assert.False(settler.Goto.IsEmpty, "a designated colonist should be routed somewhere");
			Assert.True(settler.Goto.X == berth.X && settler.Goto.Y == berth.Y,
				$"expected the berth at ({berth.X},{berth.Y}); got ({settler.Goto.X},{settler.Goto.Y})");
		}

		// The port's gift, and the two things that must bound it.
		[Fact]
		public void TheEscortSailsWithTheColonist_AndCostsTheColonyNothing()
		{
			var (p, settler, hull, berth) = Port();
			AI ai = AI.Instance(p);
			ai.BestOverseasSite(hull);
			Assert.True(ai.KnownColonySites() > 0, "scenario is broken: no overseas site was surveyed");

			ai.Move(settler);

			IUnit[] escorts = berth.Units.Where(u => u.Class == UnitClass.Land).ToArray();
			Assert.True(escorts.Length >= 1, "the port should have put a defender aboard");
			Assert.All(escorts, e => Assert.Null(e.Home));
			Assert.All(escorts, e => Assert.True(e.Sentry, "an escort aboard must be sentried as cargo"));
		}

		// The cap counts settlers still WALKING to the port, not only those aboard.
		// Without that, an epic-map civ marches half its settlers at one hull and stops
		// improving its land — this change, inverted.
		[Fact]
		public void SettlersAlreadyWalkingToPort_CountAgainstTheCap()
		{
			var (p, settler, hull, berth) = Port();
			AI ai = AI.Instance(p);
			ai.BestOverseasSite(hull);
			Assert.True(ai.WantsColonist());

			byte id = Game.Instance.PlayerNumber(p);
			for (int i = 0; i < 2; i++)
			{
				IUnit walker = Game.Instance.CreateUnit(UnitType.Settlers, 36 + i, 22, id)!;
				walker.Goto = new System.Drawing.Point(berth.X, berth.Y);
			}

			Assert.False(ai.WantsColonist(),
				"two settlers already marching at the boat is a full quota");
		}

		// Only one escort per crossing, however many times the settler re-decides.
		[Fact]
		public void TheEscort_IsNotDonatedTwice()
		{
			var (p, settler, hull, berth) = Port();
			AI ai = AI.Instance(p);
			ai.BestOverseasSite(hull);
			Assert.True(ai.KnownColonySites() > 0, "scenario is broken: no overseas site was surveyed");

			ai.Move(settler);
			int after = berth.Units.Count(u => u.Class == UnitClass.Land);
			ai.Move(settler);

			Assert.Equal(after, berth.Units.Count(u => u.Class == UnitClass.Land));
		}
	}
}
