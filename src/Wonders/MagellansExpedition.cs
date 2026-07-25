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
	internal class MagellansExpedition : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"MAGELLAN'S EXPEDITION masters the",
			"art of ocean navigation.",
			"",
			"All your naval units gain extra",
			"MOVEMENT, ranging farther each",
			"turn.",
		};

		private static readonly string[] _page2 =
		{
			"Requires NAVIGATION.",
			"",
			"Faster fleets carry troops to war,",
			"escort caravans, and explore the",
			"map in a fraction of the time.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MagellansExpedition() : base(40)
		{
			Name = "Magellan's Expedition";
			RequiredTech = new Navigation();
			ObsoleteTech = null;
			SetSmallIcon(5, 3);
			Type = Wonder.MagellansExpedition;
		}
	}
}