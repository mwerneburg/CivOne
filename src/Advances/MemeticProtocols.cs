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
	internal class MemeticProtocols : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"MEMETIC PROTOCOLS decode the",
			"information patterns used by",
			"alien intelligences. Where",
			"computers give the capacity and",
			"philosophy the framework, Memetic",
			"Protocols yield understanding.",
			"Enables the Exchange Center.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.MemeticProtocols;
		public override string Name => "Memetic Protocols";
		public MemeticProtocols() : base(Advance.Computers, Advance.Philosophy) { }
	}
}
