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
	// Cursed wonder #5 (docs/cursed_wonders.md). The circle grants a Temple's
	// peace in every city, present and future (Michelangelo-style computed
	// effect in the City happiness pass) — but one time in four the circle is
	// a door, and something is standing in it (Game.OpenStoneDoor).
	internal class Stonehenge : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"STONEHENGE aligns the circle",
			"of standing stones with the",
			"turning sky: every city in the",
			"empire shares a Temple's peace.",
			"",
			"The oldest carvings do not show",
			"how to raise the stones.",
			"They show how to keep them shut.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public Stonehenge() : base(20)
		{
			Name = "Stonehenge";
			RequiredTech = new Mysticism();
			ObsoleteTech = new Religion();
			SetSmallIcon(1, 5);
			Type = Wonder.Stonehenge;
		}
	}
}
