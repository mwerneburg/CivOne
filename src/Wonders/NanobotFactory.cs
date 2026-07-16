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
	// Cursed wonder #12 (docs/cursed_wonders.md). Self-replicating assemblers:
	// a late-game Leonardo's Workshop, refitting the army for free — unless the
	// replication bounds don't hold (Game.SeedGreyGoo), in which case the
	// factory doesn't stop at your units. It doesn't stop at anything.
	internal class NanobotFactory : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The NANOBOT FACTORY breeds",
			"self-replicating assemblers that",
			"strip and refit obsolete units",
			"in the field, free of charge.",
			"",
			"The replication bound is",
			"mathematically proven to hold",
			"in all anticipated conditions.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public NanobotFactory() : base(40)
		{
			Name = "Nanobot Factory";
			RequiredTech = new SyntheticEcology();
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.NanobotFactory;
		}
	}
}
