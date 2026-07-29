// CivOne tests
//
// BoxedIn() drives the Longboat — the only way an island civ ever leaves its
// archipelago. It is the inverse of HasExpansionRoom(), which used to prove
// reachability by comparing ContinentId. Every small island shares the "misc"
// bucket (15), so an island civ was told that islets across open water were part
// of its own landmass: room forever, BoxedIn never, no boat ever. Reachability is
// now established by walking the land, and this pins that.

using System.Linq;
using CivOne;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class ExpansionRoomTests
	{
		// Carve an island of the given half-width around (cx, cy) and drown everything
		// else within the search window, so the only land is the island itself.
		private static void MakeIsland(int cx, int cy, int half)
		{
			for (int dy = -12; dy <= 12; dy++)
			for (int dx = -12; dx <= 12; dx++)
			{
				int x = (cx + dx + Map.WIDTH) % Map.WIDTH;
				int y = cy + dy;
				if (y < 0 || y >= Map.HEIGHT) continue;
				bool island = System.Math.Abs(dx) <= half && System.Math.Abs(dy) <= half;
				Map.Instance.ChangeTileType(x, y, island ? Terrain.Grassland1 : Terrain.Ocean);
			}
			Map.Instance.RecalculateContinentsIfDirty();
		}

		private static Player IslandCiv(int half)
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;   // Player.AI is null for a non-autopilot human
			Player player = Game.Instance.HumanPlayer;

			int cx = 40, cy = 25;
			MakeIsland(cx, cy, half);
			player.Explore(cx, cy, range: 10);

			City city = Game.Instance.AddCity(player, 0, cx, cy);
			Assert.NotNull(city);
			return player;
		}

		private static bool BoxedIn(int half) => IslandCiv(half).AI!.BoxedIn();

		// A one-tile-radius rock: nowhere to put a second city, so the civ is boxed in
		// and must reach for the boat. This is the case the ContinentId test got wrong.
		[Fact]
		public void TinyIsland_IsBoxedIn()
		{
			Assert.True(BoxedIn(half: 1), "a civ on a 3x3 rock has nowhere left to settle");
		}

		// A civ that cannot expand must not sit in the Expand stance. Everything a
		// confined nation should be doing is gated on Develop — worker settlers and
		// irrigation, the leaner tax target that funds research, and the government
		// preference (Expand scores Monarchy 5 / Democracy 2; Develop reverses that).
		// GetStance used to return Expand purely for being under the city target, which
		// an island civ is forever, so it never reached any of them.
		[Fact]
		public void BoxedInCiv_DevelopsRatherThanExpands()
		{
			Player player = IslandCiv(half: 1);
			Assert.True(player.AI!.BoxedIn(), "precondition: the rock leaves nowhere to settle");
			Assert.Equal("Develop", player.AI!.CurrentStanceName());
		}

		// A generous landmass has room at least 3 tiles out, so the civ is not boxed in
		// and should keep expanding on foot rather than building longboats.
		[Fact]
		public void LargeLandmass_IsNotBoxedIn()
		{
			Assert.False(BoxedIn(half: 6), "a civ on a large island still has room to settle");
		}
	}
}
