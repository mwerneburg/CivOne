// CivOne tests
//
// The Space Race roster panel draws modules and components as grids where a COLUMN IS A SET —
// three filled boxes down a module column is one complete set, two down a component column is
// one engine. Parts arrive strictly in rotation (hab, life, solar / pod, thruster), so which
// boxes are filled is arithmetic, and getting it wrong makes the panel lie about the ship.
//
// The panel previously drew one labelled progress bar per part, so a maxed 12-module ship
// wanted 15 rows plus dividers — about 195px of a 169px panel for MODULES alone. COMPONENTS
// was jammed against the bottom edge and STRUCTURAL pushed off the screen entirely.

using CivOne.Screens.Reports;

namespace CivOne.Tests
{
	public class SpaceshipRosterTests
	{
		private const int ModCols = Game.MAX_SS_MODULE / 3;      // 4 sets
		private const int CmpCols = Game.MAX_SS_COMPONENT / 2;   // 8 engines

		// Seven modules is two complete sets plus a hab dome: 3 / 2 / 2 across the rotation.
		[Theory]
		[InlineData(0, 0, 0, 0)]
		[InlineData(1, 1, 0, 0)]
		[InlineData(3, 1, 1, 1)]
		[InlineData(7, 3, 2, 2)]
		[InlineData(12, 4, 4, 4)]
		public void ModulesFillInRotation(int total, int hab, int life, int solar)
		{
			Assert.Equal(hab,   SpaceShips.BuiltOfType(total, 0, 3, ModCols));
			Assert.Equal(life,  SpaceShips.BuiltOfType(total, 1, 3, ModCols));
			Assert.Equal(solar, SpaceShips.BuiltOfType(total, 2, 3, ModCols));
		}

		// Components alternate pod, thruster — five is three pods and two thrusters.
		[Theory]
		[InlineData(0, 0, 0)]
		[InlineData(1, 1, 0)]
		[InlineData(5, 3, 2)]
		[InlineData(16, 8, 8)]
		public void ComponentsFillInPairs(int total, int pods, int thrusters)
		{
			Assert.Equal(pods,      SpaceShips.BuiltOfType(total, 0, 2, CmpCols));
			Assert.Equal(thrusters, SpaceShips.BuiltOfType(total, 1, 2, CmpCols));
		}

		// A complete set is a complete COLUMN — that is the whole reading of the grid, so
		// every type must agree whenever the total is a multiple of the set size.
		[Fact]
		public void ACompleteSetFillsEveryTypeEqually()
		{
			for (int sets = 0; sets <= ModCols; sets++)
			{
				int total = sets * 3;
				Assert.Equal(sets, SpaceShips.BuiltOfType(total, 0, 3, ModCols));
				Assert.Equal(sets, SpaceShips.BuiltOfType(total, 1, 3, ModCols));
				Assert.Equal(sets, SpaceShips.BuiltOfType(total, 2, 3, ModCols));
			}
		}

		// Never more boxes than there are columns, and never negative — the ship counters are
		// player-level totals that a hull change can leave above the drawn ceiling.
		[Fact]
		public void TheCountIsClampedToTheGrid()
		{
			Assert.Equal(ModCols, SpaceShips.BuiltOfType(99, 0, 3, ModCols));
			Assert.Equal(ModCols, SpaceShips.BuiltOfType(99, 2, 3, ModCols));
			Assert.Equal(0, SpaceShips.BuiltOfType(0, 2, 3, ModCols));
			Assert.Equal(0, SpaceShips.BuiltOfType(0, 1, 2, CmpCols));
		}

		// The panel has to hold a MAXED ship, which is the failure that prompted the rewrite.
		// Grid rows: 3 module + 2 component + ceil(51 / 12) structural, at a 9px pitch, plus
		// three section headers with rules and the two key lines. 169px of content height.
		[Fact]
		public void AMaxedShipFitsThePanel()
		{
			const int panelH = 200 - 20 - 11;     // H - headerH - footerH
			const int pitch = 9, header = 12;     // 9px label + 3px rule

			int structuralRows = (Game.MaxSpaceshipStructural + 11) / 12;   // 12 columns wide
			int rows = 3 + 2 + structuralRows;
			int used = header * 3 + rows * pitch + 12 + 8 + 8 + 12;         // + REQUIRED + key + slack

			Assert.True(used <= panelH,
				$"a maxed ship needs {used}px of a {panelH}px panel — MODULES alone used to want ~195");
		}
	}
}
