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
	internal class DarwinsVoyage : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"DARWIN'S VOYAGE sails the world to",
			"study life itself.",
			"",
			"Its discoveries grant your",
			"civilization FREE ADVANCES the",
			"moment it is completed.",
		};

		private static readonly string[] _page2 =
		{
			"Requires RAILROAD.",
			"",
			"A one-time leap: time the Voyage",
			"for when you are deep in an",
			"expensive line of research and",
			"vault past it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public DarwinsVoyage() : base(30)
		{
			Name = "Darwin's Voyage";
			RequiredTech = new RailRoad();
			ObsoleteTech = null;
			SetSmallIcon(6, 4);
			Type = Wonder.DarwinsVoyage;
		}
	}
}