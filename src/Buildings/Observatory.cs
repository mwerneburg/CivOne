// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class Observatory : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"An OBSERVATORY turns a city's eyes",
			"to the heavens, adding to its",
			"SCIENCE output.",
			"",
			"Astronomers here chart the stars",
			"and sharpen every advance the",
			"city funds.",
		};

		private static readonly string[] _page2 =
		{
			"Requires COMPUTERS.",
			"",
			"When enough Observatories watch",
			"the sky across the world, together",
			"they may catch a signal that is",
			"not natural...",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Observatory() : base(16, 3)
		{
			Name = "Observatory";
			RequiredTech = new Advances.Computers();
			SetIcon(2, 2, false);
			SetSmallIcon(2, 1);
			Type = Building.Observatory;
		}
	}
}
