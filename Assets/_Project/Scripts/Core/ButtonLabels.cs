using System.Collections.Generic;

namespace Bomb.Core
{
    /// <summary>ボタン表記を引くときの操作の種類。</summary>
    public enum ButtonAction
    {
        Move,
        Jump,
        Interact,
        Throw,
        Mute,
        SwitchGuide,
        Pause,
        ReloadScene,
    }

    /// <summary>
    /// 操作ごとの「画面に出すボタンの文字」を、機器の種類ごとにまとめた対応表。
    /// ボタン名を画面に出す UI は、文字を直接書かずにここから引くこと。
    ///
    /// 【使い方】
    ///   今の機器に合わせた文字 : ButtonLabels.Get(ButtonAction.Interact)        → "E" / "X" / "□"
    ///   機器を指定した文字     : ButtonLabels.Get(ButtonAction.Jump, InputDeviceKind.PlayStation) → "×"
    ///
    /// 【バインドを変えたら】ここの文字も合わせて直すこと。実際のバインドは次の場所にある。
    ///   Move / Jump / Interact : Settings/ReceiveActions.inputactions
    ///   Throw       : PlayerThrower (KEY_THROW / PAD_THROW)
    ///   Mute        : MuteManager (KEY_TOGGLE / PAD_TOGGLE)
    ///   SwitchGuide : ControlGuideUI (KEY_SWITCH / PAD_SWITCH)
    ///   Pause       : PauseMenuUI (Esc / Start)
    ///   ReloadScene : SceneReloader (Q、コントローラーなし)
    ///
    /// 【フォントについて】今の TMP フォント (LiberationSans) に入っている記号だけを使っている。
    ///   × ○ □ は表示できる。△ と日本語は入っていないので使わない (豆腐になる)。
    /// </summary>
    public static class ButtonLabels
    {
        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>PS の △ はフォントに無いので文字で出す。</summary>
        private const string PS_TRIANGLE = "Triangle";

        // NOTE: 並びは { キーボード, Xbox, PlayStation }。その他のゲームパッドは Xbox の列を使う。
        private const int COLUMN_KEYBOARD = 0;
        private const int COLUMN_XBOX = 1;
        private const int COLUMN_PLAYSTATION = 2;

        private static readonly Dictionary<ButtonAction, string[]> LABEL_TABLE = new Dictionary<ButtonAction, string[]>
        {
            { ButtonAction.Move,        new[] { "WASD",  "L Stick", "L Stick" } },
            { ButtonAction.Jump,        new[] { "Space", "A",       "×"       } },
            { ButtonAction.Interact,    new[] { "E",     "X",       "□"       } },
            { ButtonAction.Throw,       new[] { "F",     "RT",      "R2"      } },
            { ButtonAction.Mute,        new[] { "M",     "RS",      "R3"      } },
            { ButtonAction.SwitchGuide, new[] { "R",     "View",    "Share"   } },
            { ButtonAction.Pause,       new[] { "Esc",   "Menu",    "Options" } },
            { ButtonAction.ReloadScene, new[] { "Q",     "",        ""        } },
        };

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>PS の △ ボタンの表記。今は使っていないが、バインドを足すときに使う。</summary>
        public static string PlayStationTriangle => PS_TRIANGLE;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>今操作している機器に合わせたボタンの文字を返す。</summary>
        public static string Get(ButtonAction action)
        {
            return Get(action, InputDeviceWatcher.CurrentKind);
        }

        /// <summary>機器の種類を指定してボタンの文字を返す。その機器で割り当てが無ければ空文字。</summary>
        public static string Get(ButtonAction action, InputDeviceKind kind)
        {
            if (!LABEL_TABLE.TryGetValue(action, out string[] labels))
            {
                return string.Empty;
            }

            return labels[ToColumn(kind)];
        }

        /// <summary>その機器で割り当てがあるなら true。</summary>
        public static bool Has(ButtonAction action, InputDeviceKind kind)
        {
            return !string.IsNullOrEmpty(Get(action, kind));
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private static int ToColumn(InputDeviceKind kind)
        {
            switch (kind)
            {
                case InputDeviceKind.KeyboardMouse:
                    return COLUMN_KEYBOARD;

                case InputDeviceKind.PlayStation:
                    return COLUMN_PLAYSTATION;

                // その他のゲームパッドは Xbox の表記にそろえる。
                default:
                    return COLUMN_XBOX;
            }
        }
    }
}
