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
	internal class SunTzusWarAcademy : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"SUN TZU'S WAR ACADEMY spreads the",
			"master strategist's principles",
			"throughout your empire. Every",
			"land combat unit you build starts",
			"its service as a veteran, in any",
			"city, until Gunpowder transforms",
			"the art of war.",
		};

		private static readonly string[] _page2 =
		{
			"Sun Tzu wrote The Art of War in",
			"the 5th century BC. Its influence",
			"never faded: generals of China,",
			"Persia, and Europe all applied",
			"its principles independently. Its",
			"genius lay not in tactics but in",
			"understanding war as a problem",
			"of information, patience, and will.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public SunTzusWarAcademy() : base(20)
		{
			Name = "Sun Tzu's War Academy";
			RequiredTech = new Feudalism();
			ObsoleteTech = new Gunpowder();
			SetSmallIcon(5, 5);
			Type = Wonder.SunTzusWarAcademy;
		}
	}
}
