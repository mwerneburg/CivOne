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
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Tasks;
using CivOne.UserInterface;

using Gov = CivOne.Governments;

namespace CivOne.Screens
{
	internal class King : BaseScreen
	{
		private int OX => (Width - 320) / 2;
		private int OY => (Height - 200) / 2;

		private const int FONT_ID = 0;
		private const int PANEL_X = 2;
		private const int PANEL_Y = 135;
		private const int PANEL_W = 316;

		private readonly Player _enemy;
		private readonly Picture _background;
		private readonly bool _aiInitiated;

		private bool _menuAdded = false;
		private bool _needsRedraw = true;
		private FaceState _portraitState = FaceState.Neutral;
		private string[] _speechLines;

		// ── drawing ────────────────────────────────────────────────────────────

		private void DrawScene()
		{
			this.AddLayer(_background, OX, OY)
				.AddLayer(_enemy.Civilization.Leader.GetPortrait(_portraitState), 90 + OX, OY);

			if (_speechLines == null) return;

			int fh = Resources.GetFontHeight(FONT_ID);
			int panelH = _speechLines.Length * fh + 8;
			DrawPanel(PANEL_X + OX, PANEL_Y + OY, PANEL_W, panelH);
			for (int i = 0; i < _speechLines.Length; i++)
				this.DrawText(_speechLines[i], FONT_ID, 15, PANEL_X + 4 + OX, PANEL_Y + 4 + OY + i * fh);
		}

		// ── greeting text based on relationship and leader personality ──────────

		private string[] GreetingText()
		{
			var agg = _enemy.Civilization.Leader.Aggression;
			bool atWar = Human.IsAtWar(_enemy);

			if (_aiInitiated)
			{
				if (atWar)
					return agg == AggressionLevel.Aggressive
						? new[] { $"Our patience grows thin, {Human.LeaderName}.", "Surrender or face more war." }
						: new[] { $"We seek to end this conflict,", $"{Human.LeaderName}. Let us talk terms." };

				return agg == AggressionLevel.Friendly
					? new[] { $"Well met, {Human.LeaderName}!", $"The {_enemy.TribeNamePlural} bring greetings." }
					: agg == AggressionLevel.Aggressive
					? new[] { $"We come with demands, {Human.LeaderName}.", "Choose your next words carefully." }
					: new[] { $"We come to you, {Human.LeaderName},", "on a matter of mutual interest." };
			}

			if (atWar)
				return agg == AggressionLevel.Aggressive
					? new[] { $"What do you want, {Human.LeaderName}?", "We have nothing to discuss." }
					: new[] { "Ambassador. You come in a", "time of war. Speak quickly." };

			return agg == AggressionLevel.Friendly
				? new[] { $"Greetings, {Human.LeaderName}!", $"The {_enemy.TribeNamePlural} welcome you." }
				: agg == AggressionLevel.Aggressive
				? new[] { "Your visit had better be", "worth our time, ambassador." }
				: new[] { $"Welcome, {Human.LeaderName}.", "What is your purpose here?" };
		}

		// ── AI decision helper ──────────────────────────────────────────────────

		private bool AIAccepts(int basePct)
		{
			var agg = _enemy.Civilization.Leader.Aggression;
			int chance = agg == AggressionLevel.Friendly  ? basePct + 25
			           : agg == AggressionLevel.Aggressive ? basePct - 25
			           : basePct;
			return Common.Random.Next(100) < Math.Max(0, Math.Min(100, chance));
		}

		private int TributeAmount() =>
			Math.Max(25, 25 + Common.Random.Next(Math.Max(1, Math.Min(200, (int)_enemy.Gold) / 2)));

		// ── response helper ─────────────────────────────────────────────────────

		private void SetResponse(FaceState face, params string[] lines)
		{
			_portraitState = face;
			_speechLines   = lines;
			_needsRedraw   = true;
		}

		// ── peace menu callbacks ────────────────────────────────────────────────

		private void SeekKnowledge(object sender, EventArgs args)
		{
			CloseMenus();
			IAdvance[] theyOffer = _enemy.Advances.Where(a => !Human.HasAdvance(a)).ToArray();
			IAdvance[] weOffer   = Human.Advances.Where(a => !_enemy.HasAdvance(a)).ToArray();

			if (theyOffer.Length == 0)
			{
				SetResponse(FaceState.Smiling,
					"We have nothing left to teach.", "Your scholars know it all.");
				return;
			}
			if (weOffer.Length == 0)
			{
				SetResponse(FaceState.Neutral,
					"We would trade, but you have", "nothing to offer us.");
				return;
			}
			if (AIAccepts(50))
			{
				IAdvance give = weOffer[Common.Random.Next(weOffer.Length)];
				IAdvance get  = theyOffer[Common.Random.Next(theyOffer.Length)];
				_enemy.AddAdvance(give, false);
				GameTask.Enqueue(new GetAdvance(Human, get));
				SetResponse(FaceState.Smiling,
					$"We offer {get.Name}", $"in exchange for {give.Name}.", "Agreed.");
			}
			else
			{
				SetResponse(FaceState.Angry,
					"We are not interested", "in such an exchange.");
			}
		}

