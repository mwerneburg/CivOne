// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;

namespace CivOne.Leaders
{
	public abstract class BaseLeader : BaseInstance, ILeader
	{
		private string _defaultName;
		private string DefaultName
		{
			get
			{
				foreach (LeaderModification modification in Modifications)
				{
					if (!modification.Name.HasValue) continue;
					_defaultName = modification.Name.Value;
				}
				return _defaultName;
			}
			set => _defaultName = value;
		}
		
		public string Name { get; set; }

		private readonly string _picFile = null;
		private readonly int _overlayX;
		private readonly int _overlayY;
		private Picture _picture, _portraitSmall;
		private Picture _pngPortrait;

		protected abstract Leader Leader { get; }

		private static string LeaderArtPath(string name)
		{
			try
			{
				string file = name.ToLower()
					.Replace('.', '_').Replace(' ', '_') + ".png";
				string path = Path.Combine(Settings.Instance.DataDirectory, "leader_art", file);
				return File.Exists(path) ? path : null;
			}
			catch { return null; }
		}

		private Picture LoadPngPortrait()
		{
			if (_pngPortrait != null) return _pngPortrait;
			string path = LeaderArtPath(Name);
			if (path == null) return null;
			byte[] rgba = PngFile.ReadRgba(path, out int w, out int h);
			if (rgba == null) return null;
			Palette pal = Common.DefaultPalette;
			CassetteTheme.ApplyTo(pal);
			byte[,] idx = PngFile.ToIndices(rgba, w, h, pal);
			var pic = new Picture(w, h, pal);
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				pic.Bitmap[x, y] = idx[y, x];
			_pngPortrait = pic;
			return _pngPortrait;
		}

		private IBitmap _modifiedPicture, _modifiedPortraitSmall;
		public Picture GetPortrait(FaceState state = FaceState.Neutral)
		{
			Picture png = LoadPngPortrait();
			if (png != null) return png;

			if (_modifications.ContainsKey(Leader))
			{
				if (_modifiedPicture == null)
				{
					_modifiedPicture = _modifications[Leader].LastOrDefault(x => x.Portrait != null && x.Portrait.GifToBitmap() != null)?.Portrait.GifToBitmap();
				}

				if (_modifiedPicture != null && _modifiedPicture.Width() == 139 && _modifiedPicture.Height() == 133)
				{
					return GFX256 ? _modifiedPicture.MakePalette(64, 16) : _modifiedPicture.MakePalette(1, 15);
				}
			}

			if (_picFile == null) return new Picture(139, 133, Common.GetPalette256);

			if (_picture == null)
			{
				_picture = Resources[_picFile];
			}

			Picture output = _picture[181, 67, 139, 133];
			switch (state)
			{
				case FaceState.Smiling:
					output.AddLayer(_picture[1, 51, 59, 49], _overlayX, _overlayY);
					break;
				case FaceState.Angry:
					output.AddLayer(_picture[1, 151, 59, 49], _overlayX, _overlayY);
					break;
				case FaceState.Neutral:
				default:
					break; // base portrait is already the neutral expression
			}
			return output;
		}

		public Picture PortraitSmall
		{
			get
			{
				if (_modifications.ContainsKey(Leader))
				{
					if (_modifiedPortraitSmall == null)
					{
						_modifiedPortraitSmall = _modifications[Leader].LastOrDefault(x => x.PortraitSmall != null && x.PortraitSmall.GifToBitmap() != null)?.PortraitSmall.GifToBitmap();
					}

					if (_modifiedPortraitSmall != null && _modifiedPortraitSmall.Width() == 27 && _modifiedPortraitSmall.Height() == 33)
					{
						return new Picture(_modifiedPortraitSmall.MatchColours(Resources["SLAM2"].Palette, 1, 255), Resources["SLAM2"].Palette);
					}
				}
				return _portraitSmall;
			}
		}
		
		private AggressionLevel _aggression = AggressionLevel.Normal;
		public AggressionLevel Aggression
		{
			get
			{
				foreach (LeaderModification modification in Modifications)
				{
					if (!modification.Aggression.HasValue) continue;
					_aggression = modification.Aggression.Value;
				}
				return _aggression;
			}
			set => _aggression = value;
		}

		private DevelopmentLevel _development = DevelopmentLevel.Normal;
		public DevelopmentLevel Development
		{
			get
			{
				foreach (LeaderModification modification in Modifications)
				{
					if (!modification.Development.HasValue) continue;
					_development = modification.Development.Value;
				}
				return _development;
			}
			set => _development = value;
		}

		private MilitarismLevel _militarism = MilitarismLevel.Normal;
		public MilitarismLevel Militarism
		{
			get
			{
				foreach (LeaderModification modification in Modifications)
				{
					if (!modification.Militarism.HasValue) continue;
					_militarism = modification.Militarism.Value;
				}
				return _militarism;
			}
			set => _militarism = value;
		}
		
		private static Dictionary<Leader, List<LeaderModification>> _modifications = new Dictionary<Leader, List<LeaderModification>>();
		internal static void LoadModifications()
		{
			_modifications.Clear();

			LeaderModification[] modifications = Reflect.GetModifications<LeaderModification>().ToArray();
			if (modifications.Length == 0) return;

			Log("Applying leader modifications");

			foreach (LeaderModification modification in modifications)
			{
				if (!_modifications.ContainsKey(modification.Leader))
					_modifications.Add(modification.Leader, new List<LeaderModification>());
				_modifications[modification.Leader].Add(modification);
			}

			Log("Finished applying leader modifications");
		}
		public IEnumerable<LeaderModification> Modifications => _modifications.ContainsKey(Leader) ? _modifications[Leader].ToArray() : new LeaderModification[0];

		protected BaseLeader(string name, string picFile, int overlayX, int overlayY)
		{
			DefaultName = name;
			Name = DefaultName;
			_picFile = picFile;
			_overlayX = overlayX;
			_overlayY = overlayY;
			if (picFile.StartsWith("KING"))
			{
				int id;
				if (int.TryParse(picFile.Substring(4), out id) && id >= 0 && id <= 13)
				{
					int col = (id % 7);
					int row = (id - col) / 7;
					_portraitSmall = Resources["SLAM2"][(28 * col) + 1, 34 * row, 27, 33];
					_portraitSmall.ColourReplace(0, 191, 0, 0, 27, 33);
					return;
				}
			}
			_portraitSmall = new Picture(27, 33, Common.GetPalette256);

			Aggression = AggressionLevel.Normal;
			Development = DevelopmentLevel.Normal;
			Militarism = MilitarismLevel.Normal;
		}

		protected BaseLeader(string name)
		{
			DefaultName = name;
			Name = DefaultName;
			_portraitSmall = new Picture(27, 33, Common.GetPalette256);
		}
	}
}