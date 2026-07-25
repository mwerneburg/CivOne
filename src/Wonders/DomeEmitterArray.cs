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
	internal class DomeEmitterArray : BaseWonder, IDomeComponent
	{
		private static readonly string[] _page1 =
		{
			"The last piece of the planetary",
			"DOME.",
			"",
			"The EMITTER ARRAY casts the shield",
			"itself — a canopy of fusion fire",
			"between Earth and the dark.",
		};

		private static readonly string[] _page2 =
		{
			"Requires FUSION POWER.",
			"",
			"With the Emitter Array the five",
			"Dome pieces are complete: the Dome",
			"rises and Earth is held against",
			"all who would claim it.",
			"",
			"A victory that never leaves home.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public DomeEmitterArray() : base(30)
		{
			Name         = "Dome Emitter Array";
			RequiredTech = new FusionPower();
			ObsoleteTech = null;
			SetSmallIcon(3, 5);
			Type = Wonder.DomeEmitterArray;
		}
	}
}
