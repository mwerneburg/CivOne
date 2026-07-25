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
	internal class Bank : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A BANK adds a further 50% to the",
			"TAX and LUXURY revenue of the",
			"city.",
			"",
			"Its effect stacks with the",
			"MARKETPLACE.",
		};

		private static readonly string[] _page2 =
		{
			"Requires BANKING.",
			"",
			"Best placed in large trade cities,",
			"where half again of a large sum",
			"is worth more than half again of",
			"a small one.",
			"",
			"A city in disorder may see its",
			"bank looted.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Bank() : base(12, 3)
		{
			Name = "Bank";
			RequiredTech = new Banking();
			SetIcon(2, 0, true);
			SetSmallIcon(1, 4);
			Type = Building.Bank;
		}
	}
}