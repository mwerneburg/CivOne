#nullable enable
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
			"sustaining. Trade revenues pay",
			"the maintenance of every city",
			"improvement that costs 1 gold",
			"per turn.",
			"",
			"It also reroutes INFRASTRUCTURE",
			"BONDS: cities producing bonds",
			"pool their shields each turn,",
			"and the pool is split evenly",
			"among all other cities.",
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

		public AdamSmithsTradingHouse() : base(60)
		{
			Name = "Adam Smith's Trading House";
			RequiredTech = new TheCorporation();
			ObsoleteTech = null;
			SetSmallIcon(6, 5);
			Type = Wonder.AdamSmithsTradingHouse;
		}
	}
}
