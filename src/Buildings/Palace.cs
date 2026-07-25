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
	internal class Palace : BaseBuilding
	{
		public void SetFree()
		{
			Maintenance = 0;
		}

		private static readonly string[] _page1 =
		{
			"The PALACE marks your CAPITAL.",
			"",
			"The city holding it suffers NO",
			"CORRUPTION, and corruption in every",
			"other city grows with its distance",
			"from here.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MASONRY.",
			"",
			"Your first city begins with one.",
			"Building a palace elsewhere moves",
			"the capital, and the old one is",
			"lost.",
			"",
			"Under COMMUNISM distance no longer",
			"matters, and the palace instead",
			"halves corruption where it stands.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Palace() : base(20, 5)
		{
			Name = "Palace";
			RequiredTech = new Masonry();
			SetSmallIcon(0, 0);
			Type = Building.Palace;
			
			// Civilopedia says the Maintenance cost is 5, but it is actually 0
			SetFree();
		}
	}
}