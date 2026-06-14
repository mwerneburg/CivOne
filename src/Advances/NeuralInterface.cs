#nullable enable
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
	internal class NeuralInterface : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"NEURAL INTERFACE technology allows",
			"direct communication between",
			"biological minds and digital",
			"systems. Built on advances in",
			"neuroscience and computing, it",
			"enables the Neural Lab and",
			"advances toward Collective Memory.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.NeuralInterface;
		public override string Name => "Neural Interface";
		public override bool AvailablePreContact => true;
		public NeuralInterface() : base(Advance.MemeticProtocols) { }
	}
}
