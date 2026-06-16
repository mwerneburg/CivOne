#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Graphics;

namespace CivOne.Concepts
{
	internal abstract class BaseConcept : BaseInstance, IConcept
	{
		public string Name { get; protected set; } = null!;
		public IBitmap Icon => null!;
		public byte PageCount => 2;

		public virtual string[] GetPageText(byte pageNumber)
		{
			string suffix = pageNumber == 1 ? "" : "2";
			return Resources.GetCivilopediaText("BLURB4/" + Name.ToUpper() + suffix);
		}

		public Picture DrawPage(byte pageNumber)
		{
			string[] text = GetPageText(pageNumber);
			switch (pageNumber)
			{
				case 1: break;
				case 2: break;
				default:
					Log("Invalid page number: {0}", pageNumber);
					break;
			}
			
			Picture output = new Picture(320, 200);
			
			int yy = 76;
			foreach (string line in text)
			{
				Log(line);
				output.DrawText(line, 6, 1, 12, yy);
				yy += 9;
			}
			
			return output;
		}
		
		protected BaseConcept()
		{
			
		}
	}
}