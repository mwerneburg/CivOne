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
	internal class CeremonialBurial : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"CEREMONIAL BURIAL is the first",
			"care taken with the dead, and the",
			"beginning of organized belief.",
			"",
			"Allows the TEMPLE.",
		};

		private static readonly string[] _page2 =
		{
			"The temple is the earliest cure",
			"for civil disorder, so this is",
			"often the advance that lets your",
			"cities grow past size 4 in peace.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CeremonialBurial() : base(8, 2, 0)
		{
			Name = "Ceremonial Burial";
			Type = Advance.CeremonialBurial;
		}
	}
}