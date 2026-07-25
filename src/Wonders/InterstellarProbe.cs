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
	internal class InterstellarProbe : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"When the world's OBSERVATORIES",
			"catch a signal from the stars,",
			"the INTERSTELLAR PROBE answers.",
			"",
			"It launches humanity's first",
			"reach beyond the solar system,",
			"toward whatever is calling.",
		};

		private static readonly string[] _page2 =
		{
			"Requires SPACE FLIGHT, and only",
			"once the signal is received.",
			"",
			"Answering the stars is one path;",
			"raising the DOME to defend Earth",
			"is the other. Choose well.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public InterstellarProbe() : base(50)
		{
			Name         = "Interstellar Probe";
			RequiredTech = new SpaceFlight();
			ObsoleteTech = null;
			SetSmallIcon(2, 5);
			Type = Wonder.InterstellarProbe;
		}
	}
}
