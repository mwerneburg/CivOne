// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.UserInterface;
using CivOne.Wonders;

namespace CivOne.Units
{
	internal class Nuclear : BaseUnitAir
	{
		private static readonly string[] _page1 =
		{
			"A NUCLEAR MISSILE destroys every",
			"unit on its target tile and around",
			"it, and halves the population of",
			"a city.",
			"",
			"The ground it touches is left",
			"POLLUTED.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROCKETRY and THE",
			"MANHATTAN PROJECT.",
			"",
			"The Manhattan Project lets EVERY",
			"civilization build these, not only",
			"the one that completed it.",
			"",
			"Fallout drives GLOBAL WARMING, and",
			"the world does not forgive it.",
			"Some say the blasts wake worse",
			"things than warming.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		// Detonate where it stands.
		//
		// Until now a missile could only go off through Confront, which MoveTo reaches only
		// for a FOREIGN unit or a foreign city — so a strike on your own ground was impossible
		// and the missile simply walked into the city. That left Game.SterilizeGoo unreachable
		// in the case it was written for; its own comment calls a nuclear strike on grey goo
		// "the one time the game rewards nuking your own land", and there was no way to do it.
		//
		// Everything downstream already handles this. The missile sits at the centre of its
		// own blast and is consumed with everything else; CondemnNuclearStrike does nothing
		// when there is no foreign victim, so scouring your own country costs no standing; and
		// AwakenGozira still fires, because the first detonation of a game is the first
		// detonation of a game whoever it was aimed at.
		public override IEnumerable<MenuItem<int>> MenuItems
		{
			get
			{
				foreach (MenuItem<int> item in base.MenuItems)
				{
					// Ahead of the disband separator, so the order that ends the unit
					// usefully sits beside the one that just throws it away.
					if (item is null) yield return MenuDetonate();
					yield return item!;
				}
			}
		}

		private MenuItem<int> MenuDetonate() =>
			MenuItem<int>.Create("Detonate").SetShortcut("n").OnSelect((s, a) => Detonate());

		private void Detonate()
		{
			City? here = Map[X, Y].City;
			string where = here is not null ? here.Name : $"({X}, {Y})";
			var confirm = new Screens.Dialogs.ConfirmDetonate(where,
				here is not null && here.Owner == Owner);
			confirm.Detonate += (s, a) =>
			{
				int blastX = X, blastY = Y;
				Player detonator = Game.GetPlayer(Owner);
				Show blast = Show.EventArt("nuclearbombdetonation", "Nuclear bomb detonated!");
				blast.Done += (s2, a2) => Game.ApplyNuclearStrike(blastX, blastY, detonator);
				GameTask.Enqueue(blast);
			};
			Common.AddScreen(confirm);
		}

		public Nuclear() : base(16, 99, 0, 16)
		{
			Type = UnitType.Nuclear;
			Name = "Nuclear";
			RequiredTech = new Rocketry();
			RequiredWonder = new ManhattanProject();
			ObsoleteTech = null;
			SetIcon('D', 0, 0);
		}
	}
}