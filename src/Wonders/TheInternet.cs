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
	// Cursed wonder #2 (docs/cursed_wonders.md). Every city, one conversation:
	// +25% science in every city and +1 culture per city per turn — unless the
	// conversation goes badly (Game.ExecuteSocialMediaSchism), in which case
	// half the country stops talking to the other half. Permanently.
	internal class TheInternet : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"THE INTERNET joins every mind",
			"in the empire into one endless",
			"conversation: knowledge and",
			"culture flow between all cities.",
			"",
			"Early trials report the discourse",
			"is 'mostly constructive'.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public TheInternet() : base(40)
		{
			Name = "The Internet";
			RequiredTech = new Computers();
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.TheInternet;
		}
	}
}
