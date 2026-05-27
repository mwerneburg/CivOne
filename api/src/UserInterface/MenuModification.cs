// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.UserInterface
{
	/// <summary>
	/// Override menu item text for a named in-game menu. Subclass this, pass the target menu
	/// ID to the base constructor, then override <see cref="ChangeMenuItemText"/> to replace
	/// the text of specific items. Return inputs unchanged for items you do not want to modify.
	/// </summary>
	public abstract class MenuModification : IModification
	{
		/// <summary>Identifies which menu this modification targets.</summary>
		public string MenuId { get; }

		/// <summary>
		/// Called for each item in the target menu. Return the modified text pair, or return
		/// the inputs unchanged to leave that item unmodified.
		/// </summary>
		public virtual (string MenuText, string ShortcutText) ChangeMenuItemText(string menuText, string shortcutText)
		{
			return (menuText, shortcutText);
		}

		/// <param name="menuId">The ID of the menu to target.</param>
		public MenuModification(string menuId)
		{
			MenuId = menuId;
		}
	}
}