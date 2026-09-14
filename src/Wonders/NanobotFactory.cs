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

		// Page 2 is the counterplay: what to do once the bound has failed. Page 1 keeps its
		// one quiet warning (docs/cursed_wonders.md rule 4).
		private static readonly string[] _page2 =
		{
			"Requires SYNTHETIC ECOLOGY.",
			"",
			"If the bound fails, a grey tide",
			"doubles every 5 turns. It cannot",
			"cross the sea. Units left on it",
			"are lost; a city under it for 10",
			"turns falls. The factory never",
			"refits again.",
			"",
			"SETTLERS are immune: order them",
			"to CLEAN POLLUTION on a goo tile",
			"(2 turns). A NUCLEAR strike clears",
			"all goo connected to the blast.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

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
