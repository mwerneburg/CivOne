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
	internal class SamBattery : BaseBuilding
	{
		// The old text promised defence "against enemy AIRCRAFT and missiles", and advised
		// pairing the battery with an SDI DEFENSE. Both were wrong in the same direction —
		// they read as nuclear insurance, and there is none here. A Nuclear attack never
		// reaches DefendStrength at all: it takes its own branch to ApplyNuclearStrike
		// (BaseUnit.cs), where the only thing that stops it is a defender holding the FUSION
		// CORE, and the per-city SDI Defense building was never implemented.
		//
		// What the battery does do is now stated exactly, including what it does NOT do,
		// because a player who reads this as protection from the bomb will build it and be
		// wrong at the worst possible moment.
		private static readonly string[] _page1 =
		{
			"A SAM BATTERY rings the city with",
			"surface-to-air missiles.",
			"",
			"Aircraft attacking the city no",
			"longer strip its defenders of",
			"their FORTIFICATION — though the",
			"terrain still counts for nothing",
			"from the air.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROCKETRY.",
			"",
			"It answers BOMBERS, REAPER DRONES",
			"and CRUISE MISSILES alike.",
			"",
			"It does NOT stop a nuclear strike.",
			"Nothing does, save the FUSION CORE,",
			"which intercepts warheads over the",
			"whole empire that holds it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SamBattery() : base(15, 3)
		{
			Name = "SAM Battery";
			RequiredTech = new Advances.Rocketry();
			SetIcon(1, 1, false);
			SetSmallIcon(1, 0);
			Type = Building.SamBattery;
		}
	}
}
