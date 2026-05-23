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
	internal class Xenolab : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A XENOLAB is a research center",
			"dedicated to the study of alien",
			"life sciences. Incorporating Olvir",
			"methodologies, it amplifies the",
			"city's total science output by 50%.",
		};

		private static readonly string[] _page2 =
		{
			"The first Xenolabs were founded on",
			"translated Olvir research packets.",
			"Human scientists found that alien",
			"empirical methods overturned",
			"centuries of assumption. The",
			"greatest insights came not from",
			"alien data, but from learning to",
			"question what we thought was fact.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public Xenolab() : base(12, 2)
		{
			Name = "Xenolab";
			RequiredTech = new Xenobiology();
			Type = Building.Xenolab;
		}
	}
}
