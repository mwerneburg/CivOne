// CivOne tests
//
// Diagnostic: what is actually painted on the map, by count.
//
// Written because a screenshot full of three-amber-blob icons was read as placer gold, and
// two different sprites answer that description — Free.Special(Terrain.River) draws three
// outlined nuggets in a diagonal cascade, and OlvirSprites.GetSettlementCluster draws three
// unoutlined amber domes in a triangle. Counting them apart is the only way to tell a rich
// river valley from an Olvir landing.
//
// Opt-in; skips silently when no save is given.
//     CIVONE_ENDGAME_SAVE=/path/to/save.cos dotnet test \
//       --filter "FullyQualifiedName~MapSpecials" --logger "console;verbosity=detailed"

using System;
using System.Linq;
using CivOne.Tiles;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class MapSpecialsDiagnostic
	{
		private readonly ITestOutputHelper _out;
		public MapSpecialsDiagnostic(ITestOutputHelper output) => _out = output;

		private static string? SavePath()
		{
			string? env = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrWhiteSpace(env)) return null;
			if (env.StartsWith("~"))
				env = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + env.Substring(1);
			return System.IO.File.Exists(env) ? env : null;
		}

		[Trait("Category", "Diagnostic")]
		[Fact]
		public void WhatIsPaintedOnTheMap()
		{
			string? path = SavePath();
			if (path is null) { _out.WriteLine("no save given — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), $"load failed: {path}");
			Game g = Game.Instance;

			ITile[] all = Map.Instance.AllTiles().Where(t => t is not null).ToArray();
			int land = all.Count(t => !t.IsOcean);
			River[] rivers = all.OfType<River>().ToArray();
			int goldRivers = rivers.Count(r => r.Gold);

			_out.WriteLine($"save: {path}");
			_out.WriteLine($"turn {g.GameTurn} ({Common.YearString((ushort)g.GameTurn)})  "
			             + $"map {Map.WIDTH}x{Map.HEIGHT} = {all.Length} tiles, {land} land");
			_out.WriteLine("");
			_out.WriteLine($"rivers          {rivers.Length}");
			_out.WriteLine($"  of which gold {goldRivers}  ({(rivers.Length > 0 ? goldRivers * 100.0 / rivers.Length : 0):F1}% "
			             + "— the 1-in-16 lattice predicts 6.25%)");
			_out.WriteLine("");
			_out.WriteLine($"Olvir improvements {g.OlvirImprovements.Count} total:");
			foreach (var grp in g.OlvirImprovements.GroupBy(kv => kv.Value).OrderByDescending(x => x.Count()))
				_out.WriteLine($"  {grp.Key,-20} {grp.Count()}");

			// The two confusable sprites, side by side. Whichever dominates is what the
			// screenshot was showing.
			int clusters = g.OlvirImprovements.Count(kv => kv.Value == Enums.OlvirImprovementType.SettlementCluster);
			_out.WriteLine("");
			_out.WriteLine($"THREE-AMBER-BLOB SPRITES: {goldRivers} gold rivers vs {clusters} settlement clusters");

			// Strategic resource camps also paint (mine + fortress), and were in the shot.
			_out.WriteLine($"resource camps {g.ResourceCamps.Count}");

			// Transport tubes were meant to be water-only. Are they?
			ITile[] tubes = all.Where(t => t.TransportTube).ToArray();
			_out.WriteLine("");
			_out.WriteLine($"transport tubes {tubes.Length}: "
			             + $"{tubes.Count(t => t.IsOcean)} on water, {tubes.Count(t => !t.IsOcean)} on LAND");
			foreach (var grp in tubes.Where(t => !t.IsOcean).GroupBy(t => t.Type)
			                         .OrderByDescending(x => x.Count()))
				_out.WriteLine($"  land tube on {grp.Key,-14} {grp.Count()}");
			// Rough land is where the movement rule bites: entering railed/tubed terrain with
			// Movement > 1 from an unconnected tile rolls the last-move-point dice.
			_out.WriteLine($"  of the land tubes, {tubes.Count(t => !t.IsOcean && t.Movement > 1)} sit on rough terrain");
			_out.WriteLine($"railroaded rough-terrain tiles {all.Count(t => t.RailRoad && t.Movement > 1)}");

			// Only WORKED tiles reach the economy, so this is the number that matters for any
			// change to what a tube is worth.
			var worked = new HashSet<(int, int)>();
			foreach (City c in g.GetCities().Where(c => c.Size > 0))
			foreach (ITile rt in c.ResourceTiles)
				worked.Add((rt.X, rt.Y));
			_out.WriteLine($"  tubes actually WORKED by a city: {tubes.Count(t => worked.Contains((t.X, t.Y)))}"
			             + $" of {tubes.Length}");
		}
	}
}
