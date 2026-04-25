// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Drawing;
using CivOne.Enums;
using CivOne.IO;

namespace CivOne
{
	internal partial class GameWindow
	{
		private SDL.Texture CursorTexture = null;

		private static Size DefaultCanvasSize
		{
			get
			{
				if (Settings.AspectRatio != AspectRatio.Expand)
					return new Size(320, 200);
				// Fallback for initial window sizing before first SetCanvasSize() call
				int w = Settings.ExpandWidth  > 0 ? Settings.ExpandWidth  : 640;
				int h = Settings.ExpandHeight > 0 ? Settings.ExpandHeight : 360;
				return new Size(w, h);
			}
		}

		private void Render()
		{
			switch(Settings.AspectRatio)
			{
				case AspectRatio.Scaled:
				case AspectRatio.ScaledFixed:
					if (!PixelScale) PixelScale = true;
					break;
				default:
					if (PixelScale) PixelScale = false;
					break;
			}

			Clear(Color.Black);
			GetBorders(out int x1, out int y1, out int x2, out int y2);
			if (_runtime.Layers == null) return;
			int drawW = x2 - x1, drawH = y2 - y1;
			foreach (Bytemap bytemap in _runtime.Layers)
			using (SDL.Texture canvas = CreateTexture(_runtime.Palette, bytemap))
			{
				int bx = x1, by = y1, bw = drawW, bh = drawH;
				if (Settings.AspectRatio == AspectRatio.Expand && CanvasWidth != 0 && CanvasHeight != 0
				    && (bytemap.Width != CanvasWidth || bytemap.Height != CanvasHeight))
				{
					// Non-canvas-sized bitmap (e.g. 320×200 dialog in a 480×300 canvas):
					// render at its own proportional scale, centred in the draw area.
					int scaleFactor = drawW / CanvasWidth;
					if (scaleFactor < 1) scaleFactor = 1;
					bw = bytemap.Width * scaleFactor;
					bh = bytemap.Height * scaleFactor;
					bx = x1 + (drawW - bw) / 2;
					by = y1 + (drawH - bh) / 2;
				}
				canvas.Draw(bx, by, bw, bh);
				
				switch (Settings.AspectRatio)
				{
					case AspectRatio.Scaled:
						{
							PointF scaleF = GetScaleF();
							CursorTexture?.Draw((int)(_mouseX * scaleF.X), (int)(_mouseY * scaleF.Y), (int)(CursorTexture.Width * scaleF.X), (int)(CursorTexture.Height * scaleF.Y));
						}
						break;
					case AspectRatio.ScaledFixed:
						{
							PointF scaleF = GetScaleF();
							CursorTexture?.Draw((int)((_mouseX /*+ x1*/) * scaleF.X), (int)((_mouseY/* + y1*/) * scaleF.Y), (int)(CursorTexture.Width * scaleF.X), (int)(CursorTexture.Height * scaleF.Y));
						}
						break;
					default:
						CursorTexture?.Draw(x1 + (_mouseX * ScaleX), y1 + (_mouseY * ScaleY), CursorTexture.Width * ScaleX, CursorTexture.Height * ScaleY);
						break;
				}
			}
		}

		private Size SetCanvasSize()
		{
			if (Settings.AspectRatio != AspectRatio.Expand)
				return new Size(320, 200);

			// Derive canvas as half the actual window size so scale is always exactly 2.
			// Rounding down to multiples of 8 keeps pixel-art alignment clean.
			int cw = (ClientRectangle.Width  / 2 / 8) * 8;
			int ch = (ClientRectangle.Height / 2 / 8) * 8;
			return new Size(Math.Max(320, cw), Math.Max(200, ch));
		}

		private static int InitialCanvasWidth => DefaultCanvasSize.Width;
		private static int InitialCanvasHeight => DefaultCanvasSize.Height;

		private static int InitialWidth => InitialCanvasWidth * Settings.Scale;
		private static int InitialHeight => InitialCanvasHeight * Settings.Scale;

		private Size ClientRectangle => new Size(Width, Height);
		
		private int ScaleX
		{
			get
			{
				int cw = CanvasWidth, ch = CanvasHeight;
				if (cw == 0) cw = DefaultCanvasSize.Width;
				if (ch == 0) ch = DefaultCanvasSize.Height;
				
				switch (Settings.AspectRatio)
				{
					case AspectRatio.Fixed:
					case AspectRatio.ScaledFixed:
					case AspectRatio.Expand:
						int scaleX = (ClientRectangle.Width - (ClientRectangle.Width % cw)) / cw;
						int scaleY = (ClientRectangle.Height - (ClientRectangle.Height % ch)) / ch;
						if (scaleX > scaleY)
							return scaleY;
						return scaleX;
					default:
						return (ClientRectangle.Width - (ClientRectangle.Width % cw)) / cw;
				}
			}
		}

		private int ScaleY
		{
			get
			{
				int cw = CanvasWidth, ch = CanvasHeight;
				if (cw == 0) cw = DefaultCanvasSize.Width;
				if (ch == 0) ch = DefaultCanvasSize.Height;

				switch (Settings.Instance.AspectRatio)
				{
					case AspectRatio.Fixed:
					case AspectRatio.ScaledFixed:
					case AspectRatio.Expand:
						int scaleX = (ClientRectangle.Width - (ClientRectangle.Width % cw)) / cw;
						int scaleY = (ClientRectangle.Height - (ClientRectangle.Height % ch)) / ch;
						if (scaleY > scaleX)
							return scaleX;
						return scaleY;
					default:
						return (ClientRectangle.Height - (ClientRectangle.Height % ch)) / ch;
				}
			}
		}

		private int CanvasWidth => Runtime.CanvasSize.Width;
		private int CanvasHeight => Runtime.CanvasSize.Height;

		private int DrawWidth => CanvasWidth * ScaleX;
		private int DrawHeight => CanvasHeight * ScaleY;

		private void GetBorders(out int x1, out int y1, out int x2, out int y2)
		{
			x1 = (ClientRectangle.Width - DrawWidth) / 2;
			y1 = (ClientRectangle.Height - DrawHeight) / 2;
			x2 = x1 + DrawWidth;
			y2 = y1 + DrawHeight;

			switch (Settings.AspectRatio)
			{
				case AspectRatio.Scaled:
					x1 = 0;
					y1 = 0;
					x2 = ClientRectangle.Width;
					y2 = ClientRectangle.Height;
					break;
				case AspectRatio.ScaledFixed:
					float scaleX = (float)ClientRectangle.Width / CanvasWidth;
					float scaleY = (float)ClientRectangle.Height / CanvasHeight;
					if (scaleX > scaleY) scaleX = scaleY;
					else if (scaleY > scaleX) scaleY = scaleX;

					int drawWidth = (int)((float)CanvasWidth * scaleX);
					int drawHeight = (int)((float)CanvasHeight * scaleY);

					x1 = (ClientRectangle.Width - drawWidth) / 2;
					y1 = (ClientRectangle.Height - drawHeight) / 2;
					x2 = x1 + drawWidth;
					y2 = y1 + drawHeight;
					break;
			}
		}
	}
}