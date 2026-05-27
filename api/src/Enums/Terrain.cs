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
	}
}