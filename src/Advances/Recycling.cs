// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class Recycling : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"RECYCLING returns waste to use",
			"instead of to the ground.",
			"",
			"Allows the RECYCLING CENTER.",
		};

		private static readonly string[] _page2 =
		{
			"A recycling center cuts industrial",
			"pollution to a third — the right",
			"answer for a city with both a",
			"factory and a manufacturing plant.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Recycling() : base(5, 0, 2, Advance.MassProduction, Advance.Democracy)
		{
			Name = "Recycling";
			Type = Advance.Recycling;
		}
	}
}