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
	internal class TajMahal : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The TAJ MAHAL is the supreme",
			"symbol of your civilization's",
			"cultural achievement. Its beauty",
			"transcends politics: one content",
			"citizen in every city becomes",
			"happy, empire-wide.",
		};

		private static readonly string[] _page2 =
		{
			"Emperor Shah Jahan built the Taj",
			"Mahal as a monument to his beloved",
			"wife Mumtaz. Twenty thousand",
			"artisans worked for twenty-two",
			"years. Rival courts sent architects",
			"to study it. It became not a tomb",
			"but a proof: that a civilization",
			"could make something eternal.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public TajMahal() : base(30)
		{
			Name = "Taj Mahal";
			RequiredTech = new Chivalry();
			ObsoleteTech = null;
			Type = Wonder.TajMahal;
		}
	}
}
