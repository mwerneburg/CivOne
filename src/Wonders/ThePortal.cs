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
	// Cursed wonder #3 (docs/cursed_wonders.md). The Portal reaches across
	// planes for beings of great enlightenment. Usually it finds them: their
	// counsel ends every war on Earth (Game.OpenPortal). One time in four it
	// finds the Greys, who move in, skim the till, and watch television.
	internal class ThePortal : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"THE PORTAL is humanity's reach",
			"across the planes: a standing",
			"gate to whatever answers.",
			"",
			"The builders expect beings of",
			"great enlightenment, whose mere",
			"presence would end all war.",
			"",
			"The builders expect a great",
			"many things.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public ThePortal() : base(40)
		{
			Name = "The Portal";
			RequiredTech = new GravitonEngineering();
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.ThePortal;
		}
	}
}
