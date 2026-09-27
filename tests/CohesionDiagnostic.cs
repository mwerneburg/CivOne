using System; using System.Linq; using Xunit.Abstractions;
namespace CivOne.Tests { public class CohesionDiagnostic {
	private readonly ITestOutputHelper _o; public CohesionDiagnostic(ITestOutputHelper o) => _o = o;
	[Fact] public void Measure() {
		if (Environment.GetEnvironmentVariable("CIVONE_COHESION_RUN") != "1") return;
		foreach (short seed in new short[] { 101, 202, 303 })
		{
			Sim.NewGame(width: 80, height: 50, competition: 7, difficulty: 2, seed: seed, varyHuman: true);
			Settings.Instance.Autopilot = true; Settings.Instance.CursedWonders = false;
			Game g = Game.Instance;
			var line = new System.Text.StringBuilder($"seed {seed}:");
			foreach (int target in new[] { 150, 250, 350 })
			{
				Sim.RunTurns(target - g.GameTurn);
				int emb = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && !p.IsDestroyed()).Sum(p => p.Embassies.Length);
				int civs = g.Players.Count(p => p is not null && g.PlayerNumber(p) != 0 && !p.IsDestroyed());
				int wars = 0; var ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && !p.IsDestroyed()).ToArray();
				for (int i = 0; i < ps.Length; i++) for (int j = i + 1; j < ps.Length; j++) if (ps[i].IsAtWar(ps[j])) wars++;
				line.Append($"  t{g.GameTurn}: civs {civs} embassies {emb} wars {wars} cohesion {g.Cohesion():F2}");
			}
			_o.WriteLine(line.ToString());
		}
		Settings.Instance.Autopilot = false;
	} } }
