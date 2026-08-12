// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Buildings
{
	// The hole in the roster, found by measuring a finished game: 27% of AI cities had a food
	// surplus of zero and had simply stopped, against 5% of the human's — and 38 of those 73
	// stalled cities were coastal. An ocean tile yields 1 food and there was nothing in the
	// game that could raise it except the Sea Platform, which needs AquaticColonization, a
	// post-contact advance. The Ottomans finished on 36 advances with every city at food 0 and
	// twenty-one ocean tiles around Edirne. They were not playing badly; the building that
	// would have fed them did not exist.
	//
	// Pottery rather than a seafaring-era gate on purpose. The civs this is meant to rescue
	// are the backward ones, and a mid-game requirement would be reached by exactly the
	// empires that were never stuck.
	internal class Harbour : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A HARBOUR adds ONE FOOD to every",
			"ocean tile the city works.",
			"",
			"A coastal city that had stopped",
			"growing usually starts again.",
		};

		private static readonly string[] _page2 =
		{
			"Requires POTTERY.",
			"",
			"Only cities on the coast may build",
			"one — a lake shore is not enough.",
			"",
			"On a city ringed by water it is the",
			"difference between a fishing village",
			"and a port.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Harbour() : base(6, 1)
		{
			Name = "Harbour";
			RequiredTech = new Pottery();
			SetIcon(0, 1, true);
			SetSmallIcon(0, 2);
			Type = Building.Harbour;
		}
	}
}
