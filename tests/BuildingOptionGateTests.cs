// CivOne tests
//
// The Options menu switches that retire whole buildings.
//
// Circuses has always gated the Colosseum and Barricades the City Walls. Two gaps were left:
// the SAM Battery is the air-defence half of the same fortification choice and was offered
// whatever Barricades said, and the Aqueduct / Sewer System chain had no switch at all.
//
// Both gates read a Game flag that round-trips through the save. The load default is TRUE in
// each case: a game written before an option existed had those buildings, and opening it must
// not retire what its cities already hold.

using System.Linq;
using CivOne.Buildings;

namespace CivOne.Tests
{
	public class BuildingOptionGateTests
	{
		private static Player APlayerWithEveryAdvance()
		{
			Sim.NewGame(width: 80, height: 50);
			Player me = Game.Instance.HumanPlayer;
			// The gates sit ahead of the tech test, but a building whose tech is missing is
			// unavailable for that reason instead and the assertions would prove nothing.
			foreach (var a in Common.Advances) me.AddAdvance(a, false);
			Sim.ClearTasks();
			return me;
		}

		private static bool Offered<T>(Player p) where T : IBuilding, new() =>
			p.ProductionAvailable(new T());

		// Barricades now covers the SAM Battery as well as the City Walls.
		[Fact]
		public void BarricadesGatesTheSamBatteryWithTheWalls()
		{
			Player me = APlayerWithEveryAdvance();

			Game.Instance.Barricades = true;
			Assert.True(Offered<CityWalls>(me),  "fixture: walls are unavailable for some other reason");
			Assert.True(Offered<SamBattery>(me), "fixture: the SAM is unavailable for some other reason");

			Game.Instance.Barricades = false;
			Assert.False(Offered<CityWalls>(me));
			Assert.False(Offered<SamBattery>(me));
		}

		// The new switch, covering the whole size-cap plumbing chain.
		[Fact]
		public void AqueductsGatesTheAqueductAndTheSewerSystem()
		{
			Player me = APlayerWithEveryAdvance();

			Game.Instance.Aqueducts = true;
			Assert.True(Offered<Aqueduct>(me),     "fixture: the aqueduct is unavailable for some other reason");
			Assert.True(Offered<SewerSystem>(me),  "fixture: the sewer is unavailable for some other reason");

			Game.Instance.Aqueducts = false;
			Assert.False(Offered<Aqueduct>(me));
			Assert.False(Offered<SewerSystem>(me));
		}

		// Each switch minds its own buildings. Without this, a gate widened by accident — the
		// exact mistake this change could have made — reads as a pass everywhere else.
		[Fact]
		public void NeitherSwitchTouchesTheOthersBuildings()
		{
			Player me = APlayerWithEveryAdvance();

			Game.Instance.Barricades = false;
			Game.Instance.Aqueducts  = true;
			Assert.True(Offered<Aqueduct>(me),    "Barricades retired the aqueduct");
			Assert.True(Offered<SewerSystem>(me), "Barricades retired the sewer");

			Game.Instance.Barricades = true;
			Game.Instance.Aqueducts  = false;
			Assert.True(Offered<CityWalls>(me),  "Aqueducts retired the walls");
			Assert.True(Offered<SamBattery>(me), "Aqueducts retired the SAM");
			// ...and a building neither switch owns is never affected.
			Assert.True(Offered<Barracks>(me), "an unrelated building was gated");
		}

		// The switch has to survive a save. Circuses and Barricades are persisted as nullable
		// bools precisely so an older file can be told apart from one that says "off"; the new
		// flag follows that, and an absent value means ON.
		[Fact]
		public void TheAqueductSwitchRoundTripsThroughASave()
		{
			Sim.NewGame(width: 80, height: 50);
			Game.Instance.Aqueducts = false;
			string path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(), $"civone-aqueduct-{System.Guid.NewGuid():N}.cos");
			try
			{
				Game.Instance.SaveCos(path);
				Sim.ResetState();
				Assert.True(Game.LoadCos(path), "the fixture could not read its own save");

				Assert.False(Game.Instance.Aqueducts, "the switch came back on");
			}
			finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
		}

		// A save written before the option existed carries no value at all, and must load as
		// ON — the alternative silently retires buildings a player's cities already hold.
		[Fact]
		public void AnOlderSaveWithoutTheOptionLoadsItOn()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(Sim.RepoRoot(), "src", "Game.Cos.cs"));

			Assert.Contains("Aqueducts      = opt.Aqueducts  ?? true;", src);
		}

		// The panel sizes itself from the number of entries, and the screen is 200px tall.
		// Adding a fifteenth row is safe only while the arithmetic still fits — and the font
		// height is loaded data, so this cannot be reasoned about from the source alone.
		[Fact]
		public void TheOptionsPanelStillFitsOnTheScreen()
		{
			Sim.EnsureRuntime();
			int fh = Graphics.Resources.Instance.GetFontHeight(0);

			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "Screens", "GameOptions.cs"));
			// One toggle lambda per row, which is the only part of an entry that is spelled
			// identically whether it reads Game or Settings.
			int entries = src.Split(new[] { "() => {" }, System.StringSplitOptions.None).Length - 1;
			Assert.True(entries >= 15, $"expected at least 15 options, counted {entries}");

			int panelH = (fh + 8) + (entries * (fh + 2) + 4) + (fh + 6);
			Assert.True(panelH <= 200,
				$"the options panel is {panelH}px tall on a 200px screen with {entries} entries");
		}
	}
}
