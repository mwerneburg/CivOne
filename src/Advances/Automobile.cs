// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class Automobile : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE AUTOMOBILE puts the engine",
			"under everything that moves on",
			"land.",
			"",
			"Allows ARMOR.",
		};

		private static readonly string[] _page2 =
		{
			"It retires KNIGHTS: the age of the",
			"horse ends in a single advance.",
			"",
			"Armor attacks heavily and can hold",
			"what it takes, which makes it the",
			"decisive land unit of the age.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Automobile() : base(6, 0, 2, Advance.Combustion, Advance.Steel)
		{
			Name = "Automobile";
			Type = Advance.Automobile;
		}
	}
}