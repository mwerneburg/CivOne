// CivOne tests
//
// Guards the COS save layer and the city-coordinate fix. These run against the
// real simulation singletons, so the whole assembly runs single-threaded (see
// the CollectionBehavior attribute below).

using System.IO;
using CivOne;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CivOne.Tests
{
	public class SaveRoundTripTests
	{
		// The keystone: a save → load → save cycle must reproduce the file exactly.
		// Any field that doesn't survive deserialization (the advance-origin scramble,
		// Apollo visibility baking, the city-coordinate truncation) shows up here as a
		// diff between the two saves.
		[Fact]
		public void NewGame_SaveLoadSave_IsByteIdentical()
		{
			Sim.NewGame(width: 80, height: 50);
			string saves = Settings.Instance.SavesDirectory;
			string first  = Path.Combine(saves, "roundtrip_a.cos");
			string second = Path.Combine(saves, "roundtrip_b.cos");

			Game.Instance.SaveCos(first);
			Sim.ResetState();                       // LoadCos refuses if an instance exists
			Assert.True(Game.LoadCos(first), "LoadCos should succeed");
			Game.Instance.SaveCos(second);

			Assert.Equal(File.ReadAllText(first), File.ReadAllText(second));
		}

		// Regression for the byte-coordinate bug: a city east of x=255 on an Epic-width
		// map kept its real X (309), not the byte-wrapped 53, through a save/load cycle.
		[Fact]
		public void City_EastOf255_SurvivesSaveLoad()
		{
			// Width must exceed 309 for the bug to bite (x past a byte's 255); height is
			// kept small to keep map generation fast.
			Sim.NewGame(width: 320, height: 60);

			var city = Game.Instance.AddCity(Game.Instance.HumanPlayer, 0, 309, 30);
			Assert.NotNull(city);
			Assert.Equal(309, city.X);

			string path = Path.Combine(Settings.Instance.SavesDirectory, "coord.cos");
			Game.Instance.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "LoadCos should succeed");

			var loaded = Game.Instance.GetCity(309, 30);
			Assert.NotNull(loaded);
			Assert.Equal(309, loaded.X);
		}
	}
}
