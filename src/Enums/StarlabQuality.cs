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
	// Which Starlab got built. Drawn once, at completion, from the builder's character
	// (Game.DrawStarlabQuality) — not from a curse roll, and never redrawn afterwards.
	//
	// NotBuilt is 0 so the default-initialised field and a save with no entry both mean
	// "no station", and so the value survives CosSerializer's OmitNull handling. Callers
	// still gate on the wonder existing; this answers only WHICH one it is.
	internal enum StarlabQuality
	{
		NotBuilt = 0,
		Intended = 1,   // the station its founders described
		FreePort = 2,   // casinos, smugglers, and corruption
	}
}
