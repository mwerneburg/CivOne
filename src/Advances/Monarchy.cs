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
	internal class Monarchy : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"MONARCHY places one crowned ruler",
			"above the tribes.",
			"",
			"Allows the MONARCHY government,",
			"which supports larger armies and",
			"frees your cities from the worst",
			"of DESPOTISM.",
		};

		private static readonly string[] _page2 =
		{
			"Despotism penalizes every tile",
			"that produces well. Changing to",
			"Monarchy is usually the largest",
			"single gain of the early game.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Monarchy() : base(2, 2, 0, Advance.CeremonialBurial, Advance.CodeOfLaws)
		{
			Name = "Monarchy";
			Type = Advance.Monarchy;
		}
	}
}