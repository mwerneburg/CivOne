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
	internal class SouthPoleExpedition : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The SOUTH POLE EXPEDITION plants a",
			"research base on the last empty",
			"continent.",
			"",
			"It returns rare intelligence about",
			"the wider world — and about things",
			"best left frozen.",
		};

		private static readonly string[] _page2 =
		{
			"Requires SPACE FLIGHT.",
			"",
			"The ice keeps old secrets. Not",
			"every expedition comes back with",
			"only data.",
			"",
			"Read the expedition log to see",
			"what your teams found.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SouthPoleExpedition() : base(30)
		{
			Name = "South Pole Expedition";
			RequiredTech = new SpaceFlight();
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.SouthPoleExpedition;
		}
	}
}
