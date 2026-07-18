// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
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
		private readonly List<AIDemand> _demands = null!;

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
			this.DrawText(_enemy.LeaderName.ToUpper(),
			              FONT_ID, CassetteTheme.PHOS, 10, 4 + fh + 2);

			bool atWar  = Human.IsAtWar(_enemy);
			bool allied = !atWar && Human.HasDefensePact(_enemy);
			byte moodColor = atWar ? CassetteTheme.ALERT : CassetteTheme.OK;
			this.DrawText(atWar ? "◇ AT WAR ◇" : allied ? "▣ ALLIED ▣" : "▤ PEACE ▤",
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

			string caption = "▤ " + _enemy.LeaderName.ToUpper() + " ▤";
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
			this.DrawCassetteField("STATUS", atWar ? "AT WAR" : allied ? "ALLIED" : "PEACE",
			                       fieldX, fieldY + fh + PAD, fieldW, FONT_ID, moodColor);
			this.DrawCassetteField("GOV", _enemy.Government.Name.ToUpper(),
			                       fieldX, fieldY + (fh + PAD) * 2, fieldW);

			// Right panel — speech transcript
			if (_speechLines is null) return;

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
						? [$"Our patience grows thin, {Human.LeaderName}.", "Surrender or face more war."]
					: [$"We seek to end this conflict,", $"{Human.LeaderName}. Let us talk terms."];

				return agg == AggressionLevel.Friendly
					? [$"Well met, {Human.LeaderName}!", $"The {_enemy.TribeNamePlural} bring greetings."]
					: agg == AggressionLevel.Aggressive
					? [$"We come with demands, {Human.LeaderName}.", "Choose your next words carefully."]
					: [$"We come to you, {Human.LeaderName},", "on a matter of mutual interest."];
			}

			if (atWar)
				return agg == AggressionLevel.Aggressive
					? [$"What do you want, {Human.LeaderName}?", "We have nothing to discuss."]
					: ["Ambassador. You come in a", "time of war. Speak quickly."];

			return agg == AggressionLevel.Friendly
				? [$"Greetings, {Human.LeaderName}!", $"The {_enemy.TribeNamePlural} welcome you."]
				: agg == AggressionLevel.Aggressive
				? ["Your visit had better be", "worth our time, ambassador."]
				: [$"Welcome, {Human.LeaderName}.", "What is your purpose here?"];
		}

		// ── demand helpers ───────────────────────────────────────────────────

		private string[] DemandGreeting()
		{
			bool atWar = Human.IsAtWar(_enemy);
			var agg = _enemy.Civilization.Leader.Aggression;
			if (_demands.Any(d => d.Kind == AIDemandKind.BegForAid))
				return [$"Our people starve, {Human.LeaderName}.", "We come not to demand, but to beg."];
			if (_demands.Any(d => d.Kind == AIDemandKind.OfferTribute))
				return [$"We cannot win this war, {Human.LeaderName}.", "Let us buy the peace instead."];
			if (_demands.Any(d => d.Kind == AIDemandKind.GrievancePack))
				return agg == AggressionLevel.Aggressive
					? [$"You seized our cities, {Human.LeaderName}.", "We demand reparations."]
					: [$"You hold our people's cities, {Human.LeaderName}.", "We insist on a just settlement."];
			if (atWar)
				return agg == AggressionLevel.Aggressive
					? [$"Our cities are occupied, {Human.LeaderName}.", "Return them. Now."]
					: [$"We demand the return of our cities,", $"{Human.LeaderName}. Peace awaits compliance."];
			return agg == AggressionLevel.Aggressive
				? [$"We have expectations, {Human.LeaderName}.", "Meet them or face consequences."]
				: [$"We come with requests, {Human.LeaderName}.", "Cooperation benefits us both."];
		}

		private string DemandLabel(AIDemand d)
		{
			if (d.Kind == AIDemandKind.GrievancePack)
			{
				var parts = new System.Collections.Generic.List<string> { $"Return {d.City!.Name}" };
				if (d.Advance is not null) parts.Add(d.Advance!.Name);
				if (d.Amount > 0) parts.Add($"${d.Amount}");
				return $"Accept settlement: {string.Join(" + ", parts)} → {d.Duration} turns of peace";
			}
			return d.Kind switch
			{
				AIDemandKind.BegForAid => $"Send aid: ${d.Amount} + emergency food → {d.Duration} turns of goodwill",
				AIDemandKind.ReturnCity => $"Return {d.City!.Name} → {d.Duration} turns of peace",
				AIDemandKind.GiveMap   => $"Share your maps → {d.Duration} turns of goodwill",
				AIDemandKind.GiveTech  => $"Transfer {d.Advance!.Name} → {d.Duration} turns of goodwill",
				AIDemandKind.GiveMoney => $"Pay ${d.Amount} tribute → {d.Duration} turns of goodwill",
				AIDemandKind.CedeCity  => $"Cede {d.City!.Name} → {d.Duration} turns of goodwill",
				AIDemandKind.OfferTribute => $"Accept tribute: ${d.Amount}/turn → peace while the gold flows",
				_                      => "Unknown demand"
			};
		}

		private void FulfillDemand(AIDemand d)
		{
			CloseMenus();
			byte aiNum = (byte)Game.PlayerNumber(_enemy);

			switch (d.Kind)
			{
				case AIDemandKind.BegForAid:
					if (d.Amount > 0) { Human.Gold -= (short)d.Amount; _enemy.Gold += (short)d.Amount; }
					// Emergency food airdrop: refill the starving city's store and pull a size-1
					// town back from the brink. A reprieve, not a cure — if its tiles can't feed
					// it, it will starve again (the forest-clear at founding is the real fix).
					if (d.City is not null && Game.GetCities().Contains(d.City))
					{
						d.City.Food = d.City.FoodRequired;
						if (d.City.Size == 1) d.City.Size++;
					}
					_enemy.SetAttitudeBonus(Human, d.Duration);
					Human.SetAttitudeBonus(_enemy, d.Duration); // gratitude flows both ways
					SetResponse(FaceState.Smiling,
						"Your generosity saves our people.",
						$"The {_enemy.TribeNamePlural} will not forget this.");
					break;

				case AIDemandKind.ReturnCity:
					d.City!.Owner = aiNum;
					Human.MakePeace(_enemy);
					_enemy.SetPeaceTreaty(Human, d.Duration);
					SetResponse(FaceState.Smiling,
						$"{d.City!.Name} is restored to us.",
						$"We guarantee {d.Duration} turns of peace.");
					break;

				case AIDemandKind.GiveMap:
					Human.MergeVisibility(_enemy);
					_enemy.SetAttitudeBonus(Human, d.Duration);
					SetResponse(FaceState.Smiling,
						"Your cartographers are generous.",
						$"{d.Duration} turns of goodwill — agreed.");
					break;

				case AIDemandKind.GiveTech:
					_enemy.AddAdvance(d.Advance!, false);
					_enemy.SetAttitudeBonus(Human, d.Duration);
					SetResponse(FaceState.Smiling,
						$"{d.Advance!.Name} — a worthy gift.",
						$"{d.Duration} turns of goodwill — agreed.");
					break;

				case AIDemandKind.GiveMoney:
					Human.Gold  -= (short)d.Amount;
					_enemy.Gold += (short)d.Amount;
					_enemy.SetAttitudeBonus(Human, d.Duration);
					SetResponse(FaceState.Smiling,
						$"${d.Amount} received. Satisfactory.",
						$"{d.Duration} turns of goodwill — agreed.");
					break;

				case AIDemandKind.CedeCity:
					d.City!.Owner = aiNum;
					_enemy.SetAttitudeBonus(Human, d.Duration);
					SetResponse(FaceState.Smiling,
						$"{d.City!.Name} joins our realm.",
						$"{d.Duration} turns of goodwill — agreed.");
					break;

				case AIDemandKind.OfferTribute:
					// EstablishTribute makes peace and installs the self-renewing
					// treaty; the gold arrives each turn the payer stays solvent.
					_enemy.EstablishTribute(Human, d.Amount);
					SetResponse(FaceState.Neutral,
						$"${d.Amount} will arrive each year.",
						"Call your armies home.");
					break;

				case AIDemandKind.GrievancePack:
					d.City!.Owner = aiNum;
					if (d.Advance is not null) _enemy.AddAdvance(d.Advance!, false);
					if (d.Amount > 0) { Human.Gold -= (short)d.Amount; _enemy.Gold += (short)d.Amount; }
					_enemy.SetPeaceTreaty(Human, d.Duration);
					_enemy.SetAttitudeBonus(Human, d.Duration);
					var responseLines = new System.Collections.Generic.List<string>
					{
						$"{d.City!.Name} is restored to us."
					};
					if (d.Advance is not null) responseLines.Add($"{d.Advance!.Name} received.");
					if (d.Amount > 0) responseLines.Add($"${d.Amount} received.");
					responseLines.Add($"{d.Duration} turns of peace — agreed.");
					SetResponse(FaceState.Smiling, responseLines.ToArray());
					break;
			}
		}

		private void DeclineAllDemands(object sender, EventArgs args)
		{
			CloseMenus();
			var agg = _enemy.Civilization.Leader.Aggression;
			if (_demands.Any(d => d.Kind == AIDemandKind.BegForAid))
			{
				SetResponse(FaceState.Neutral,
					"Then we are truly alone.",
					"History will remember your silence.");
				return;
			}
			if (_demands.Any(d => d.Kind == AIDemandKind.OfferTribute))
			{
				SetResponse(FaceState.Neutral,
					"So be it. We will die",
					"with our gold.");
				return;
			}
			if (_demands.Any(d => d.Kind == AIDemandKind.GrievancePack))
			{
				SetResponse(FaceState.Angry,
					agg == AggressionLevel.Aggressive
						? "Thievery without reparations."
						: "Your intransigence is noted.",
					"This matter is not settled.");
			}
			else
			{
				SetResponse(FaceState.Angry,
					agg == AggressionLevel.Aggressive
						? "Shortsighted. You will regret this."
						: "Very well. Do not expect our favor.");
			}
		}

		private Menu BuildDemandsMenu()
		{
			int fh           = Resources.GetFontHeight(FONT_ID);
			int speechPanelH = _speechLines.Length * fh + fh + 2 * PAD + 4;
			int menuY        = BodyY + speechPanelH + PAD + fh + PAD / 2;

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

			foreach (AIDemand demand in _demands)
			{
				AIDemand captured = demand;
				menu.Items.Add(DemandLabel(captured)).OnSelect((s, e) => FulfillDemand(captured));
			}
			menu.Items.Add("We want nothing to do with you.").OnSelect(DeclineAllDemands);

			return menu;
		}

		// ── AI helper ────────────────────────────────────────────────────────

		private bool AIAccepts(int basePct)
		{
			var agg = _enemy.Civilization.Leader.Aggression;
			int chance = agg == AggressionLevel.Friendly  ? basePct + 25
			           : agg == AggressionLevel.Aggressive ? basePct - 25
			           : basePct;
			if (_enemy.HasAttitudeBonus(Human)) chance += 20;
			// Culture admiration: nations defer to a civilization whose accumulated
			// culture dwarfs their own.
			if (Human.Culture >= 100 && Human.Culture >= _enemy.Culture * 2) chance += 10;
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
			if (!AIAccepts(50))
			{
				SetResponse(FaceState.Angry,
					"We are not interested", "in such an exchange.");
				return;
			}

			// AI demands the advance it values most (per its strategic stance).
			// Ties broken randomly so it's not perfectly predictable.
			AI ai = AI.Instance(_enemy);
			int topWeight = weOffer.Max(a => ai.AdvanceDemandValue(a));
			IAdvance[] topCandidates = weOffer.Where(a => ai.AdvanceDemandValue(a) == topWeight).ToArray();
			IAdvance pendingGive = topCandidates[Common.Random.Next(topCandidates.Length)];
			SetResponse(FaceState.Neutral,
				$"We seek {pendingGive.Name}.",
				"Name your price:");
			AddMenu(BuildAdvancePicker(theyOffer, pendingGive));
		}

		private Menu BuildAdvancePicker(IAdvance[] advances, IAdvance pendingGive)
		{
			int fh           = Resources.GetFontHeight(FONT_ID);
			int speechPanelH = _speechLines.Length * fh + fh + 2 * PAD + 4;
			int menuY        = BodyY + speechPanelH + PAD + fh + PAD / 2;

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

			foreach (IAdvance adv in advances.OrderBy(a => a.Name).Take(MenuRowsAvailable()))
			{
				IAdvance captured = adv;
				menu.Items.Add(adv.Name).OnSelect((s, e) =>
				{
					CloseMenus();
					_enemy.AddAdvance(pendingGive, false);
					GameTask.Enqueue(new GetAdvance(Human, captured));
					SetResponse(FaceState.Smiling,
						$"We offer {captured.Name}",
						$"in exchange for {pendingGive.Name}.",
						"Agreed.");
				});
			}

			return menu;
		}

		// ── gift callbacks ───────────────────────────────────────────────────
		// Player-initiated goodwill: the mirror of the AI's CedeCity/GiveTech
		// demands. Gifts are always accepted — the goodwill duration scales with
		// what the gift is worth to the recipient. The attitude bonus boosts
		// trade acceptance, pauses AI demand approaches, and deters war
		// declarations (AI.ConsiderWar).

		private Menu StyledMenu()
		{
			int fh           = Resources.GetFontHeight(FONT_ID);
			int speechPanelH = _speechLines.Length * fh + fh + 2 * PAD + 4;
			int menuY        = BodyY + speechPanelH + PAD + fh + PAD / 2;

			return new Menu(Palette)
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
		}

		private static int CityGiftDuration(City city) => Math.Min(100, 30 + 10 * city.Size);

		// The menu widget does not scroll: cap pickers to the rows that fit
		// between the transcript panel and the bottom of the screen.
		private int MenuRowsAvailable()
		{
			int fh = Resources.GetFontHeight(FONT_ID);
			int speechPanelH = _speechLines.Length * fh + fh + 2 * PAD + 4;
			int menuY = BodyY + speechPanelH + PAD + fh + PAD / 2;
			return Math.Max(3, (Height - menuY - PAD) / fh);
		}

		private void OfferCity(object sender, EventArgs args)
		{
			CloseMenus();
			// Anchored on the recipient: nearest to their capital first, so the
			// cities that would knit their realm together top the list.
			City? anchor = _enemy.Cities.FirstOrDefault(c => c.HasBuilding<Buildings.Palace>())
				?? _enemy.Cities.FirstOrDefault();
			City[] giftable = Human.Cities
				.Where(c => c.Size > 0 && !c.HasBuilding<Buildings.Palace>())
				.OrderBy(c => anchor is null ? 0 : Common.DistanceToTile(c.X, c.Y, anchor.X, anchor.Y))
				.ThenBy(c => c.Name)
				.Take(MenuRowsAvailable())
				.ToArray();
			if (giftable.Length == 0)
			{
				SetResponse(FaceState.Neutral,
					"You have no city to spare,", "and we both know it.");
				return;
			}

			SetResponse(FaceState.Neutral, "A city? A generous gesture.", "Which one?");
			Menu menu = StyledMenu();
			foreach (City city in giftable)
			{
				City captured = city;
				menu.Items.Add($"{city.Name} ({city.Size}) → {CityGiftDuration(city)} turns of goodwill")
					.OnSelect((s, e) =>
				{
					CloseMenus();
					int duration = CityGiftDuration(captured);
					// Units homed here fight on unsupported — the base changes flags.
					foreach (var unit in captured.Units.ToArray())
						unit.SetHome(null);
					captured.Owner = (byte)Game.PlayerNumber(_enemy);
					captured.ResetResourceTiles();
					_enemy.AddAttitudeBonus(Human, duration);
					SetResponse(FaceState.Smiling,
						$"{captured.Name} joins our realm.",
						$"{duration} turns of goodwill — agreed.");
				});
			}
			AddMenu(menu);
		}

		private void OfferTechnology(object sender, EventArgs args)
		{
			CloseMenus();
			IAdvance[] weOffer = Human.Advances.Where(a => !_enemy.HasAdvance(a)).ToArray();
			if (weOffer.Length == 0)
			{
				SetResponse(FaceState.Neutral,
					"Your scholars know nothing", "that ours do not.");
				return;
			}

			// Goodwill scales with how much the recipient's strategy wants the tech.
			// Most-wanted first — the gifts worth the most goodwill top the list —
			// capped to the rows that fit (the menu does not scroll).
			AI ai = AI.Instance(_enemy);
			int TechGiftDuration(IAdvance a) => Math.Min(75, 25 + 5 * ai.AdvanceDemandValue(a));

			SetResponse(FaceState.Neutral, "Knowledge freely given?", "We are listening.");
			Menu menu = StyledMenu();
			foreach (IAdvance adv in weOffer
				.OrderByDescending(a => ai.AdvanceDemandValue(a))
				.ThenBy(a => a.Name)
				.Take(MenuRowsAvailable()))
			{
				IAdvance captured = adv;
				menu.Items.Add($"{adv.Name} → {TechGiftDuration(adv)} turns of goodwill")
					.OnSelect((s, e) =>
				{
					CloseMenus();
					int duration = TechGiftDuration(captured);
					_enemy.AddAdvance(captured, false);
					_enemy.AddAttitudeBonus(Human, duration);
					SetResponse(FaceState.Smiling,
						$"{captured.Name} — a worthy gift.",
						$"{duration} turns of goodwill — agreed.");
				});
			}
			AddMenu(menu);
		}

		// Mutual defense pact: an attack on either signatory pulls the other in
		// automatically (Player.DeclareWar honors pacts one hop deep). 50 turns,
		// renewable by proposing again — a standing pact renews without a roll.
		private const int PactDuration = 50;

		private void ProposePact(object sender, EventArgs args)
		{
			CloseMenus();
			if (Human.HasDefensePact(_enemy))
			{
				Human.SetDefensePact(_enemy, PactDuration);
				_enemy.SetDefensePact(Human, PactDuration);
				SetResponse(FaceState.Smiling,
					"Our pact stands renewed.",
					$"({PactDuration} turns)");
				return;
			}
			if (!AIAccepts(25))
			{
				SetResponse(FaceState.Neutral,
					"We prefer to keep", "our options open.");
				return;
			}
			Human.SetDefensePact(_enemy, PactDuration);
			_enemy.SetDefensePact(Human, PactDuration);
			SetResponse(FaceState.Smiling,
				"Agreed. An attack on one",
				$"is an attack on both. ({PactDuration} turns)");
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

		private void TradeMaps(object sender, EventArgs args)
		{
			CloseMenus();
			bool weHaveNew  = Human.HasNewVisibilityFor(_enemy);
			bool theyHaveNew = _enemy.HasNewVisibilityFor(Human);

			if (!weHaveNew && !theyHaveNew)
			{
				SetResponse(FaceState.Neutral,
					"Our cartographers have nothing", "you don't already know.");
				return;
			}
			if (AIAccepts(65))
			{
				Human.MergeVisibility(_enemy);
				_enemy.MergeVisibility(Human);
				SetResponse(FaceState.Smiling,
					"Agreed. Our cartographers", "will share their charts.");
			}
			else
			{
				SetResponse(FaceState.Neutral,
					"We are not interested", "in sharing our maps.");
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
				SetResponse(FaceState.Angry, "You don't have the cards!", "The war continues.");
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
				menu.Items.Add("Trade maps").OnSelect(TradeMaps);
				menu.Items.Add("Offer a city").OnSelect(OfferCity);
				menu.Items.Add("Offer technology").OnSelect(OfferTechnology);
				menu.Items.Add(Human.HasDefensePact(_enemy) ? "Renew defense pact" : "Propose defense pact").OnSelect(ProposePact);
				menu.Items.Add("Demand tribute").OnSelect(SeekTribute);
				if (!(Human.Government is Gov.Democracy))
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
				if (_aiInitiated && _demands is { Count: > 0 })
					AddMenu(BuildDemandsMenu());
				else
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

		public King(Player player, bool aiInitiated = false, List<AIDemand>? demands = null)
		{
			_enemy       = player;
			_aiInitiated = aiInitiated;
			_demands     = demands ?? new List<AIDemand>();

			// Start with the portrait's full palette so its pixels render correctly,
			// then overwrite indices 1-17 with the cassette design tokens.
			Picture portrait = player.Civilization.Leader.GetPortrait();
			Palette p = portrait.Palette.Copy();
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			_speechLines = (_aiInitiated && demands is { Count: > 0 }) ? DemandGreeting() : GreetingText();
		}
	}
}
