// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Enums
{
	/// <summary>
	/// Map tile terrain types. Two non-obvious points: (1) Grassland is split into two values —
	/// Grassland1 produces a bonus shield, Grassland2 does not; the distinction is stored in each
	/// tile's terrain byte. (2) River is a full terrain type, not an overlay drawn on top of
	/// another terrain.
	/// </summary>
	public enum Terrain
	{
		None = -1,
		Desert = 0,
		Plains = 1,
		Grassland1 = 2,
		Forest = 3,
		Hills = 4,
		Mountains = 5,
		Tundra = 6,
		Arctic = 7,
		Swamp = 8,
		Jungle = 9,
		Ocean = 10,
		River = 11,
		Grassland2 = 12,
		// Exposed seabed. Never generated — only created by draining water away, so it appears
		// mid-game or not at all. See CivOne.Tiles.SaltFlat.
		SaltFlat = 13,
		// Wooded slopes: the hill's defence and the forest's timber. Generated where hills
		// meet woodland, and made or unmade by settlers (plant forest / chop). A terrain
		// rather than a flag on Hills because the engine switches on this enum in a dozen
		// places, every one of which would otherwise have to remember to ask.
		// See CivOne.Tiles.ForestedHills.
		ForestedHills = 14,
	}
}