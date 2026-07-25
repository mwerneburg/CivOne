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

namespace CivOne.Wonders
{
	// Re-flavored from the original SETI Program wonder; keeps wonder id 20 so
	// existing saves load it transparently. The SETI signal storyline no longer
	// hangs off this wonder — it triggers from the world-wide Observatory count
	// (see Game.EndTurn). This is now a pure science wonder.
	internal class HumanGenomeProject : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The HUMAN GENOME PROJECT maps the",
			"whole of human heredity.",
			"",
			"Its discoveries flow into every",
			"laboratory in your empire, greatly",
			"speeding your research.",
		};

		private static readonly string[] _page2 =
		{
			"Requires GENETIC ENGINEERING.",
			"",
			"A pure science wonder — the code",
			"of life itself, turned to the",
			"advance of your civilization.",
			"",
			"Medicine, agriculture, and long",
			"life all follow from it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public HumanGenomeProject() : base(60)
		{
			Name = "Human Genome Project";
			RequiredTech = new GeneticEngineering();
			ObsoleteTech = null;
			SetSmallIcon(0, 5);
			Type = Wonder.HumanGenomeProject;
		}
	}
}