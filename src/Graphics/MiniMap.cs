// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Graphics
{
	internal static class MiniMap
	{
		// Terrain → cassette-palette colour. Shared by the sidebar minimap, the
		// full World Map, the history replay, and the map-generation preview so all
		// four read alike — and so they render without the original SP299 assets.
		public static byte TerrainColour(ITile t)
		{
			if (t is null) return CassetteTheme.BG0;
			if (t.Hut)     return CassetteTheme.ALERT;     // bright red dot
			switch (t.Type)
			{
				case Terrain.Ocean:      return CassetteTheme.OCEAN;
				case Terrain.River:      return CassetteTheme.CYAN;
				case Terrain.Forest:     return CassetteTheme.OK;
				case Terrain.Jungle:     return CassetteTheme.PHOS_DIM;
				case Terrain.Grassland1:
				case Terrain.Grassland2: return CassetteTheme.OK;
				case Terrain.Plains:     return CassetteTheme.PHOS_GLOW;
				case Terrain.Desert:     return CassetteTheme.PHOS;
				case Terrain.Hills:      return CassetteTheme.INK_MID;
				case Terrain.Mountains:  return CassetteTheme.INK_LOW;
				case Terrain.Swamp:      return CassetteTheme.PHOS_FAINT;
				case Terrain.Tundra:     return CassetteTheme.INK_HIGH;
				case Terrain.Arctic:     return CassetteTheme.WHITE;
				case Terrain.SaltFlat:   return CassetteTheme.INK_HIGH;   // pale, and unmistakably not sea
				default:                 return CassetteTheme.BG3;
			}
		}
	}
}
