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
	internal class HagiaSofia : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"HAGIA SOFIA elevates the power",
			"of faith in your empire. Every",
			"Cathedral you own reduces",
			"unhappiness by 6 citizens instead",
			"of 4 — and by 8 if Michelangelo's",
			"Chapel stands on the same",
			"continent.",
		};

		private static readonly string[] _page2 =
		{
			"For a thousand years, Hagia Sofia",
			"was the greatest church in",
			"Christendom. Its half-dome, its",
			"cascading light, its gold mosaics",
			"overwhelmed visitors into silence.",
			"Justinian called it the House of",
			"God. Its influence on mosque and",
			"cathedral alike is immeasurable.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public HagiaSofia() : base(20)
		{
			Name = "Hagia Sofia";
			RequiredTech = new Religion();
			ObsoleteTech = new Communism();
			Type = Wonder.HagiaSofia;
		}
	}
}
