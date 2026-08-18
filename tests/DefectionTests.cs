// CivOne tests
//
// A city changing flags for culture is the only place this game's culture reaches out and
// TAKES something — and nothing recorded it happening.
//
// The gates are narrow: a city of size 5 or less, rioting, with at most one defender, within
// five tiles of a civilization at peace with it holding three times its owner's culture, and
// then an 8%-a-turn roll, at most one in the world per turn. So "the mechanic never fires"
// and "it fires quietly, twice a game" look identical in a finished save, and the Artist
// specialist raises exactly the number the rule reads.
//
// Two things are checked here: that a rigged world actually produces a defection (the whole
// mechanic, end to end, including the roll), and that the log call sits inside that branch.
//
// The record itself is pinned on the source rather than by running the logger — DecisionLogger
// appends to the user's real decisions.jsonl on a background task, and a test that emitted
// rows would be writing into the file the analysis reads. Same reasoning as SalvageTests.

using System.Linq;
using System.Reflection;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class DefectionTests
	{
		private static string RepoRoot()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return dir!.FullName;
		}

		// Every gate satisfied except the dice: a small rioting town three tiles from a
		// far more cultured neighbour it is at peace with.
		private static (Game game, City town, Player owner, Player magnet) ATownReadyToLeave()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player owner = ps[0], magnet = ps[1];
			foreach (Player p in new[] { owner, magnet })
			{
				p.Government = new Monarchy();
				p.Explore(43, 25, range: 20);
			}

			// The owner's FIRST city takes the Palace, and a capital never defects — so the
			// town under test has to be their second. Founding it the other way round is how
			// this fixture first "proved" the mechanic was dead.
			g.AddCity(owner, 0, 48, 29)!.Size = 4;
			City town = g.AddCity(owner, 1, 40, 25)!;
			town.Size = 3;
			Assert.False(town.HasBuilding<CivOne.Buildings.Palace>(), "fixture: the town is a capital");
			g.AddCity(magnet, 2, 43, 25)!.Size = 6;   // three tiles away — inside the five

			owner.SetCulture(100);
			magnet.SetCulture(100 * 3 + 1);           // just over the triple the rule demands

			// The rule reads the disorder recorded during the city's own NewTurn, not a live
			// recomputation — so set the flag the same way the turn would have.
			town.WasInDisorder = true;

			Sim.ClearTasks();
			return (g, town, owner, magnet);
		}

		private static void Defections(Game g)
		{
			typeof(Game).GetMethod("ProcessCultureDefections",
				BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(g, null);
		}

		// The mechanic, end to end. 300 rounds at 8% leaves a false failure once in about
		// ten billion runs, which is a fair price for testing the real rule with its real dice
		// rather than a reimplementation of it.
		[Fact]
		public void ARiotingTownInASuperiorCulturesShadowChangesHands()
		{
			(Game g, City town, Player owner, Player magnet) = ATownReadyToLeave();
			byte before = town.Owner;
			Assert.Equal(g.PlayerNumber(owner), before);

			for (int i = 0; i < 300 && town.Owner == before; i++)
			{
				town.WasInDisorder = true;   // the riot persists; NewTurn is not running here
				Defections(g);
			}

			Assert.Equal(g.PlayerNumber(magnet), town.Owner);
		}

		// ...and it stays put when the pull is not there. Without this the test above passes
		// against a rule that hands cities over for no reason at all.
		[Fact]
		public void ATownWithNoCulturedNeighbourStaysPut()
		{
			(Game g, City town, Player owner, Player magnet) = ATownReadyToLeave();
			magnet.SetCulture(owner.Culture * 2);   // ahead, but under the triple

			for (int i = 0; i < 300; i++)
			{
				town.WasInDisorder = true;
				Defections(g);
			}

			Assert.Equal(g.PlayerNumber(owner), town.Owner);
		}

		// The log call must be inside the branch that actually moves the city — a record
		// emitted from anywhere else would count intentions, not flips.
		[Fact]
		public void TheFlipIsRecorded()
		{
			string body = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));
			int at = body.IndexOf("private void ProcessCultureDefections");
			Assert.True(at > 0, "ProcessCultureDefections has moved or been renamed");
			string method = body.Substring(at, body.IndexOf("\n\t\t}", at) - at);

			int log = method.IndexOf("DecisionLogger.LogDefection(");
			int flip = method.IndexOf("city.Owner = mnum");
			Assert.True(log > 0, "a defection is not recorded anywhere");
			Assert.True(flip > 0, "the ownership change has moved");
			Assert.True(log < flip, "the record is written after the city has already changed hands");
		}

		// Who lost it, who took it, and the culture on both sides — without the two culture
		// figures the log cannot say whether the artists are what made the difference, which
		// is the only reason this record exists.
		[Theory]
		[InlineData("city")]
		[InlineData("city_size")]
		[InlineData("from")]
		[InlineData("to")]
		[InlineData("from_cult")]
		[InlineData("to_cult")]
		public void TheRecordCarriesBothSides(string field)
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "DecisionLogger.cs"));
			// Anchored on the KV entry: "defection" also appears in the schema comment at the
			// top of the file, and matching that finds some other record's closing brace.
			int at = src.IndexOf("\"defection\"),");
			Assert.True(at > 0, "the defection record has moved or been rewritten");
			string record = src.Substring(at, src.IndexOf("}));", at) - at);

			Assert.Contains($"KV(\"{field}\"", record);
		}
	}
}
