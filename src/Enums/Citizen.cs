// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Enums
{
	internal enum Citizen
	{
		HappyMale = 0,
		HappyFemale = 1,
		ContentMale = 2,
		ContentFemale = 3,
		UnhappyMale = 4,
		UnhappyFemale = 5,
		Taxman = 6,
		Scientist = 7,
		Entertainer = 8,
		// Culture, as a use for a citizen. The other three specialists convert population
		// into gold, science or contentment; nothing converted it into culture, so the only
		// way to raise culture was buildings — which every civ builds alike, which is why
		// culture per head converges to within a few percent across a whole field and the
		// victory margin had to be set as low as 1.10x. This is the lever that was missing.
		Artist = 9
	}
}