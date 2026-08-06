// CivOne tests
//
// The fourteenth terrain, and the first that is never generated: a salt flat exists only where
// water used to be, so it appears mid-game or not at all.
//
// The trap this file exists for: both save codecs encode terrain into Civ 1's original MAP
// byte codes and end with
//
//     default: code = 1;   // Ocean
//
// so a new terrain that nobody adds a case for saves as OCEAN. For salt flats that is the
// worst available failure — drain the sea, save, reload, and the water is back. It would look
// like the draining was broken rather than the codec. SaltFlat is code 16: the free low codes
// (0/4/5/8) are unassigned in the original encoding rather than guaranteed meaningless, and
// there is a whole byte to spend.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Persistence;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class SaltFlatTerrainTests
	{
		// The defect, stated directly: a salt flat survives a save and reload as a salt flat.
		[Fact]
		public void ASaltFlatSurvivesTheRoundTrip()
		{
			Sim.NewGame(width: 80, height: 50);
			Map m = Map.Instance;
			m.ChangeTileType(40, 25, Terrain.SaltFlat);
			m.ChangeTileType(41, 25, Terrain.Ocean);
			Assert.Equal(Terrain.SaltFlat, m[40, 25].Type);

			CosMap saved = m.SaveToCos();
			m.LoadFromCos(saved);

			Assert.Equal(Terrain.SaltFlat, Map.Instance[40, 25].Type);
			Assert.Equal(Terrain.Ocean, Map.Instance[41, 25].Type);
		}

		// The specific way it would go wrong, pinned separately: not-ocean is the whole point.
		[Fact]
		public void ItDoesNotComeBackAsOcean()
		{
			Sim.NewGame(width: 80, height: 50);
			Map m = Map.Instance;
			m.ChangeTileType(40, 25, Terrain.SaltFlat);

			m.LoadFromCos(m.SaveToCos());

			Assert.False(Map.Instance[40, 25].IsOcean,
				"the drained seabed must not refill on reload");
		}

		// Every other terrain still round-trips: the new case must not have disturbed the
		// existing encoding, which is shared with saves already on disk.
		[Fact]
		public void TheOtherTerrainsStillRoundTrip()
		{
			Sim.NewGame(width: 80, height: 50);
			Map m = Map.Instance;
			Terrain[] all =
			{
				Terrain.Desert, Terrain.Plains, Terrain.Grassland1, Terrain.Forest,
				Terrain.Hills, Terrain.Mountains, Terrain.Tundra, Terrain.Arctic,
				Terrain.Swamp, Terrain.Jungle, Terrain.Ocean, Terrain.SaltFlat,
			};
			// A row of RIVER directly above, because FinalizeForCosLoad runs the freshwater
			// retrofit: dry land out of reach of fresh water has an oasis planted on it, and an
			// inland Desert came back as River. Ocean is not enough — EnsureMaritimeFreshwater
			// exists precisely because salt water does not count. That is the retrofit working,
			// not the codec failing, but it makes a bare inland row untestable.
			for (int i = -2; i < all.Length + 2; i++) m.ChangeTileType(10 + i, 29, Terrain.River);
			for (int i = 0; i < all.Length; i++) m.ChangeTileType(10 + i, 30, all[i]);

			m.LoadFromCos(m.SaveToCos());

			for (int i = 0; i < all.Length; i++)
				Assert.Equal(all[i], Map.Instance[10 + i, 30].Type);
		}

		// ── what it is like to live on ──────────────────────────────────────

		// Zero food is the point. A city that loses its ocean tiles to salt should starve, not
		// merely stagnate, or taking the water away is an inconvenience rather than a disaster.
		[Fact]
		public void NothingGrowsOnIt()
		{
			ITile flat = new SaltFlat(40, 25, false);
			Assert.Equal(0, flat.Food);
		}

		// Worse than Desert, which at least yields a shield unworked.
		[Fact]
		public void ItIsWorseThanDesert()
		{
			ITile flat = new SaltFlat(40, 25, false);
			ITile desert = new Desert(41, 25, false);

			Assert.True(flat.Food <= desert.Food);
			Assert.True(flat.Shield < desert.Shield, "desert yields a shield; salt does not");
		}

		// ...but not dead ground for ever: a ruined coast is still worth something to a player
		// willing to spend the turns on it.
		[Fact]
		public void ItCanBeMinedEventually()
		{
			ITile flat = new SaltFlat(40, 25, false);
			Assert.True(flat.MiningShieldBonus > 0, "mining a salt pan must pay something");
			Assert.True(flat.MiningCost >= 10, "...but slowly");
		}

		// It is never generated — only ever left behind. A fresh world must contain none.
		[Fact]
		public void NoWorldIsGeneratedWithSaltFlats()
		{
			Sim.NewGame(width: 80, height: 50);
			Assert.DoesNotContain(Map.Instance.AllTiles(), t => t.Type == Terrain.SaltFlat);
		}

		// The Civilopedia picks terrain up by reflection (ITile : ICivilopedia), so a new tile
		// class needs no registration — but it does need to not break the listing.
		[Fact]
		public void ItAppearsInTheCivilopedia()
		{
			Sim.NewGame(width: 80, height: 50);
			Assert.Contains(Reflect.GetCivilopediaTerrainTypes(), t => t.Name == "Salt Flat");
		}
	}
}
