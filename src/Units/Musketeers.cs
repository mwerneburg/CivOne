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

namespace CivOne.Units
{
	[Default]
	internal class Musketeers : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Riflemen;
		private static readonly string[] _page1 =
		{
			"MUSKETEERS carry firearms and mark",
			"the end of the age of spears.",
			"",
			"Their defence is far beyond",
			"anything before them, and they",
			"can attack at need.",
		};

		private static readonly string[] _page2 =
		{
			"Requires GUNPOWDER.",
			"Made obsolete by CONSCRIPTION.",
			"",
			"When Gunpowder is discovered, every",
			"MILITIA and PHALANX you own is",
			"outclassed at once. Upgrade the",
			"garrisons of your border cities",
			"first.",
			"",
			"LEONARDO'S WORKSHOP converts them",
			"for you.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Musketeers() : base(3, 2, 3, 1)
		{
			Type = UnitType.Musketeers;
			Name = "Musketeers";
			RequiredTech = new Gunpowder();
			ObsoleteTech = new Conscription();
			SetIcon('A', 0, 0);
		}
	}
}