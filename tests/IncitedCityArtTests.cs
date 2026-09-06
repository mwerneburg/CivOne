// CivOne tests
//
// A city bribed away by a diplomat announced itself with "<city> has built Incite Rebellion."
// The art lives in event_art, but both incite sites reached it through
// ImprovementArtScreen.FindArtPath(name, "event_art") — and that screen captions everything it
// draws as a construction project, because that is the only thing it was ever built to show.
//
// Both sites now go through Show.IncitedCity, which uses EventArtScreen: same art, but the
// caption is a whole sentence chosen by the caller rather than a name slotted into "has built".
//
// The art file gets its own check because a miss is silent: FindPath returns null and the
// generic "HAS FALLEN" capture screen plays instead, which is wrong but not obviously wrong.

using System.IO;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Screens;
using CivOne.Tasks;

namespace CivOne.Tests
{
	public class IncitedCityArtTests
	{
		private static string? RepositoryRoot()
		{
			DirectoryInfo? dir = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			return dir?.FullName;
		}

		private static string ShippedArt() => Path.Combine(RepositoryRoot()!, "runtime", "sdl",
			"Resources", "defaults", "data", "event_art", "incite_rebellion.png");

		// Sim points StorageDirectory at a throwaway temp folder, so no art ships with it and
		// every FindPath misses. Plant the file the installer would have put there — without it
		// the test only ever sees the missing-art fallback and can say nothing about the caption.
		private static void InstallTheArt()
		{
			string dir = Path.Combine(Settings.Instance.DataDirectory, "event_art");
			Directory.CreateDirectory(dir);
			File.Copy(ShippedArt(), Path.Combine(dir, "incite_rebellion.png"), overwrite: true);
		}

		private static (City city, Player briber) AnIncitedCity()
		{
			Sim.NewGame(width: 80, height: 50);
			InstallTheArt();
			Game g = Game.Instance;
			Player owner = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			Player briber = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer && p != owner);
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(40, 25, range: 3);
			return (g.AddCity(owner, 65, 40, 25)!, briber);
		}

		[Fact]
		public void TheBribedCityIsNotAnnouncedAsAConstructionProject()
		{
			(City city, Player briber) = AnIncitedCity();

			EventArtScreen art = Assert.IsType<EventArtScreen>(Show.IncitedCity(city, briber).Displayed);

			Assert.DoesNotContain("has built", art.Caption);
			Assert.Contains("induced to join", art.Caption);
			Assert.Contains(city.Name, art.Caption);
			Assert.Contains(briber.TribeNamePlural, art.Caption);
		}

		// Not the capture screen, and not the celebration art either: whatever else reads the
		// queue must be able to tell this event from the rest that share EventArtScreen.
		[Fact]
		public void TheScreenKnowsWhichEventItIs()
		{
			(City city, Player briber) = AnIncitedCity();

			EventArtScreen art = Assert.IsType<EventArtScreen>(Show.IncitedCity(city, briber).Displayed);

			Assert.Equal("incite_rebellion", art.ArtKey);
		}

		[Fact]
		public void TheRebellionArtIsShipped()
		{
			if (RepositoryRoot() is null) return;   // not running from the source tree
			Assert.True(File.Exists(ShippedArt()), $"incite rebellion art is missing: {ShippedArt()}");
		}
	}
}
