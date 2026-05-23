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
	internal class AdamSmithsTradingHouse : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"ADAM SMITH'S TRADING HOUSE makes",
			"your empire's commerce self-",
			"sustaining. Trade revenues",
			"automatically pay the maintenance",
			"cost of every city improvement",
			"that costs 1 gold per turn.",
		};

		private static readonly string[] _page2 =
		{
			"Adam Smith argued in The Wealth of",
			"Nations that individual ambition,",
			"operating through free markets,",
			"generates collective prosperity.",
			"Cities that traded freely produced",
			"enough surplus to fund their own",
			"infrastructure without royal edict.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public AdamSmithsTradingHouse() : base(40)
		{
			Name = "Adam Smith's Trading House";
			RequiredTech = new TheCorporation();
			ObsoleteTech = null;
			SetSmallIcon(6, 5);
			Type = Wonder.AdamSmithsTradingHouse;
		}
	}
}
