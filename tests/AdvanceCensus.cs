using System;
using System.Linq;
using Xunit.Abstractions;
namespace CivOne.Tests
{
	public class AdvanceCensus
	{
		private readonly ITestOutputHelper _out;
		public AdvanceCensus(ITestOutputHelper o) => _out = o;
		[Fact]
		public void WhoIsResearching()
		{
			string path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE") ?? "";
			if (!System.IO.File.Exists(path)) { _out.WriteLine("skipped"); return; }
			Sim.EnsureRuntime(); Sim.ResetState();
			Assert.True(Game.LoadCos(path));
			Game g = Game.Instance;
			_out.WriteLine($"turn {g.GameTurn}");
			foreach (Player p in g.Players.Where(q => q is not null && g.PlayerNumber(q) != 0 && !q.IsDestroyed()))
				_out.WriteLine($"  {p.TribeNamePlural,-14} human={(p == g.HumanPlayer),-5} cities={p.Cities.Length,3} "
					+ $"advances={p.Advances.Length,3} gov={p.Government.GetType().Name,-10} "
					+ $"tax={p.TaxesRate} lux={p.LuxuriesRate} sci={10 - p.TaxesRate - p.LuxuriesRate} "
					+ $"research={(p.CurrentResearch is null ? "NOTHING" : p.CurrentResearch.Name)} "
					+ $"distinct={p.Advances.Select(a => a.Id).Distinct().Count()}");
		}
	}
}
