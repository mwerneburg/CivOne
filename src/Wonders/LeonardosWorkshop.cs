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
	internal class LeonardosWorkshop : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"LEONARDO'S WORKSHOP produces the",
			"Renaissance's finest military",
			"engineering. Each turn, one",
			"obsolete unit in your empire is",
			"automatically upgraded at no cost",
			"to your treasury.",
		};

		private static readonly string[] _page2 =
		{
			"Leonardo da Vinci filled notebooks",
			"with machines centuries ahead of",
			"their time: armoured vehicles,",
			"flying machines, giant crossbows.",
			"His workshop in Milan became the",
			"model for all state research,",
			"fusing art and engineering into",
			"the engine of a civilization.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public LeonardosWorkshop() : base(40)
		{
			Name = "Leonardo's Workshop";
			RequiredTech = new Invention();
			ObsoleteTech = new Automobile();
			SetSmallIcon(4, 5);
			Type = Wonder.LeonardosWorkshop;
		}
	}
}
