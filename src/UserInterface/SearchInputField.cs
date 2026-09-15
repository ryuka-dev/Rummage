using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>
    /// The search box's input field. The game's UI input module sends a Cancel event for Backspace
    /// as well as Escape (its UI Cancel action binds both), and TMP_InputField leaves the field on
    /// any Cancel. Backspace has to delete text instead, so a Cancel that comes from it is ignored.
    /// </summary>
    internal sealed class SearchInputField : TMP_InputField
    {
        public override void OnCancel(BaseEventData eventData)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backspaceKey.isPressed)
            {
                return;
            }

            base.OnCancel(eventData);
        }
    }
}
