using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// プレイヤーのアニメーションを、メニュー1回でプロジェクトに組み込むエディタ拡張。
///
///   1. Models/Player の fbx を Generic で取り込み、待機の fbx から Avatar を作って他の fbx に使い回す
///   2. 待機・走り・構えのクリップをループにし、投げは1回だけ流す
///   3. AC_Player (Idle / Run / Kamae / Throw) を作る
///   4. P_Player の仮モデル (testchara) を止め、新しいモデルを置いて AC_Player と PlayerAnimation をつなぐ
///
/// すでにある物は作り直さないので、何度実行しても同じ結果になる。
/// </summary>
public static class PlayerAnimationSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Player Animation";

    private const string MODEL_FOLDER = "Assets/_Project/Models/Player";
    private const string IDLE_PATH = MODEL_FOLDER + "/SK_Player_Idle.fbx";
    private const string RUN_PATH = MODEL_FOLDER + "/A_Player_Run.fbx";
    private const string THROW_PATH = MODEL_FOLDER + "/A_Player_Throw.fbx";
    private const string KAMAE_PATH = MODEL_FOLDER + "/A_Player_Kamae.fbx";

    private const string CONTROLLER_FOLDER = "Assets/_Project/Animations/Player";
    private const string CONTROLLER_PATH = CONTROLLER_FOLDER + "/AC_Player.controller";
    private const string PLAYER_PREFAB_PATH = "Assets/_Project/Prefabs/P_Player.prefab";

    private const string RENDERER_NAME = "Renderer";
    private const string OLD_MODEL_NAME = "testchara";
    private const string NEW_MODEL_NAME = "SK_Player";

    private const string SPEED_PARAMETER = "Speed";
    private const string IS_AIMING_PARAMETER = "IsAiming";
    private const string THROW_PARAMETER = "Throw";

    /// <summary>投げのクリップをどこまで流したら待機へ戻り始めるか (0〜1)。</summary>
    private const float THROW_EXIT_TIME = 0.85f;

    /// <summary>これより速ければ走りにする (m/s)。</summary>
    private const float RUN_SPEED_THRESHOLD = 0.1f;

    /// <summary>アニメーションを切り替えるときに重ねる時間 (秒)。</summary>
    private const float TRANSITION_SECONDS = 0.05f;

    /// <summary>足元を決める部品の名前に含まれる言葉。</summary>
    private const string FOOT_PART_KEYWORD = "boot";

    /// <summary>足元よりこれだけ下までは、足元より下の部品として捨てずに数える (モデルの座標)。</summary>
    private const float FLOOR_TOLERANCE = 0.001f;

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        StringBuilder report = new StringBuilder();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(IDLE_PATH) == null)
        {
            EditorUtility.DisplayDialog("Player Animation Setup", $"{IDLE_PATH} が無い。", "OK");
            return;
        }

        // 待機の fbx を先に取り込み、その Avatar を他の fbx に渡す。
        Avatar avatar = ImportModel(IDLE_PATH, null, true, report);
        ImportModel(RUN_PATH, avatar, true, report);
        ImportModel(KAMAE_PATH, avatar, true, report);

        // 投げは1回だけ流すのでループしない。
        ImportModel(THROW_PATH, avatar, false, report);

        AnimatorController controller = LoadOrCreateController(report);
        SetupPrefab(controller, report);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Player Animation Setup", report.ToString(), "OK");
    }

    /// <summary>
    /// fbx を Generic で取り込み、この fbx の Avatar を返す。
    /// sourceAvatar が null ならこの fbx から Avatar を作る。
    /// </summary>
    private static Avatar ImportModel(string path, Avatar sourceAvatar, bool isLoop, StringBuilder report)
    {
        if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
        {
            report.AppendLine($"・{path} が無いので飛ばした。");
            return null;
        }

        importer.animationType = ModelImporterAnimationType.Generic;

        if (sourceAvatar == null)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
        else
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
        }

        importer.importAnimation = true;

        // ここで1回取り込み直さないと、defaultClipAnimations が古い設定のまま返ってくる。
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
            ? importer.clipAnimations
            : importer.defaultClipAnimations;

        string clipName = System.IO.Path.GetFileNameWithoutExtension(path);

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = isLoop;

            // fbx の中のテイク名はばらばらなので、ファイル名にそろえて探しやすくする。
            if (clips.Length == 1)
            {
                clips[i].name = clipName;
            }
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        string loopText = isLoop ? "ループ" : "1回だけ";
        report.AppendLine($"・{System.IO.Path.GetFileName(path)} を Generic で取り込んだ (クリップ {clips.Length} 個、{loopText})。");

        return AssetDatabase.LoadAssetAtPath<Avatar>(path);
    }

    private static AnimationClip LoadClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        for (int i = 0; i < assets.Length; i++)
        {
            // fbx には Unity が足すプレビュー用のクリップも入っているので除く。
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }

        return null;
    }

    private static AnimatorController LoadOrCreateController(StringBuilder report)
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);

        if (existing != null && HasParameter(existing, THROW_PARAMETER))
        {
            // 作り直さずに、切り替えの時間だけ今の設定にそろえる。
            UpdateTransitionSeconds(existing);
            report.AppendLine($"・AC_Player はすでにある。切り替えの時間を {TRANSITION_SECONDS} 秒にそろえた。");
            return existing;
        }

        // 投げが入る前に作った AC_Player は、作り直して投げを足す。
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(CONTROLLER_PATH);
            report.AppendLine("・投げの無い古い AC_Player を作り直す。");
        }

        if (!AssetDatabase.IsValidFolder(CONTROLLER_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Animations", "Player");
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        controller.AddParameter(SPEED_PARAMETER, AnimatorControllerParameterType.Float);
        controller.AddParameter(IS_AIMING_PARAMETER, AnimatorControllerParameterType.Bool);
        controller.AddParameter(THROW_PARAMETER, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState("Idle", new Vector3(300f, 0f, 0f));
        idle.motion = LoadClip(IDLE_PATH);

        AnimatorState run = machine.AddState("Run", new Vector3(300f, 120f, 0f));
        run.motion = LoadClip(RUN_PATH);

        AnimatorState kamae = machine.AddState("Kamae", new Vector3(600f, 0f, 0f));
        kamae.motion = LoadClip(KAMAE_PATH);

        AnimatorState throwState = machine.AddState("Throw", new Vector3(600f, 120f, 0f));
        throwState.motion = LoadClip(THROW_PATH);

        machine.defaultState = idle;

        AnimatorStateTransition idleToRun = AddTransition(idle, run);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, RUN_SPEED_THRESHOLD, SPEED_PARAMETER);

        AnimatorStateTransition runToIdle = AddTransition(run, idle);
        runToIdle.AddCondition(AnimatorConditionMode.Less, RUN_SPEED_THRESHOLD, SPEED_PARAMETER);

        // 走っていても待機していても、チャージを始めたら構える。
        AddTransition(idle, kamae).AddCondition(AnimatorConditionMode.If, 0f, IS_AIMING_PARAMETER);
        AddTransition(run, kamae).AddCondition(AnimatorConditionMode.If, 0f, IS_AIMING_PARAMETER);

        AnimatorStateTransition kamaeToIdle = AddTransition(kamae, idle);
        kamaeToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, IS_AIMING_PARAMETER);

        // どこからでも投げた瞬間に投げを流す。投げの途中で次を投げたら頭から流し直す。
        AnimatorStateTransition toThrow = machine.AddAnyStateTransition(throwState);
        SetupTransition(toThrow);
        toThrow.duration = 0.05f;
        toThrow.canTransitionToSelf = true;
        toThrow.AddCondition(AnimatorConditionMode.If, 0f, THROW_PARAMETER);

        // 投げ終わったら待機へ。走っていれば待機からすぐ走りに移る。
        AnimatorStateTransition throwToIdle = AddTransition(throwState, idle);
        throwToIdle.hasExitTime = true;
        throwToIdle.exitTime = THROW_EXIT_TIME;

        EditorUtility.SetDirty(controller);
        report.AppendLine("・AC_Player を作った (Idle / Run / Kamae / Throw)。");
        return controller;
    }

    /// <summary>入力で切り替わる遷移 (Exit Time を使わないもの) の時間を TRANSITION_SECONDS にする。</summary>
    private static void UpdateTransitionSeconds(AnimatorController controller)
    {
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        foreach (ChildAnimatorState child in machine.states)
        {
            foreach (AnimatorStateTransition transition in child.state.transitions)
            {
                if (!transition.hasExitTime)
                {
                    transition.duration = TRANSITION_SECONDS;
                }
            }
        }

        EditorUtility.SetDirty(controller);
    }

    private static bool HasParameter(AnimatorController controller, string parameterName)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        SetupTransition(transition);
        return transition;
    }

    /// <summary>入力にすぐ反応させるため、最後まで待たずに短く重ねて切り替える。</summary>
    private static void SetupTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = TRANSITION_SECONDS;
        transition.hasFixedDuration = true;
    }

    private static void SetupPrefab(AnimatorController controller, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);

        if (root == null)
        {
            report.AppendLine($"・プレハブが見つからない: {PLAYER_PREFAB_PATH}");
            return;
        }

        try
        {
            Transform rendererRoot = root.transform.Find(RENDERER_NAME);

            if (rendererRoot == null)
            {
                report.AppendLine($"・P_Player に {RENDERER_NAME} が無い。");
                return;
            }

            Transform model = rendererRoot.Find(NEW_MODEL_NAME);

            if (model == null)
            {
                model = PlaceModel(rendererRoot, report);
            }
            else
            {
                report.AppendLine("・P_Player にはすでに新しいモデルがある。");
            }

            FitModel(root, model, report);
            HideOldModel(rendererRoot, report);

            Animator animator = model.GetComponent<Animator>();

            if (animator == null)
            {
                animator = model.gameObject.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(IDLE_PATH);

            // 移動は CharacterController で行うので、アニメーションでは動かさない。
            animator.applyRootMotion = false;

            PlayerAnimation playerAnimation = root.GetComponent<PlayerAnimation>();

            if (playerAnimation == null)
            {
                playerAnimation = root.AddComponent<PlayerAnimation>();
                report.AppendLine("・P_Player に PlayerAnimation を付けた。");
            }

            SerializedObject serialized = new SerializedObject(playerAnimation);
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.FindProperty("_controller").objectReferenceValue = root.GetComponent<PlayerController>();
            serialized.FindProperty("_thrower").objectReferenceValue = root.GetComponent<PlayerThrower>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);
            report.AppendLine("・P_Player を保存した。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>新しいモデルを置く。大きさと高さは <see cref="FitModel"/> で合わせる。</summary>
    private static Transform PlaceModel(Transform rendererRoot, StringBuilder report)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(IDLE_PATH);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, rendererRoot);
        instance.name = NEW_MODEL_NAME;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        report.AppendLine("・新しいモデルを置いた。");
        return instance.transform;
    }

    /// <summary>
    /// 待機アニメーションの姿勢で測ったモデルの高さと足元を、CharacterController に合わせる。
    /// モデルを置いたときの姿勢 (T ポーズなど) で測ると、再生してアニメーションが動いた瞬間に足が浮くため。
    /// </summary>
    private static void FitModel(GameObject root, Transform model, StringBuilder report)
    {
        if (!root.TryGetComponent(out CharacterController characterController))
        {
            report.AppendLine("・CharacterController が無いので、モデルの大きさは合わせなかった。");
            return;
        }

        if (!TryMeasureIdlePose(out Bounds bounds) || bounds.size.y <= 0.0001f)
        {
            report.AppendLine("・モデルの大きさを測れなかった。SK_Player の Scale と Position は手で合わせる。");
            return;
        }

        float scale = characterController.height / bounds.size.y;
        model.localScale = Vector3.one * scale;

        // CharacterController は Skin Width の分だけ地面から浮くので、その分も下げて足を地面に付ける。
        float bottom = characterController.center.y
            - characterController.height * 0.5f
            - characterController.skinWidth;

        Vector3 position = model.localPosition;
        position.y = bottom - bounds.min.y * scale;
        model.localPosition = position;

        report.AppendLine($"・待機の姿勢で測って、大きさ {scale:0.###} 倍、高さ {position.y:0.###} に合わせた。");
    }

    /// <summary>
    /// 待機アニメーションの最初の姿勢にした仮のモデルを作り、見た目の範囲をモデル自身の座標で返す。
    /// 骨で曲げた後の頂点で測るので、Renderer.bounds より正確になる。
    /// </summary>
    private static bool TryMeasureIdlePose(out Bounds bounds)
    {
        bounds = default;

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(IDLE_PATH);

        if (source == null)
        {
            return false;
        }

        GameObject temp = Object.Instantiate(source);
        temp.hideFlags = HideFlags.HideAndDontSave;
        Mesh baked = new Mesh();
        bool hasBounds = false;
        StringBuilder measureLog = new StringBuilder();
        List<(string Name, float MinY, float MaxY)> parts = new List<(string Name, float MinY, float MaxY)>();

        try
        {
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;

            AnimationClip idleClip = LoadClip(IDLE_PATH);

            if (idleClip != null)
            {
                idleClip.SampleAnimation(temp, 0f);
            }

            // fbx には作業用の板 (pPlane など) が非表示で入っているので、見えている物だけで測る。
            foreach (SkinnedMeshRenderer skinned in temp.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!skinned.enabled || skinned.sharedMesh == null)
                {
                    continue;
                }

                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
                float minY = float.MaxValue;
                float maxY = float.MinValue;

                // 行列でまとめて変換する。左右反転 (Scale がマイナス) の部品は、
                // 位置と回転だけで計算すると上下が逆になり、足元より下にあると誤って測ってしまう。
                foreach (Vector3 vertex in baked.vertices)
                {
                    Vector3 point = toWorld.MultiplyPoint3x4(vertex);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }

                measureLog.AppendLine($"{skinned.name}: y {minY:0.###} 〜 {maxY:0.###}");
                parts.Add((skinned.name, minY, maxY));
            }

            foreach (MeshFilter filter in temp.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null
                    || !filter.TryGetComponent(out MeshRenderer meshRenderer)
                    || !meshRenderer.enabled)
                {
                    continue;
                }

                float minY = float.MaxValue;
                float maxY = float.MinValue;

                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }

                measureLog.AppendLine($"{filter.name}: y {minY:0.###} 〜 {maxY:0.###}");
                parts.Add((filter.name, minY, maxY));
            }

            hasBounds = TryGetBodyRange(parts, out float floorY, out float topY);

            if (hasBounds)
            {
                bounds = new Bounds(new Vector3(0f, (floorY + topY) * 0.5f, 0f), new Vector3(0f, topY - floorY, 0f));
                measureLog.AppendLine($"→ 足元 {floorY:0.###}、頭の上 {topY:0.###} として合わせる。");
            }

            // 足が浮く・沈むときに、どの部品が範囲を広げているか Console で確かめられるようにする。
            Debug.Log($"[{nameof(PlayerAnimationSetupMenu)}] 測った部品 (待機の姿勢、モデルの座標)\n{measureLog}");
        }
        finally
        {
            Object.DestroyImmediate(baked);
            Object.DestroyImmediate(temp);
        }

        return hasBounds;
    }

    /// <summary>
    /// 部品ごとの高さから、足元と頭の上を決める。
    /// 足元は靴 (名前に boot を含む部品) の一番下。靴が無ければ全部品の一番下。
    /// 足元より下にある部品は数えない。左右反転の部品が上下逆の位置で取り込まれていて、
    /// それを数えると背が2倍あると判断してしまうため。
    /// </summary>
    private static bool TryGetBodyRange(
        List<(string Name, float MinY, float MaxY)> parts, out float floorY, out float topY)
    {
        floorY = float.MaxValue;
        topY = float.MinValue;

        if (parts.Count == 0)
        {
            return false;
        }

        foreach ((string name, float minY, float _) in parts)
        {
            if (name.IndexOf(FOOT_PART_KEYWORD, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                floorY = Mathf.Min(floorY, minY);
            }
        }

        if (floorY == float.MaxValue)
        {
            foreach ((string _, float minY, float _) in parts)
            {
                floorY = Mathf.Min(floorY, minY);
            }
        }

        foreach ((string _, float minY, float maxY) in parts)
        {
            if (minY >= floorY - FLOOR_TOLERANCE)
            {
                topY = Mathf.Max(topY, maxY);
            }
        }

        return topY > floorY;
    }

    /// <summary>仮モデルは消さずに非表示にする。戻したいときに Inspector で有効にするだけで済むように。</summary>
    private static void HideOldModel(Transform rendererRoot, StringBuilder report)
    {
        Transform oldModel = rendererRoot.Find(OLD_MODEL_NAME);

        if (oldModel == null || !oldModel.gameObject.activeSelf)
        {
            return;
        }

        oldModel.gameObject.SetActive(false);
        report.AppendLine("・仮モデル testchara を非表示にした。");
    }
}
