#nullable enable
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
	// Zeros out the Democracy size-floor leak in this city. Applied AFTER the
	// Courthouse halving in City.Corruption, so a Police Station is the
	// definitive anti-graft building under utopian governments. Has no effect
	// on distance-based corruption (Despotism/Monarchy/Republic/Communism) —
	// for those, Courthouse and Audit Authority remain the levers.
	internal class PoliceStation : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A POLICE STATION stamps out the",
			"low-level graft that creeps into",
			"any large democratic city. While",
			"distance-based corruption is",
			"unaffected, the size-driven leak",
			"that troubles metropolitan",
			"administrations is eliminated.",
		};

		private static readonly string[] _page2 =
		{
			"Robert Peel's 1829 Metropolitan",
			"Police taught the modern world a",
			"deceptively simple lesson: a city",
			"polices itself best when it polices",
			"itself visibly. Where audit ledgers",
			"and judicial reach fail, a uniformed",
			"presence on every other street",
			"corner does the rest.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public PoliceStation() : base(10, 2)
		{
			Name = "Police Station";
			RequiredTech = new Communism();
			Type = Building.PoliceStation;
		}
	}
}
