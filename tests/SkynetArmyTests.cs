// CivOne tests
//
// The Machines' Armor "don't strike for the kill as often as they should ... I've seen them
// drift right past undefended cities" (the user, Oct 2026). Two causes were the network's own:
// its seized towns rioted like anyone's, and two in disorder held a 14-city Skynet in
// Consolidate, with no attack target; and ConsiderWar returned for the story factions before
// MarshalStrikeGroup, so an Armor that fortified was never woken again.

using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SkynetArmyTests
	{
		// The owner's capital at 30,25 is spared; its Neural Lab city at 40,25 is seized.
		private static (Game g, Player sky, City lab) AnUprising()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 25; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player owner = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			owner.Government = new Monarchy();
			g.AddCity(owner, 0, 30, 25);
			City lab = g.AddCity(owner, 1, 40, 25)!;
			lab.AddBuilding(new CivOne.Buildings.NeuralLab());
			Sim.ClearTasks();

			typeof(Game).GetMethod("ExecuteSkynetUprising", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(g, null);
			Assert.True(lab.Player.Civilization is Civilizations.Skynet, "fixture: not seized");
			lab.Player.SetLuxuriesKeepingScience(0);   // no slider doing the work
			lab.Player.Explore(35, 25, range: 10);
			Sim.ClearTasks();
			return (g, lab.Player, lab);
		}

		[Fact]
		public void ASeizedCityDoesNotRiot()
		{
			(Game _, Player _, City lab) = AnUprising();
			lab.Size = 16;
			lab.ResetResourceTiles();   // working citizens: a specialist is never unhappy

			Assert.Equal(0, lab.UnhappyCitizens);
			Assert.False(lab.IsInDisorder);
		}

		[Fact]
		public void AFortifiedArmorIsWokenForTheWar()
		{
			(Game g, Player sky, City _) = AnUprising();
			IUnit armor = g.CreateUnit(UnitType.Armor, 36, 25, g.PlayerNumber(sky))!;
			armor.Fortify = true;
			Assert.True(armor.Fortify, "fixture: not fortified");

			AI.Instance(sky).ConsiderWar();

			Assert.False(armor.Fortify, "the network's Armor slept through its war");
		}

		// ── the Registry and the Thing ───────────────────────────────────────

		private static (Game g, Player faction, City city) AFactionCity<T>() where T : Civilizations.ICivilization
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 25; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			var faction = new Player(Common.Civilizations.First(c => c is T), "Faction");
			g.AddPlayer(faction);
			faction.Government = new Communism();
			faction.TaxesRate = 5; faction.LuxuriesRate = 0;
			faction.Explore(40, 25, range: 10);
			City city = g.AddCity(faction, 9, 40, 25)!;
			city.Size = 16;
			city.ResetResourceTiles();   // working citizens: a specialist is never unhappy
			Sim.ClearTasks();
			return (g, faction, city);
		}

		[Fact]
		public void ARegistryCityDoesNotRiot()
		{
			(Game _, Player _, City city) = AFactionCity<Civilizations.TheOthers>();
			Assert.Equal(0, city.UnhappyCitizens);
		}

		[Fact]
		public void AThingCityDoesNotRiot()
		{
			(Game _, Player _, City city) = AFactionCity<Civilizations.TheThing>();
			Assert.Equal(0, city.UnhappyCitizens);
		}

		[Fact]
		public void AFortifiedRegistryArmorIsWokenForTheWar()
		{
			(Game g, Player registry, City _) = AFactionCity<Civilizations.TheOthers>();
			Player victim = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer && p != registry);
			victim.Explore(30, 25, range: 3);
			g.AddCity(victim, 0, 30, 25);
			registry.DeclareWar(victim);
			IUnit armor = g.CreateUnit(UnitType.Armor, 36, 25, g.PlayerNumber(registry))!;
			armor.Fortify = true;
			Assert.True(armor.Fortify, "fixture: not fortified");

			AI.Instance(registry).ConsiderWar();

			Assert.False(armor.Fortify, "the Registry's Armor slept through its war");
		}
	}
}
