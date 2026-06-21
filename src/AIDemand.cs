// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;

namespace CivOne
{
	internal enum AIDemandKind { ReturnCity, GiveMap, GiveTech, GiveMoney, CedeCity, GrievancePack, BegForAid }

	internal class AIDemand
	{
		internal AIDemandKind Kind     { get; }
		internal City?        City     { get; }
		internal IAdvance?    Advance  { get; }
		internal int          Amount   { get; }
		internal int          Duration { get; }

		internal AIDemand(AIDemandKind kind, City? city = null, IAdvance? advance = null, int amount = 0, int duration = 50)
		{
			Kind     = kind;
			City     = city;
			Advance  = advance;
			Amount   = amount;
			Duration = duration;
		}
	}
}