// CivOne tests
//
// A Democracy could be dismantled building by building with no constitutional answer.
// An incited city convened the Senate (IncitedCityResponse); sabotage got only a spy
// report, and BaseUnit.Confront went on blocking every retaliation with "The Senate has
// blocked your attack!" — a diplomat war the victim was forbidden to fight back in.
//
// Three hostile acts now convene the Senate, and thereafter that civ is no longer
// shielded by the veto.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SenateGrievanceTests
	{
		private static (Player human, Player culprit) TwoPowers()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player human = Game.Instance.HumanPlayer;
			Player culprit = Game.Instance.Players
				.First(p => p is not null && Game.Instance.PlayerNumber(p) != 0 && p != human);
			human.Government = new CivOne.Governments.Democracy();
			Sim.ClearTasks();
			return (human, culprit);
		}

		// The tally, and the exact turn the Senate convenes: on the third act, once.
		[Fact]
		public void TheSenateConvenes_OnTheThirdActAndOnlyThen()
		{
			var (_, culprit) = TwoPowers();
			Game g = Game.Instance;

			Assert.False(g.RecordProvocation(culprit), "one act is an incident");
			Assert.False(g.RecordProvocation(culprit), "two is still not a campaign");
			Assert.True(g.RecordProvocation(culprit), "the third convenes the Senate");
			Assert.False(g.RecordProvocation(culprit), "and it does not convene again");
		}

		// Crossing the threshold is what lifts the veto.
		[Fact]
		public void BelowTheThreshold_TheCivIsStillShielded()
		{
			var (_, culprit) = TwoPowers();
			Game g = Game.Instance;
			byte num = g.PlayerNumber(culprit);

			g.RecordProvocation(culprit);
			g.RecordProvocation(culprit);

			Assert.False(g.IsProvocateur(num));
		}

		[Fact]
		public void AtTheThreshold_TheVetoLifts()
		{
			var (_, culprit) = TwoPowers();
			Game g = Game.Instance;
			byte num = g.PlayerNumber(culprit);

			for (int i = 0; i < Game.ProvocationThreshold; i++) g.RecordProvocation(culprit);

			Assert.True(g.IsProvocateur(num));
		}

		// The human's own acts are not grievances against the human.
		[Fact]
		public void TheHumansOwnActs_AreNotCounted()
		{
			var (human, _) = TwoPowers();

			Assert.False(Game.Instance.RecordProvocation(human));
			Assert.False(Game.Instance.IsProvocateur(Game.Instance.PlayerNumber(human)));
		}

		// The tally has to survive a save, or a days-long game forgets every grievance
		// each time it is reloaded — which is most of the game.
		[Fact]
		public void TheTally_SurvivesASaveAndReload()
		{
			var (_, culprit) = TwoPowers();
			Game g = Game.Instance;
			byte num = g.PlayerNumber(culprit);
			for (int i = 0; i < Game.ProvocationThreshold; i++) g.RecordProvocation(culprit);

			string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
				$"grievance_{System.Guid.NewGuid():N}.cos");
			Game.Instance.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "reload should succeed");

			Assert.True(Game.Instance.IsProvocateur(num),
				"a grievance forgotten on reload is a grievance that never bites");
			System.IO.File.Delete(path);
		}
	}
}
