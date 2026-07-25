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
	internal class Ironclad : BaseUnitSea
	{
		private static readonly string[] _page1 =
		{
			"The IRONCLAD is the first steam",
			"warship: armoured, fast and unable",
			"to carry cargo.",
			"",
			"It exists to sink other ships and",
			"shell the coast.",
		};

		private static readonly string[] _page2 =
		{
			"Requires THE STEAM ENGINE.",
			"Made obsolete by COMBUSTION.",
			"",
			"Wooden fleets cannot answer it.",
			"For a short age the civilization",
			"that has ironclads owns the sea.",
			"",
			"Keep transports behind it, never",
			"beside it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Ironclad() : base(6, 4, 4, 4)
		{
			Type = UnitType.Ironclad;
			Name = "Ironclad";
			RequiredTech = new SteamEngine();
			ObsoleteTech = new Combustion();
			SetIcon('A', 0, 1);
		}
	}
}