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

namespace CivOne.Wonders
{
	internal class GreatLibrary : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The GREAT LIBRARY gathers the",
			"world's knowledge under one roof.",
			"",
			"It gives you for FREE any advance",
			"that most other civilizations",
			"already know.",
		};

		private static readonly string[] _page2 =
		{
			"Requires LITERACY.",
			"",
			"Let rivals do your research: the",
			"Library hands you their common",
			"advances, until the world outpaces",
			"it and it falls obsolete.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public GreatLibrary() : base(30)
		{
			Name = "Great Library";
			RequiredTech = new Literacy();
			ObsoleteTech = null;
			SetSmallIcon(5, 0);
			Type = Wonder.GreatLibrary;
		}
	}
}