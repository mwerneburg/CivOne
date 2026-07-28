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
	// Shields into science. The counterpart to the Infrastructure Bond, which turns a
	// city's output into shields for its neighbours: this turns it into research.
	//
	// It exists for the city that has nothing left worth building — every improvement
	// raised, the garrison sufficient, and a tech tree too far behind to offer anything
	// new. Before this, such a city rolled the production dice and produced another
	// obsolete spearman, forever. A civilization that has fallen behind should be able
	// to spend its industry on catching up.
	//
	// Like the Bond under Adam Smith's, this never completes: the shields are diverted
	// as they are earned (City.cs), so the city keeps the grant in production
	// indefinitely and the empire's research pays for it.
	internal class ResearchGrant : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A RESEARCH GRANT commits a city's",
			"workshops to the pursuit of",
			"knowledge instead of goods.",
			"",
			"The city never finishes it. Each",
			"turn its production is converted",
			"directly into research for the",
			"whole empire.",
			"",
			"Set it, and leave it set.",
		};

		private static readonly string[] _page2 =
		{
			"Requires WRITING.",
			"",
			"For a city with every improvement",
			"already raised — or a people who",
			"have fallen behind and have",
			"nothing worth the building — the",
			"foundry is better spent on the",
			"library.",
			"",
			"Nations that cannot out-produce",
			"their rivals may still out-think",
			"them.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public ResearchGrant() : base(1, 0)
		{
			Name = "Research Grant";
			RequiredTech = new Writing();
			Type = Building.ResearchGrant;
			// Never actually held by a city, so it can never be sold.
			SellPrice = 0;
		}
	}
}
