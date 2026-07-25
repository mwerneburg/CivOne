// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class HorsebackRiding : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"HORSEBACK RIDING puts riders on",
			"the backs of horses, and warfare",
			"gains a speed it never had.",
			"",
			"Allows CAVALRY.",
		};

		private static readonly string[] _page2 =
		{
			"Cavalry explore, chase barbarians",
			"and reach a threatened border in",
			"half the time. They are not meant",
			"to storm cities.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public HorsebackRiding() : base(7, 0, 1)
		{
			Name = "Horseback Riding";
			Type = Advance.HorsebackRiding;
		}
	}
}