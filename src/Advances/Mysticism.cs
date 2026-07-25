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
	internal class Mysticism : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"MYSTICISM seeks meaning behind the",
			"visible world.",
			"",
			"Allows STONEHENGE and ANGKOR WAT.",
		};

		private static readonly string[] _page2 =
		{
			"Watchers of the sky and watchers",
			"of the soul set out from the same",
			"advance.",
			"",
			"Not everything Stonehenge watches",
			"for is kindly.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Mysticism() : base(1, 1, 1, Advance.CeremonialBurial)
		{
			Name = "Mysticism";
			Type = Advance.Mysticism;
		}
	}
}