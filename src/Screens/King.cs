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
	[Expand]
	internal class King : BaseScreen
	{
		private const int FONT_ID  = 0;
		private const int HEADER_H = 28;
		private const int PAD      = 8;
		private const int LEFT_W   = 220;

		private int RightX => PAD + LEFT_W + PAD;
		private int RightW => Width - RightX - PAD;
		private int BodyY  => HEADER_H + PAD;

		private readonly Player _enemy;
		private readonly bool _aiInitiated;

		private bool _menuAdded = false;
		private bool _needsRedraw = true;
		private FaceState _portraitState = FaceState.Neutral;
		private string[] _speechLines;

		// ── drawing ─────────────────────────────────────────────────────────

		private void DrawScene()
		{
			int fh = Resources.GetFontHeight(FONT_ID);

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);

			// Header bar
			this.FillRectangle(0, 0, Width, HEADER_H, CassetteTheme.BG1)
			    .FillRectangle(0, HEADER_H, Width, 1, CassetteTheme.BORDER);
			this.DrawText("DIPLOMATIC CONSOLE · " + _enemy.TribeNamePlural.ToUpper(),
			              FONT_ID, CassetteTheme.INK_MID, 10, 4);
			this.DrawText(_enemy.Civilization.Leader.Name.ToUpper(),
			              FONT_ID, CassetteTheme.PHOS, 10, 4 + fh + 2);

			bool atWar = Human.IsAtWar(_enemy);
			byte moodColor = atWar ? CassetteTheme.ALERT : CassetteTheme.OK;
			this.DrawText(atWar ? "◇ AT WAR ◇" : "▤ PEACE ▤",
			              FONT_ID, moodColor, Width - PAD, 4 + fh / 2, TextAlign.Right);

			// Left panel — portrait + status fields
			int bodyH = Height - BodyY - PAD;
			this.DrawCassettePanel(PAD, BodyY, LEFT_W, bodyH, "CHANNEL");

			Picture portrait = _enemy.Civilization.Leader.GetPortrait(_portraitState);
			int porW = portrait.Width, porH = portrait.Height;
			int porX = PAD + PAD + (LEFT_W - 2 * PAD - porW) / 2;
			int porY = BodyY + fh + 2 * PAD;
			this.AddLayer(portrait, porX, porY);
			this.DrawRectangle(porX - 2, porY - 2, porW + 4, porH + 4, CassetteTheme.BORDER);

			string caption = "▤ " + _enemy.Civilization.Leader.Name.ToUpper() + " ▤";
			int capY = porY + porH + PAD;
			this.DrawText(caption, FONT_ID, CassetteTheme.PHOS, PAD + LEFT_W / 2, capY, TextAlign.Center);

			int fieldX = PAD + PAD;
			int fieldW = LEFT_W - PAD * 3;
			int fieldY = capY + fh + PAD;
			var agg = _enemy.Civilization.Leader.Aggression;
			byte attColor = agg == AggressionLevel.Aggressive ? CassetteTheme.ALERT
			              : agg == AggressionLevel.Friendly   ? CassetteTheme.OK
			              : CassetteTheme.INK_MID;
			string attStr = agg == AggressionLevel.Aggressive ? "HOSTILE"
			              : agg == AggressionLevel.Friendly   ? "CORDIAL" : "NEUTRAL";

			this.DrawCassetteField("ATTITUDE", attStr, fieldX, fieldY, fieldW, FONT_ID, attColor);
			this.DrawCassetteField("STATUS", atWar ? "AT WAR" : "PEACE",
			                       fieldX, fieldY + fh + PAD, fieldW, FONT_ID, moodColor);
			this.DrawCassetteField("GOV", _enemy.Government.Name.ToUpper(),
			                       fieldX, fieldY + (fh + PAD) * 2, fieldW);

			// Right panel — speech transcript
			if (_speechLines == null) return;

			int speechPanelH = _speechLines.Length * fh + fh + 2 * PAD + 4;
			this.DrawCassettePanel(RightX, BodyY, RightW, speechPanelH, "TRANSCRIPT");
			for (int i = 0; i < _speechLines.Length; i++)
				this.DrawText(_speechLines[i], FONT_ID, CassetteTheme.INK_HIGH,
				              RightX + PAD + 2, BodyY + fh + PAD + i * fh);

			// "TRANSMIT · SELECT ACTION" header above menu items
			int transmitY = BodyY + speechPanelH + PAD;
			this.DrawText("TRANSMIT · SELECT ACTION", FONT_ID, CassetteTheme.INK_MID,
			              RightX + PAD, transmitY);
		}

		// ── greeting text ────────────────────────────────────────────────────

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

		// ── AI helper ────────────────────────────────────────────────────────

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

		// ── response helper ──────────────────────────────────────────────────

		private void SetResponse(FaceState face, params string[] lines)
		{
			_portraitState = face;
			_speechLines   = lines;
			_needsRedraw   = true;
		}

		// ── peace menu callbacks ─────────────────────────────────────────────

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

		// ── war menu callbacks ────────────────────────────────────────────────

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

		// ── menu construction ─────────────────────────────────────────────────

		private Menu BuildMenu(bool atWar)
		{
			int fh = Resources.GetFontHeight(FONT_ID);
			int speechPanelH = _speechLines.Length * fh + fh + 2 * PAD + 4;
			int transmitY    = BodyY + speechPanelH + PAD;
			int menuY        = transmitY + fh + PAD / 2;

			var menu = new Menu(Palette)
			{
				X              = RightX,
				Y              = menuY,
				MenuWidth      = RightW,
				ActiveColour   = CassetteTheme.PHOS_FAINT,
				TextColour     = CassetteTheme.INK_HIGH,
				DisabledColour = CassetteTheme.INK_LOW,
				FontId         = FONT_ID,
				Indent         = PAD
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

		// ── update loop ───────────────────────────────────────────────────────

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

		// ── constructor ───────────────────────────────────────────────────────

		public King(Player player, bool aiInitiated = false)
		{
			_enemy      = player;
			_aiInitiated = aiInitiated;

			// Start with the portrait's full palette so its pixels render correctly,
			// then overwrite indices 1-17 with the cassette design tokens.
			Picture portrait = player.Civilization.Leader.GetPortrait();
			Palette p = portrait.Palette.Copy();
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			_speechLines = GreetingText();
		}
	}
}
