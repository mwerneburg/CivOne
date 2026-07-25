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

namespace CivOne.Units
{
	internal class Sail : BaseUnitSea, IBoardable
	{
		public int Cargo
		{
			get
			{
				return 3;
			}
		}

		private static readonly string[] _page1 =
		{
			"The SAIL is a true seagoing ship,",
			"free of the Trireme's fear of open",
			"water, carrying 3 land units.",
		};

		private static readonly string[] _page2 =
		{
			"Requires NAVIGATION.",
			"Made obsolete by MAGNETISM.",
			"",
			"The first vessel that can safely",
			"cross an ocean, and so the unit",
			"that opens the rest of the world",
			"to you.",
			"",
			"MAGELLAN'S EXPEDITION adds a move",
			"to every ship you own.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Sail() : base(4, 1, 1, 3)
		{
			Type = UnitType.Sail;
			Name = "Sail";
			RequiredTech = new Navigation();
			ObsoleteTech = new Magnetism();
			SetIcon('B', 1, 1);
		}
	}
}