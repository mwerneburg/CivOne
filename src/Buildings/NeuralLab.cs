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

namespace CivOne.Buildings
{
	internal class NeuralLab : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A NEURAL LAB bridges biological",
			"and digital cognition through",
			"direct mind-machine interfaces.",
			"Citizens who train here report a",
			"profound sense of purpose,",
			"reducing unrest by one citizen.",
		};

		private static readonly string[] _page2 =
		{
			"Neural interfaces were among the",
			"most controversial Olvir gifts.",
			"Critics feared the loss of a",
			"private self; advocates argued",
			"that loneliness was humanity's",
			"oldest disease. Neural Labs show",
			"that both were right, and that",
			"citizens choose connection anyway.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public NeuralLab() : base(16, 3)
		{
			Name = "Neural Lab";
			RequiredTech = new NeuralInterface();
			Type = Building.NeuralLab;
		}
	}
}
