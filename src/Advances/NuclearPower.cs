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
	internal class NuclearPower : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"NUCLEAR POWER draws steady",
			"electricity from the same reaction",
			"that makes the bomb.",
			"",
			"Allows the NUCLEAR PLANT.",
		};

		private static readonly string[] _page2 =
		{
			"A nuclear plant doubles factory",
			"output and halves its smoke, but",
			"may MELT DOWN. The risk ends only",
			"with FUSION POWER.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public NuclearPower() : base(2, 2, 2, Advance.NuclearFission, Advance.Electronics)
		{
			Name = "Nuclear Power";
			Type = Advance.NuclearPower;
		}
	}
}