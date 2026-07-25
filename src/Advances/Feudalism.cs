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
	internal class Feudalism : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"FEUDALISM binds land to service",
			"and service to land.",
			"",
			"Allows SUN TZU'S WAR ACADEMY.",
		};

		private static readonly string[] _page2 =
		{
			"The lord holds the land, the",
			"vassal holds the sword, and the",
			"next advance puts armour on both.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Feudalism() : base(1, 1, 0, Advance.Masonry, Advance.Monarchy)
		{
			Name = "Feudalism";
			Type = Advance.Feudalism;
		}
	}
}