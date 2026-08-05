// CivOne tests
//
// The Registry was observed completing a DOME COMPONENT around 1900 AD — the occupation
// finishing humanity's defence against the occupation. Its AI production block never chooses
// a wonder, and ExecuteOwnersLanding clears the queue of every city it seizes at the landing.
// The gap was ordinary CONQUEST: BaseUnit's changeOwner zeroes Shields and leaves the
// production queue standing, so a captured city carries on building whatever it held.
//
// Two guards, because either alone is insufficient. WonderAvailable stops a story faction
// ever CHOOSING a wonder — but it only filters the choice list, so it cannot stop a wonder
// already set as CurrentProduction from completing. Clearing the queue on capture handles
// that half.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class StoryFactionWonderTests
	{
		// Sim.NewGame FIRST: Common's static initializer needs the resource layer up, and
		// resolving the civilization as an argument would run it too early.
		private static (Game, Player) AWorldWith<T>() where T : ICivilization
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = new Player(Common.Civilizations.First(c => c is T));
			g.AddPlayer(p);
			foreach (IAdvance a in Common.Advances) p.AddAdvance(a, false);
			return (g, p);
		}

		// The defect, stated directly: the occupation may not build the Dome.
		[Fact]
		public void TheRegistryCannotBuildADomeComponent()
		{
			(Game g, Player registry) = AWorldWith<TheOthers>();

			Assert.False(registry.ProductionAvailable(new DomeSensorNet()));
			Assert.False(registry.ProductionAvailable(new DomeKineticRing()));
			Assert.False(registry.ProductionAvailable(new DomeCommandHub()));
		}

		// ...nor anything else. They did not come here to develop the place.
		[Fact]
		public void NoStoryFactionBuildsOrdinaryWonders()
		{
			Check(AWorldWith<TheOthers>());
			Check(AWorldWith<TheThing>());
			Check(AWorldWith<Skynet>());

			static void Check((Game g, Player p) world)
			{
				IWonder[] allowed = Reflect.GetWonders()
					.Where(w => world.p.ProductionAvailable(w))
					.ToArray();
				// The Vessel is the one exception, and only the organism gets it.
				Assert.All(allowed, w => Assert.True(w is TheVessel,
					$"{world.p.Civilization.Name} may build {(w as ICivilopedia)?.Name}"));
			}
		}

		// Ordinary civs are untouched — the gate is on the faction, not on wonders.
		[Fact]
		public void OrdinaryCivsStillBuildWonders()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			foreach (IAdvance a in Common.Advances) human.AddAdvance(a, false);

			Assert.True(Reflect.GetWonders().Any(w => human.ProductionAvailable(w)),
				"a fully-teched civ must still have wonders available");
		}
	}
}
