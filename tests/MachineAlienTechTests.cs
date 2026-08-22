// CivOne tests
//
// The Machines do not study alien technology.
//
// The post-contact tree opens on a WORLD-WIDE flag — once the visitors land anywhere, every
// civilization may study alien biology and transit conduits. Skynet took that path like anyone
// else, because it was the only story faction never given a clause: the horde has no
// laboratories, the organism does not study and the Registry landed knowing everything, each
// stated outright in AI.ChooseResearch, and the Machines were simply missed.
//
// Narratively they are the one power that cannot have met the visitors: they wake at war with
// every civilization and stay that way. So what they know of alien technology is only what they
// seized along with a city — which is why this is a gate on RESEARCH and not a ban on holding
// it. Everything terrestrial is still theirs to study, an intelligence explosion being the
// entire premise of the uprising.

using System.Linq;
using CivOne.Advances;
using CivOne.Units;

namespace CivOne.Tests
{
	public class MachineAlienTechTests
	{
		// Visitors landed, and somebody who is not the Machines to compare against.
		private static (Game game, Player machines, Player human) AWorldAfterContact()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			typeof(Game).GetField("VisitorsArrived",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.SetValue(g, true);

			Player human = g.HumanPlayer;
			Player machines = g.Players.First(p => p is not null && p != human && g.PlayerNumber(p) != 0);
			MakeMachines(machines);

			// Both hold everything the alien branch stands on, so the only thing that can
			// separate them is the contact rule itself.
			foreach (IAdvance a in Common.Advances.Where(a => a is not BasePostContactAdvance))
			foreach (Player p in new[] { human, machines })
				p.AddAdvance(a, false);

			return (g, machines, human);
		}

		// Civilization is readonly and set in the constructor — the game seats the Machines by
		// building the player, not by converting one — so the field is the only handle a test
		// has. Same trick Sim uses for the Game and Map singletons.
		private static void MakeMachines(Player p) =>
			typeof(Player).GetField("_civilization",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.SetValue(p, new CivOne.Civilizations.Skynet());

		private static bool CanStudyAlienTech(Player p) =>
			p.AvailableResearch.Any(a => a is BasePostContactAdvance pc && !pc.AvailablePreContact);

		// The rule, stated directly.
		[Fact]
		public void TheMachinesCannotStudyAlienTechnology()
		{
			(Game g, Player machines, Player human) = AWorldAfterContact();

			Assert.False(CanStudyAlienTech(machines),
				"the Machines are researching the visitors' technology");
		}

		// ...and everyone else still can, or the change has broken the branch rather than
		// narrowed it.
		[Fact]
		public void EverybodyElseStillCan()
		{
			(Game g, Player machines, Player human) = AWorldAfterContact();

			Assert.True(CanStudyAlienTech(human),
				"nobody can study alien technology; the gate has closed on the whole world");
		}

		// The narrow part: terrestrial research is untouched. A network that cannot improve
		// itself is a far duller antagonist, and freezing it at whatever it seized was never
		// the intent.
		[Fact]
		public void TheMachinesStillStudyEverythingElse()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			Player machines = g.Players.First(p => p is not null && p != g.HumanPlayer && g.PlayerNumber(p) != 0);
			MakeMachines(machines);

			Assert.Contains(machines.AvailableResearch, a => a is not BasePostContactAdvance);
		}

		// Seized, not studied. The Machines take cities and what those cities knew, so alien
		// advances can still reach them — this gates the laboratory, not the loot, and that is
		// what keeps "what they know, they took from you" true.
		[Fact]
		public void ButTheyCanStillHoldWhatTheySeized()
		{
			(Game g, Player machines, Player human) = AWorldAfterContact();
			IAdvance alien = Common.Advances.First(a => a is BasePostContactAdvance pc && !pc.AvailablePreContact);

			machines.AddAdvance(alien, false);

			Assert.Contains(machines.Advances, a => a.Id == alien.Id);
		}
	}
}
