// CivOne tests
//
// The city picker offered the thing the city was already building.
//
// Choosing it does nothing; queueing it schedules a repeat of the build in progress. Neither
// is ever what the click meant, and for a building or a wonder the repeat is not even legal —
// one per city.
//
// Military units are the exception. "Another Musketeers after this one" is an ordinary order
// and the reason the queue exists. The line is whether the thing can fight (Attack > 0) rather
// than a list of civilian types, so a new unit lands on the right side of it without being
// registered anywhere. That puts Transports and the Hydro Engineer with the Settlers and
// Caravans, since they are unarmed.

using System.Linq;
using CivOne.Enums;
using CivOne.Screens;
using CivOne.Buildings;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CurrentProductionHiddenTests
	{
		private static (Game game, City city) ACity()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 44; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			// Coast on one side so the sea units are offered at all.
			for (int y = 20; y <= 30; y++)
				Map.Instance.ChangeTileType(45, y, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.HumanPlayer;
			p.Explore(38, 25, range: 20);
			City c = g.AddCity(p, 0, 38, 25)!;
			c.Size = 8;
			Sim.ClearTasks();
			return (g, c);
		}

		private static IProduction Item<T>() where T : IProduction, new() => new T();

		// A building under construction is not offered again.
		[Fact]
		public void ABuildingBeingBuiltIsHidden()
		{
			(Game g, City c) = ACity();
			c.SetProduction(Item<CivOne.Buildings.Barracks>());

			Assert.True(CityChooseProduction.HiddenAsCurrent(c, Item<CivOne.Buildings.Barracks>()));
		}

		// ...and a different building still is.
		[Fact]
		public void AnotherBuildingIsStillOffered()
		{
			(Game g, City c) = ACity();
			c.SetProduction(Item<CivOne.Buildings.Barracks>());

			Assert.False(CityChooseProduction.HiddenAsCurrent(c, Item<CivOne.Buildings.Temple>()));
		}

		// A wonder in progress, same rule.
		[Fact]
		public void AWonderBeingBuiltIsHidden()
		{
			(Game g, City c) = ACity();
			c.SetProduction(Item<CivOne.Wonders.Pyramids>());

			Assert.True(CityChooseProduction.HiddenAsCurrent(c, Item<CivOne.Wonders.Pyramids>()));
		}

		// The exception, stated as the user did: a military unit stays on the list so a second
		// one can be ordered behind the first.
		[Theory]
		[InlineData(typeof(Militia))]
		[InlineData(typeof(Musketeers))]
		[InlineData(typeof(Catapult))]
		[InlineData(typeof(Trireme))]
		public void AMilitaryUnitBeingBuiltIsStillOffered(System.Type type)
		{
			(Game g, City c) = ACity();
			IUnit unit = Game.PeekUnit((UnitType)System.Enum.Parse(typeof(UnitType), type.Name))!;
			c.SetProduction(unit);

			Assert.True(unit.Attack > 0, $"{type.Name} is the wrong fixture for this rule");
			Assert.False(CityChooseProduction.HiddenAsCurrent(c, Game.PeekUnit(unit.Type)!));
		}

		// Unarmed units are not the exception: a Settler, a Caravan, a Diplomat and an unarmed
		// hull are all one-at-a-time as far as this list is concerned.
		[Theory]
		[InlineData(UnitType.Settlers)]
		[InlineData(UnitType.Caravan)]
		[InlineData(UnitType.Diplomat)]
		[InlineData(UnitType.Transport)]
		public void AnUnarmedUnitBeingBuiltIsHidden(UnitType type)
		{
			(Game g, City c) = ACity();
			IUnit unit = Game.PeekUnit(type)!;
			c.SetProduction(unit);

			Assert.Equal(0, unit.Attack);
			Assert.True(CityChooseProduction.HiddenAsCurrent(c, Game.PeekUnit(type)!));
		}

		// A city building nothing in particular hides nothing — the rule must not empty the
		// list on a fresh city.
		[Fact]
		public void NothingIsHiddenWhenNothingIsBeingBuilt()
		{
			(Game g, City c) = ACity();
			var probe = new City(0);   // CurrentProduction is null on a bare city

			Assert.False(CityChooseProduction.HiddenAsCurrent(probe, Item<CivOne.Buildings.Temple>()));
		}

		// End to end against the real list: the current build disappears from what the picker
		// would show, and everything else survives.
		[Fact]
		public void ThePickerListLosesExactlyTheCurrentBuild()
		{
			(Game g, City c) = ACity();
			IProduction[] before = c.AvailableProduction.ToArray();
			// Whatever building this player can actually build at this point — naming one
			// couples the test to the tech tree, and the first draft picked a Temple the
			// starting civilization had no Ceremonial Burial for.
			IProduction building = before.First(x => x is IBuilding);
			c.SetProduction(building);

			IProduction[] after = before.Where(x => !CityChooseProduction.HiddenAsCurrent(c, x)).ToArray();

			Assert.Equal(before.Length - 1, after.Length);
			Assert.DoesNotContain(after, x => x.GetType() == building.GetType());
		}

		// ...and with a military unit in progress the list is untouched.
		[Fact]
		public void ThePickerListIsUnchangedForAMilitaryBuild()
		{
			(Game g, City c) = ACity();
			IProduction[] before = c.AvailableProduction.ToArray();
			c.SetProduction(before.First(x => x is IUnit u && u.Attack > 0));

			IProduction[] after = before.Where(x => !CityChooseProduction.HiddenAsCurrent(c, x)).ToArray();

			Assert.Equal(before.Length, after.Length);
		}

		// The screen must actually apply it. The list is built in a private property behind a
		// screen that needs a live display, so this is pinned at the source.
		[Fact]
		public void TheScreenAppliesTheFilter()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(Sim.RepoRoot(),
				"src", "Screens", "CityChooseProduction.cs"));
			int at = src.IndexOf("private IProduction[] Filtered");
			Assert.True(at > 0, "the picker's list property has moved");
			string block = src.Substring(at, src.IndexOf("// ─── layout", at) - at);

			Assert.Contains("HiddenAsCurrent(_city, x)", block);
		}
	}
}
