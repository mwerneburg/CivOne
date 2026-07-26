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
	// Public health as civic infrastructure: calms two citizens and ends the
	// plague outright in this city, regardless of aqueduct or advance (see
	// City.Disaster). Deliberately a HAPPINESS building as well as a health one —
	// it is the counterpart to the University in resisting ideological agitation.
	internal class Hospital : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A HOSPITAL calms 2 unhappy",
			"citizens and makes the city",
			"immune to PLAGUE.",
			"",
			"A population that trusts it will",
			"be cared for is far harder to",
			"turn against its government.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MEDICINE.",
			"",
			"Plague otherwise strikes crowded",
			"cities that lack an AQUEDUCT, and",
			"only the discovery of MEDICINE",
			"ends it empire-wide. A hospital",
			"ends it here and now.",
			"",
			"Its effect stacks with the TEMPLE,",
			"the COLOSSEUM and the CATHEDRAL.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public Hospital() : base(12, 2)
		{
			Name = "Hospital";
			RequiredTech = new Medicine();
			Type = Building.Hospital;
		}
	}
}
