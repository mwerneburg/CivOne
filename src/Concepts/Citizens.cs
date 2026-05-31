// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Concepts
{
	internal class Citizens : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"Each citizen of your city appears",
			"above the buildings panel. Mood is",
			"shown by color and posture:",
			"",
			"  GOLD, arms raised  — Happy",
			"  GREY, upright      — Content",
			"  RED, slumped       — Unhappy",
			"",
			"When unhappy citizens outnumber",
			"happy ones, the city falls into",
			"civil disorder and stops working.",
		};

		private static readonly string[] _page2 =
		{
			"A SPECIALIST replaces a worker. It",
			"works no tile but yields a single",
			"resource. A small badge sits above",
			"the head in a distinct color:",
			"",
			"  $ coin, dim amber — TAXMAN",
			"     converts trade into GOLD.",
			"  + atom, cyan      — SCIENTIST",
			"     converts trade into RESEARCH.",
			"  Note, amber       — ENTERTAINER",
			"     creates LUXURIES.",
			"",
			"Click any citizen icon to cycle",
			"through specialist roles.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Citizens()
		{
			Name = "Citizens";
		}
	}
}
