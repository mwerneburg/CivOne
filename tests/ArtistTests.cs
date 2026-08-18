// CivOne tests
//
// The Artist: a use for a citizen that produces culture.
//
// The other three specialists convert population into gold, science or contentment. Nothing
// converted it into culture, so the only way to raise culture was buildings — and every civ
// builds the same ones, which is why culture per head converges to within a few percent
// across a whole field (measured leads of 1.02-1.21x late game) and why the victory margin
// had to be set as low as 1.10x. This is the lever that was missing.
//
// It matters twice over, because Cultural Ascendancy is measured per HEAD of population: an
// artist raises the numerator and leaves the denominator alone. No other specialist touches
// any victory in the game — a Taxman's gold is invisible to Pax Mercatoria, which reads
// GrossOutput.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class ArtistTests
	{
		private static (Game game, Player owner, City city) ACity(int size = 6)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0);
			p.Government = new Monarchy();
			p.Explore(40, 25, range: 12);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = (byte)size;
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static System.Collections.Generic.IList<Citizen> Specialists(City c) =>
			(System.Collections.Generic.IList<Citizen>)typeof(City)
				.GetField("_specialists", System.Reflection.BindingFlags.NonPublic
				                        | System.Reflection.BindingFlags.Instance)!.GetValue(c)!;

		[Fact]
		public void AnArtistProducesCulture()
		{
			(Game g, Player p, City c) = ACity();
			int before = c.CultureRate;

			Specialists(c).Add(Citizen.Artist);
			c.InvalidateCache();

			Assert.Equal(before + City.ArtistCulture, c.CultureRate);
		}

		// The other specialists must NOT — otherwise every civ gets culture for free and the
		// lever means nothing.
		// Citizen is internal, so the values come through as ints: 6 Taxman, 7 Scientist,
		// 8 Entertainer.
		[Theory]
		[InlineData(6)]
		[InlineData(7)]
		[InlineData(8)]
		public void OtherSpecialistsProduceNoCulture(int kind)
		{
			(Game g, Player p, City c) = ACity();
			int before = c.CultureRate;

			Specialists(c).Add((Citizen)kind);
			c.InvalidateCache();

			Assert.Equal(before, c.CultureRate);
		}

		// It costs the same as the alternatives, so choosing culture is a trade rather than a
		// free win.
		[Fact]
		public void AnArtistCostsWhatTheOthersCost()
		{
			Assert.Equal(2, City.ArtistCulture);
		}

		// The save packs specialists two bits apiece and used values 0-2; the Artist took the
		// value the field already had room for, so nothing written before it can be misread.
		[Fact]
		public void ArtistsSurviveASaveRoundTrip()
		{
			(Game g, Player p, City c) = ACity();

			// The format stores specialist TYPES but derives the COUNT as Size minus worked
			// tiles, so the fixture has to be internally consistent: a size-6 city with two
			// worked tiles implies four specialists. Setting four while leaving six tiles
			// worked is an impossible state and decodes to none — which is the format being
			// right, not the round trip being broken.
			var tiles = (System.Collections.Generic.IList<CivOne.Tiles.ITile>)typeof(City)
				.GetField("_resourceTiles", System.Reflection.BindingFlags.NonPublic
				                          | System.Reflection.BindingFlags.Instance)!.GetValue(c)!;
			tiles.Clear();
			tiles.Add(Map.Instance[c.X - 1, c.Y]);
			tiles.Add(Map.Instance[c.X + 1, c.Y]);

			var spec = Specialists(c);
			spec.Clear();
			spec.Add(Citizen.Artist); spec.Add(Citizen.Taxman);
			spec.Add(Citizen.Artist); spec.Add(Citizen.Scientist);

			byte[] packed = c.GetResourceTiles();
			var before = spec.ToArray();
			c.SetResourceTiles(packed);

			Assert.Equal(before, Specialists(c).Take(before.Length).ToArray());
		}

		// The city-screen cycle has to reach it, or a human can never choose one.
		[Fact]
		public void TheCitizenCycleReachesTheArtist()
		{
			(Game g, Player p, City c) = ACity();
			Specialists(c).Add(Citizen.Taxman);

			var seen = new System.Collections.Generic.HashSet<Citizen>();
			for (int i = 0; i < 8; i++) { seen.Add(Specialists(c)[0]); c.ChangeSpecialist(0); }

			Assert.Contains(Citizen.Artist, seen);
			Assert.Contains(Citizen.Taxman, seen);
			Assert.Contains(Citizen.Scientist, seen);
			Assert.Contains(Citizen.Entertainer, seen);
		}

		// ...and the AI reaches for it when it is chasing that victory. This is the whole
		// question: whether the NPCs pick it up.
		[Fact]
		public void ACultureSeekingAiPrefersArtists()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "City.cs"));
			int at = src.IndexOf("Citizen preferred =");
			Assert.True(at > 0, "the specialist-typing rule has moved or been rewritten");
			string block = src.Substring(at, 260);

			Assert.Contains("AI.VictoryPath.Culture", block);
			Assert.Contains("Citizen.Artist", block);
		}

		// Whether they pick it up has to be answerable from a RUN, not only from a fixture.
		// A finished save cannot say: it stores the first twelve specialists in a city and
		// drops the rest, so the biggest cities — exactly the ones that park spare citizens —
		// read back clipped. The standings sample counts them live.
		[Fact]
		public void TheStandingsRecordCountsArtists()
		{
			string logger = System.IO.File.ReadAllText(RepoPath("src", "DecisionLogger.cs"));
			int at = logger.IndexOf("\"victory_standings\"),");
			Assert.True(at > 0, "the standings record has moved or been rewritten");
			Assert.Contains("KV(\"artists\"", logger.Substring(at, logger.IndexOf("}));", at) - at));

			// ...and the call site has to actually count them, not pass a placeholder.
			string game = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));
			int call = game.IndexOf("DecisionLogger.LogVictoryStandings(");
			string around = game.Substring(System.Math.Max(0, call - 400), 400);
			Assert.Contains("Citizen.Artist", around);
		}

		// The streaks travel with them. A claim is broken by clauses the log does not carry
		// (Banking, three surviving rivals, half the rivals bound by trade, a war of your own
		// making), so without the streak a run can show a civ over the economic bar for 150
		// turns with no victory and no way to tell which clause refused it.
		[Theory]
		[InlineData("econ_streak")]
		[InlineData("cult_streak")]
		public void TheStandingsRecordCarriesTheStreaks(string field)
		{
			string logger = System.IO.File.ReadAllText(RepoPath("src", "DecisionLogger.cs"));
			int at = logger.IndexOf("\"victory_standings\"),");
			Assert.True(at > 0, "the standings record has moved or been rewritten");
			Assert.Contains($"KV(\"{field}\"", logger.Substring(at, logger.IndexOf("}));", at) - at));

			string game = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));
			int call = game.IndexOf("DecisionLogger.LogVictoryStandings(");
			string around = game.Substring(call, 600);
			Assert.Contains("Progress(pn).EconStreak", around);
			Assert.Contains("Progress(pn).CultureStreak", around);
		}

		private static string RepoPath(params string[] parts)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			return System.IO.Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
		}
	}
}
