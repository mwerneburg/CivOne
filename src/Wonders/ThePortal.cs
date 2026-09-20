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

		// Page 2 is the counterplay, the same split the Nanobot Factory uses: page 1 keeps
		// its one quiet warning (docs/cursed_wonders.md rule 4) and says nothing about the
		// odds, because a builder who knew them would not be a builder of this wonder.
		//
		// The eviction rule is the part that has to be written down. Nothing on screen
		// connects a food deficit to the houseguests leaving, and a player who does not know
		// it watches the infestation take a city every ten turns — including rivals' cities,
		// which is not a hint they will ever see either.
		private static readonly string[] _page2 =
		{
			"Requires GRAVITON ENGINEERING.",
			"",
			"Three times in four the counsel is",
			"luminous, and every war on Earth",
			"ends at once.",
			"",
			"The fourth time the Greys move in.",
			"A host city loses a fifth of its",
			"trade to corruption, and a citizen",
			"to a discontent nothing settles.",
			"Every 10 turns they take one more",
			"city - anyone's, anywhere.",
			"",
			"They keep no hungry house: one turn",
			"of NEGATIVE FOOD and they are gone.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

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
