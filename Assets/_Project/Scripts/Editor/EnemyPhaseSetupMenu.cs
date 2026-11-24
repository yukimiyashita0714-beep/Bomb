using System.Text;
using Bomb.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 爆弾設置前後の敵の切り替え (EnemyPhaseSwitcher) を、開いているシーンに組み込むエディタ拡張。
/// ・BombEvent のオブジェクトに EnemyPhaseSwitcher を付ける
/// ・BombEvent / BeforeEnemies / AfterEnemies の参照を埋める
/// すでに付いていれば作り直さず参照だけ埋めるので、何度実行しても同じ結果になる。
/// </summary>
public static class EnemyPhaseSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Enemy Phase Switcher";
    private const string DIALOG_TITLE = "Enemy Phase Switcher Setup";

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        StringBuilder report = new StringBuilder();

        BombEvent bombEvent = Object.FindFirstObjectByType<BombEvent>(FindObjectsInactive.Include);

        if (bombEvent == null)
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE, "シーンに BombEvent が無い。BombEvent を置いてからもう一度実行する。", "OK");
            return;
        }

        Scene scene = bombEvent.gameObject.scene;
        Transform before = EnemyPhaseSwitcher.FindRootInScene(scene, EnemyPhaseSwitcher.BEFORE_ROOT_NAME);
        Transform after = EnemyPhaseSwitcher.FindRootInScene(scene, EnemyPhaseSwitcher.AFTER_ROOT_NAME);

        report.AppendLine(before != null
            ? $"・{EnemyPhaseSwitcher.BEFORE_ROOT_NAME}: {before.name} (敵 {before.GetComponentsInChildren<EnemyHealth>(true).Length} 体)"
            : $"・{EnemyPhaseSwitcher.BEFORE_ROOT_NAME} が見つからない。マップの中に同じ名前の空オブジェクトを作り、敵を入れる。");

        report.AppendLine(after != null
            ? $"・{EnemyPhaseSwitcher.AFTER_ROOT_NAME}: {after.name} (敵 {after.GetComponentsInChildren<EnemyHealth>(true).Length} 体)"
            : $"・{EnemyPhaseSwitcher.AFTER_ROOT_NAME} が見つからない。マップの中に同じ名前の空オブジェクトを作り、敵を入れる。");

        EnemyPhaseSwitcher switcher = Object.FindFirstObjectByType<EnemyPhaseSwitcher>(FindObjectsInactive.Include);

        if (switcher == null)
        {
            switcher = Undo.AddComponent<EnemyPhaseSwitcher>(bombEvent.gameObject);
            report.AppendLine($"・{bombEvent.name} に EnemyPhaseSwitcher を付けた。");
        }
        else
        {
            Undo.RecordObject(switcher, "Setup Enemy Phase Switcher");
            report.AppendLine($"・EnemyPhaseSwitcher は {switcher.name} に付いている。参照だけ埋め直した。");
        }

        switcher.SetReferences(bombEvent, before, after);
        EditorUtility.SetDirty(switcher);

        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine($"・{scene.name} を変更した。保存 (Ctrl+S) を忘れずに。");

        EditorUtility.DisplayDialog(DIALOG_TITLE, report.ToString(), "OK");
    }
}
