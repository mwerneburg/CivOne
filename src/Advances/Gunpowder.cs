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
	internal class Gunpowder : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"GUNPOWDER puts a charge behind the",
			"projectile, and armour ceases to",
			"matter.",
			"",
			"Allows MUSKETEERS.",
		};

		private static readonly string[] _page2 =
		{
			"It retires the MILITIA and the",
			"PHALANX at a stroke. Every",
			"garrison you own becomes obsolete",
			"on the turn you discover it.",
			"",
			"It also blunts THE GREAT WALL.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Gunpowder() : base(6, 1, 0, Advance.Invention, Advance.IronWorking)
		{
			Name = "Gunpowder";
			Type = Advance.Gunpowder;
		}
	}
}