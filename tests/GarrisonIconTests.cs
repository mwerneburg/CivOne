// CivOne tests
//
// The city screen's garrison panel used to draw from its own art: 32x32 PNGs in
// garrison_icons/, then a hand-coded 16x16 table (CustomUnitIcons) covering eighteen
// unit types, and only then the sprite the map uses. Two icon sets for the same unit,
// side by side in the same session, and the disagreement read as a bug.
//
// Nothing about that degrades loudly — a reintroduced override just quietly wins again —
// so this pins the panel to the map sprite.

using CivOne.IO;
using CivOne.Screens;
using CivOne.Units;

namespace CivOne.Tests
{
	public class GarrisonIconTests
	{
		public GarrisonIconTests() => Sim.NewGame(width: 80, height: 50);

		// Militia is the type that actually ships a garrison_icons/Militia.png, so it is the
		// case where the old override path won outright.
		[Theory]
		[InlineData(typeof(Militia))]
		[InlineData(typeof(Phalanx))]
		[InlineData(typeof(Settlers))]
		[InlineData(typeof(Armor))]
		[InlineData(typeof(Battleship))]
		public void TheGarrisonDrawsTheMapSpriteDoubled(System.Type unitType)
		{
			IUnit unit = (IUnit)System.Activator.CreateInstance(unitType)!;

			Bytemap garrison = CityManager.GarrisonIcon(unit);
			Bytemap map = unit.ToBitmap();

			Assert.Equal(32, garrison.Width);
			Assert.Equal(32, garrison.Height);
			for (int y = 0; y < 32; y++)
			for (int x = 0; x < 32; x++)
				Assert.True(garrison[x, y] == map[x / 2, y / 2],
					$"{unitType.Name}: garrison pixel ({x},{y}) is {garrison[x, y]}, map sprite has {map[x / 2, y / 2]}");
		}
	}
}
