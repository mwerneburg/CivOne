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
	internal class Library : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A LIBRARY adds 50% to the SCIENCE",
			"output of the city, hastening every",
			"advance you research.",
		};

		private static readonly string[] _page2 =
		{
			"Requires WRITING.",
			"",
			"Its effect stacks with the",
			"UNIVERSITY and the OBSERVATORY.",
			"",
			"ISAAC NEWTON'S COLLEGE raises the",
			"bonus of each to two thirds.",
			"",
			"THE GREAT LIBRARY requires five",
			"of your cities to hold one.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Library() : base(8, 1)
		{
			Name = "Library";
			RequiredTech = new Writing();
			SetIcon(1, 0, true);
			SetSmallIcon(1, 0);
			Type = Building.Library;
		}
	}
}