		private void SeekTribute(object sender, EventArgs args)
		{
			CloseMenus();
			if (AIAccepts(30))
			{
				int amount = TributeAmount();
				_enemy.Gold -= (short)amount;
				Human.Gold  += (short)amount;
				SetResponse(FaceState.Neutral, $"We will pay ${amount}.", "Now take it and leave.");
			}
			else
			{
				SetResponse(FaceState.Angry, "Tribute?! Never!", "Now leave our presence!");
				// Aggressive leaders may take this as a declaration of war
				if (_enemy.Civilization.Leader.Aggression == AggressionLevel.Aggressive
				    && Common.Random.Next(100) < 50)
					_enemy.DeclareWar(Human);
			}
		}

		private void DeclareWarOnThem(object sender, EventArgs args)
		{
			CloseMenus();
			Human.DeclareWar(_enemy);
			Destroy();
		}

		private void Farewell(object sender, EventArgs args)
		{
			CloseMenus();
			Destroy();
		}

		// ── war menu callbacks ──────────────────────────────────────────────────

		private void SeekPeace(object sender, EventArgs args)
		{
			CloseMenus();
			if (AIAccepts(50))
			{
				Human.MakePeace(_enemy);
				SetResponse(FaceState.Smiling,
					"We accept your offer of peace.", "May our peoples prosper together.");
			}
			else
			{
				SetResponse(FaceState.Angry,
					"Never! Your treachery will", "not be forgotten!");
			}
		}

		private void DemandTributeForPeace(object sender, EventArgs args)
		{
			CloseMenus();
			if (AIAccepts(20))
			{
				int amount = TributeAmount();
				_enemy.Gold -= (short)amount;
				Human.Gold  += (short)amount;
				Human.MakePeace(_enemy);
				SetResponse(FaceState.Neutral,
					$"We will pay ${amount} and agree to peace.", "The war is now over.");
			}
			else
			{
				SetResponse(FaceState.Angry, "We will pay nothing!", "The war continues.");
			}
		}

		// ── menu construction ───────────────────────────────────────────────────

		private Menu BuildMenu(bool atWar)
		{
			int fh     = Resources.GetFontHeight(FONT_ID);
			int menuY  = PANEL_Y + OY + _speechLines.Length * fh + 10;

			var menu = new Menu(Palette)
			{
				X           = PANEL_X + OX + 2,
				Y           = menuY,
				MenuWidth   = PANEL_W - 4,
				ActiveColour  = 11,
				TextColour    = 5,
				DisabledColour = 3,
				FontId        = FONT_ID
			};

			if (atWar)
			{
				menu.Items.Add("Propose peace").OnSelect(SeekPeace);
				menu.Items.Add("Demand tribute for peace").OnSelect(DemandTributeForPeace);
				menu.Items.Add("Farewell").OnSelect(Farewell);
			}
			else
			{
				menu.Items.Add("Seek exchange of knowledge").OnSelect(SeekKnowledge);
				menu.Items.Add("Demand tribute").OnSelect(SeekTribute);
				menu.Items.Add("Declare war!").OnSelect(DeclareWarOnThem);
				menu.Items.Add("Farewell").OnSelect(Farewell);
			}

			return menu;
		}

		// ── update loop ─────────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_needsRedraw) return false;
			_needsRedraw = false;

			DrawScene();

			if (!_menuAdded)
			{
				_menuAdded = true;
				AddMenu(BuildMenu(Human.IsAtWar(_enemy)));
			}

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (!HasMenu) Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (!HasMenu) Destroy();
			return true;
		}

		// ── constructor ─────────────────────────────────────────────────────────

		public King(Player player, bool aiInitiated = false)
		{
			_enemy = player;
			_aiInitiated = aiInitiated;

			bool modern = player.HasAdvance<Invention>();
			int govId = 0;
			if (player.Government is Gov.Monarchy)
				govId = 1;
			else if (player.Government is Gov.Republic || player.Government is Gov.Democracy)
				govId = 2;
			else if (player.Government is Gov.Communism)
			{
				govId = 3;
				modern = false;
			}

			_background = Resources[$"BACK{govId}{(modern ? "M" : "A")}"];
			_background.ColourReplace(0, 5);

			Picture portrait = player.Civilization.Leader.GetPortrait();
			using (Palette palette = _background.Palette.Copy())
			{
				palette.MergePalette(portrait.Palette, 64, 80);
				Palette = palette;
			}

			_speechLines = GreetingText();
		}
	}
}
