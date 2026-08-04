// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class CascadeCathedral : BaseBuilding, IOlvirBuilding
	{
		private static readonly string[] _page1 =
		{
			"A CASCADE CATHEDRAL is a human",
			"cathedral that has been plumbed.",
			"",
			"Water is lifted to the towers and",
			"let fall: down the buttresses, out",
			"through the rose window, along",
			"flumes bolted to the nave, into a",
			"warm floor two hand-spans deep",
			"where the whole colony gathers.",
			"",
			"Requires a BREEDING SHRINE.",
		};

		private static readonly string[] _page2 =
		{
			"The Olvir did not raise these. They",
			"found them: tall, dry, acoustically",
			"magnificent buildings that the",
			"locals had abandoned or ceded, and",
			"could not imagine a use for that",
			"did not involve sitting still.",
			"",
			"The refit takes a generation. The",
			"stonework is untouched — it is",
			"considered impolite — and the",
			"congregation is simply moved from",
			"the pews to the water.",
			"",
			"Human opinion remains divided.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CascadeCathedral() : base(14, 3)
		{
			Name = "Cascade Cathedral";
			Type = Building.CascadeCathedral;
		}
	}
}
