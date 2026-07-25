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
	internal class Disband : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"DISBAND removes a unit from the",
			"game for good.",
			"",
			"Disband inside a city and part of",
			"its build cost returns as shields",
			"toward the city's production.",
			"",
			"Disband in the field and it is",
			"simply gone.",
		};

		private static readonly string[] _page2 =
		{
			"Use DISBAND to cut the upkeep of",
			"obsolete or surplus units, or to",
			"pour an old unit's shields into a",
			"new project.",
			"",
			"Retiring outdated defenders keeps",
			"your treasury and production lean.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Disband()
		{
			Name = "Disband";
		}
	}
}