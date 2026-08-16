// CivOne tests
//
// The safety net for moving per-player state into a PlayerProgress object.
//
// Eleven parallel arrays live on Game — five spaceship counters, five victory-progress
// fields, and the war-initiation record — each of which must be sized in TWO places:
// AddPlayer for a live game, and the Game(CosFile) constructor for the load path, which
// never calls AddPlayer. Missing the second made LoadCos return false for EVERY save in the
// suite, and no targeted test could see it because none of them load a save.
//
// So before any of that moves, this pins the format: a save written by the pre-refactor
// build must still load afterwards with all eleven values intact. Without it, a format
// change would silently strand every save on disk — including the one from whichever game
// happens to be running at the time.

using System.IO;
using System.Linq;

namespace CivOne.Tests
{
	public class ProgressSaveCompatTests
	{
		private const string Fixture = "progress-format.cos";

		private static string FixturePath =>
			Path.Combine(System.AppContext.BaseDirectory, "fixtures", Fixture);

		// The exact values the fixture carries, so the writer and the reader cannot drift.
		private static readonly (int launch, int arrival, int str, int cmp, int mod,
		                         uint econ, uint cult, bool colony, uint dias, int order) Expected
			= (120, 260, 51, 16, 12, 9u, 4u, true, 6u, 2);

		private static (Game g, byte slot) AGameWithProgress()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0 && x != g.HumanPlayer);
			byte n = g.PlayerNumber(p);

			g.SpaceshipLaunchTurn[n]  = Expected.launch;
			g.SpaceshipArrivalTurn[n] = Expected.arrival;
			g.SpaceshipStructural[n]  = Expected.str;
			g.SpaceshipComponent[n]   = Expected.cmp;
			g.SpaceshipModule[n]      = Expected.mod;
			g.Progress(n).EconStreak     = Expected.econ;
			g.Progress(n).CultureStreak  = Expected.cult;
			g.Progress(n).ColonyFounded  = Expected.colony;
			g.Progress(n).DiasporaStreak = Expected.dias;
			g.Progress(n).ColonyOrder    = Expected.order;
			g.RecordWarStart(n, g.PlayerNumber(g.HumanPlayer));
			Sim.ClearTasks();
			return (g, n);
		}

		private static void AssertProgress(Game g, byte n)
		{
			Assert.Equal(Expected.launch,  g.SpaceshipLaunchTurn[n]);
			Assert.Equal(Expected.arrival, g.SpaceshipArrivalTurn[n]);
			Assert.Equal(Expected.str,     g.SpaceshipStructural[n]);
			Assert.Equal(Expected.cmp,     g.SpaceshipComponent[n]);
			Assert.Equal(Expected.mod,     g.SpaceshipModule[n]);
			Assert.Equal(Expected.econ,    g.Progress(n).EconStreak);
			Assert.Equal(Expected.cult,    g.Progress(n).CultureStreak);
			Assert.Equal(Expected.colony,  g.Progress(n).ColonyFounded);
			Assert.Equal(Expected.dias,    g.Progress(n).DiasporaStreak);
			Assert.Equal(Expected.order,   g.Progress(n).ColonyOrder);
			Assert.True(g.StartedWarWith(n, g.PlayerNumber(g.HumanPlayer)));
		}

		// All eleven survive a save and a load in the CURRENT build. This is the round trip
		// the refactor must not break.
		[Fact]
		public void EveryPerPlayerValueSurvivesARoundTrip()
		{
			(Game g, byte n) = AGameWithProgress();
			string path = Path.Combine(Settings.Instance.SavesDirectory, "progress-roundtrip.cos");

			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "the save just written did not load");

			AssertProgress(Game.Instance, n);
		}

		// ...and the same values load from a save written BEFORE the refactor. The fixture is
		// a file on disk, so this keeps holding once the live format has moved on.
		[Fact]
		public void APreRefactorSaveStillLoads()
		{
			if (!File.Exists(FixturePath))
			{
				// Not yet captured. RegenerateTheFormatFixture below writes it.
				Assert.Fail($"missing fixture {Fixture} — run the regenerator");
			}

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(FixturePath), "a pre-refactor save no longer loads");

			Game g = Game.Instance;
			byte n = g.Players.Select(p => g.PlayerNumber(p))
			                  .First(i => g.Progress(i).ColonyOrder == Expected.order);
			AssertProgress(g, n);
		}

		// A FULL roster must save. This is the bug that ate a whole run: Game.NewGame sizes
		// the per-player arrays to `competition + 1`, and it is a THIRD sizing site alongside
		// AddPlayer and the Game(CosFile) constructor. The victory arrays were added to two of
		// the three, so at competition 17 they were length 16 against 18 slots, every SaveCos
		// threw on the top two, and PerformAutoSave swallowed it — Log is a no-op in Release,
		// so a 526-turn game autosaved exactly nothing and nobody noticed until the file
		// timestamp was four hours stale.
		//
		// Small-competition fixtures cannot see this. The suite was green throughout.
		[Fact]
		public void AFullRosterGameSavesAndLoads()
		{
			Sim.NewGame(width: 80, height: 50, competition: 17);
			Game g = Game.Instance;
			byte top = g.Players.Where(p => p is not null).Select(p => g.PlayerNumber(p)).Max();
			Assert.True(top >= 16, $"fixture: expected a full roster, top slot is {top}");

			g.Progress(top).EconStreak = 5;
			g.Progress(top).ColonyOrder = 3;
			g.Progress(top).ColonyFounded = true;
			Sim.ClearTasks();

			string path = Path.Combine(Settings.Instance.SavesDirectory, "fullroster.cos");
			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "a full-roster game did not save and reload");

			Assert.Equal(5u, Game.Instance.Progress(top).EconStreak);
			Assert.Equal(3, Game.Instance.Progress(top).ColonyOrder);
			Assert.True(Game.Instance.Progress(top).ColonyFounded);
		}

		// Writes the fixture. Opt-in, because it must capture the format as it stands BEFORE a
		// change, not after — running it later would quietly re-bless whatever the format has
		// become, which is the one thing this file exists to prevent.
		//     CIVONE_WRITE_PROGRESS_FIXTURE=1 dotnet test --filter RegenerateTheFormatFixture
		[Trait("Category", "Fixture")]
		[Fact]
		public void RegenerateTheFormatFixture()
		{
			if (System.Environment.GetEnvironmentVariable("CIVONE_WRITE_PROGRESS_FIXTURE") != "1") return;

			(Game g, byte n) = AGameWithProgress();
			string dir = Path.Combine(RepoRoot(), "tests", "fixtures");
			Directory.CreateDirectory(dir);
			g.SaveCos(Path.Combine(dir, Fixture));
		}

		private static string RepoRoot()
		{
			var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return dir!.FullName;
		}
	}
}
