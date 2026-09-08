// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using CivOne.Advances;
using CivOne.Screens;

namespace CivOne.Tasks
{
	internal class TechSelect : GameTask
	{
		private readonly Player _player;
		private readonly bool _human;

		private void ClosedChooseTech(object sender, EventArgs args)
		{
			EndTask();
		}

		public override void Run()
		{
			if (GameTask.Count<TechSelect>() > 1)
			{
				// Dialog already open
				EndTask();
				return;
			}

			if (!_human)
			{
				// This task is only for human players
				EndTask();
				return;
			}

			if (_player.Science == 0 && _player.Cities.Sum(x => x.Science) == 0)
			{
				// This task is only for human players
				EndTask();
				return;
			}

			// Two is all we need to know: is there a real choice to make, or only Future Tech?
			IAdvance[] available = _player.AvailableResearch.Take(2).ToArray();

			if (available.Length == 0)
			{
				EndTask();
				return;
			}

			// Once every advance is known, AvailableResearch yields Future Technology and
			// nothing else (Player.cs), so the dialog is a one-item menu — and it opened after
			// EVERY completed Future Tech, which is the rest of the game. Take the only option
			// and stay out of the way. A real choice, even one that happens to include Future
			// Tech, still asks.
			if (available.Length == 1 && available[0] is FutureTech)
			{
				_player.CurrentResearch = available[0];
				EndTask();
				return;
			}

			
			ChooseTech chooseTech = new ChooseTech();
			chooseTech.Closed += ClosedChooseTech;
			Common.AddScreen(chooseTech);
		}

		public TechSelect(Player player)
		{
			_player = player;
			_human = (Human == player);
		}
	}
}