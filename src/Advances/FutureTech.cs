// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Graphics;

namespace CivOne.Advances
{
	internal class FutureTech : IAdvance
	{
		public byte Id => 255;
		public string Name => "Future Technology";
		public IAdvance[] RequiredTechs => new IAdvance[0];
		public Palette OriginalColours => null!;
		public IBitmap Icon => new Picture(112, 68);
		public byte PageCount => 1;
		public Picture DrawPage(byte pageNumber) => new Picture(320, 200);
		public bool Requires(byte id) => false;
		public bool Is<T>() where T : IAdvance => this is T;
		public bool Not<T>() where T : IAdvance => !(this is T);

		// What the player's scientists actually made. Names by the user (Sep 2026), headline
		// forms kept to 26 characters so a " Mk II" still sits in the newspaper.
		internal static readonly string[] Breakthroughs =
		{
			"Always-Cold Popsicle", "Window-Finding Flies", "Silent Velcro", "Hoverbike",
			"Mutable Tattoos", "Vacuum Tube Transit", "Instant Water", "Ready-Thaw Pets",
			"Tricorder", "Communications Watch", "Gasoline Pill", "Entangled Interplanet Chat",
			"Tesseract", "Your Plastic Pal", "No-Injury Trampoline", "Limb Regrowth",
			"Bachelor Chow", "Mosquito-Repelling Hat", "Flight by Distraction", "Scarless Healing",
			"Vat-Grown Organs", "Adaptive Fabrics", "Self-Healing Concrete", "Atomic Recycling",
			"Oil-Eating Bacteria", "Metal-Eating Fungus", "Cellular Data Storage",
			"Photonic Computing", "Better Than Life", "Time-Slowing Trumpet",
		};

		// The nth Future Tech's name (1-based). Shuffled once per game from the game id, so a
		// reload keeps the order and a new game gets a new one; after a full round every name
		// comes back as the next mark. A hand-rolled hash because string.GetHashCode is
		// randomised per process.
		internal static string Breakthrough(int n, string? gameId)
		{
			uint seed = 2166136261;
			foreach (char c in gameId ?? "") seed = (seed ^ c) * 16777619;
			var order = Breakthroughs.ToArray();
			var rng = new System.Random((int)seed);
			for (int i = order.Length - 1; i > 0; i--)
			{
				int j = rng.Next(i + 1);
				(order[i], order[j]) = (order[j], order[i]);
			}
			int k = System.Math.Max(0, n - 1), round = k / order.Length;
			string[] marks = { "", " Mk II", " Mk III", " Mk IV", " Mk V" };
			return order[k % order.Length] + (round < marks.Length ? marks[round] : $" Mk {round + 1}");
		}
	}
}