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
	internal class Courthouse : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A COURTHOUSE halves CORRUPTION in",
			"the city, returning wasted trade",
			"to your treasury and laboratories.",
		};

		private static readonly string[] _page2 =
		{
			"Requires THE CODE OF LAWS.",
			"",
			"Corruption grows with distance",
			"from your PALACE, so the courthouse",
			"pays best in your most distant",
			"cities and is wasted in the",
			"capital itself.",
			"",
			"Under DEMOCRACY it curbs the graft",
			"that creeps into large cities.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Courthouse() : base(8, 1)
		{
			Name = "Courthouse";
			RequiredTech = new CodeOfLaws();
			SetIcon(1, 1, true);
			SetSmallIcon(1, 1);
			Type = Building.Courthouse;
		}
	}
}