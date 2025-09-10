using UnityEngine;

public class GameSystem : MonoBehaviour
{
    [Header("ゲーム設定")]
    public ObjectRenderer objectRenderer; // オブジェクトレンダラー
    public float focusRange = 2f; // ピント調整の範囲
    public int targetFocus = 50; // 正解のピント値（0-100）
    public float keepTime = 5f; // クリアに必要な維持時間
    
    [Header("ピント設定")]
    public int currentFocus = 50; // 現在のピント値（0-100）
    public float smoothSpeed = 5f; // マウスストーカーの追従速度
    
    [Header("パフォーマンス設定")]
    public float updateInterval = 0.2f; // 画像更新間隔（秒）- 中間値
    public int focusChangeThreshold = 3; // 更新する最小ピント変化量 - 中間値
    public bool enableImageProcessing = true; // 画像処理のON/OFF
    
    [Header("FPS表示設定")]
    public bool showFPS = true; // FPS表示のON/OFF
    public float fpsUpdateInterval = 0.5f; // FPS更新間隔（秒）
    
    private float correctFocusTime = 0f; // 正解ピントを維持している時間
    private bool gameCleared = false;
    private float lastUpdateTime = 0f; // 最後に更新した時間
    private int lastFocus = 50; // 前回のピント値
    
    // FPS計測用
    private float fps = 0f;
    private float lastFPSUpdateTime = 0f;
    private int frameCount = 0;
    
    // ピント誤差範囲
    private int focusTolerance = 3;
    
    void Start()
    {        
        Debug.Log("ゲーム開始！マウスを上下に動かしてピントを合わせよう！");
        Debug.Log($"目標ピント値: {targetFocus} (±{focusTolerance})");
        
        // 初期状態の画像を生成
        lastFocus = currentFocus;
        lastUpdateTime = Time.time;
        UpdateObjectImage();
        
        // FPS計測初期化
        lastFPSUpdateTime = Time.time;
        frameCount = 0;
    }
    
    void Update()
    {
        if (gameCleared) return;
        
        // マウス位置からピント値を計算（マウスストーカー風）
        UpdateFocusFromMouse();
        
        // 条件を満たした場合のみオブジェクトの画像を更新
        UpdateObjectImageConditional();
        
        // FPS計測
        UpdateFPS();
        
        // クリア判定
        CheckClearCondition();
        
        // デバッグ表示
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"現在のピント: {currentFocus} / 目標: {targetFocus} / 維持時間: {correctFocusTime:F1}秒");
            Debug.Log($"FPS: {fps:F1} / 更新間隔: {updateInterval}秒 / 変化閾値: {focusChangeThreshold}");
            Debug.Log($"最終更新からの時間: {Time.time - lastUpdateTime:F2}秒");
        }
        
        // テスト用：完全一致状態を強制作成
        if (Input.GetKeyDown(KeyCode.P))
        {
            currentFocus = targetFocus;
            Debug.Log($"強制的にピント一致: currentFocus = targetFocus = {targetFocus}");
            UpdateObjectImage(); // 即座に更新
        }
    }
    
    void UpdateFocusFromMouse()
    {
        // マウスのY座標を0-100のピント値に変換
        float mouseY = Input.mousePosition.y;
        float screenHeight = Screen.height;
        int targetFocusFromMouse = Mathf.RoundToInt((mouseY / screenHeight) * 100f);
        targetFocusFromMouse = Mathf.Clamp(targetFocusFromMouse, 0, 100);
        
        // スムーズに追従
        currentFocus = Mathf.RoundToInt(Mathf.Lerp(currentFocus, targetFocusFromMouse, smoothSpeed * Time.deltaTime));
        currentFocus = Mathf.Clamp(currentFocus, 0, 100);
    }
    
    void UpdateObjectImageConditional()
    {
        if (objectRenderer == null || !enableImageProcessing) return;
        
        // 時間による更新制限
        bool timeCondition = Time.time - lastUpdateTime >= updateInterval;
        
        // ピント変化による更新条件
        bool focusChangeCondition = Mathf.Abs(currentFocus - lastFocus) >= focusChangeThreshold;
        
        // いずれかの条件を満たした場合のみ更新
        if (timeCondition || focusChangeCondition)
        {
            Debug.Log($"画像更新実行 - Time: {timeCondition}, Focus: {focusChangeCondition}, FPS: {fps:F1}");
            UpdateObjectImage();
            lastUpdateTime = Time.time;
            lastFocus = currentFocus;
        }
    }
    
    void UpdateObjectImage()
    {
        if (objectRenderer == null) return;
        
        // ピントのずれ量を計算（0が完全、1が最大ずれ）
        float focusError = Mathf.Abs(currentFocus - targetFocus) / 100f;
        
        // デバッグ情報
        Debug.Log($"ピント計算: currentFocus={currentFocus}, targetFocus={targetFocus}, focusError={focusError:F3}");
        
        // ObjectRendererに分離率を送信
        objectRenderer.UpdateCompositeImage(focusError);
    }
    
    void UpdateFPS()
    {
        frameCount++;
        
        if (Time.time - lastFPSUpdateTime >= fpsUpdateInterval)
        {
            fps = frameCount / (Time.time - lastFPSUpdateTime);
            frameCount = 0;
            lastFPSUpdateTime = Time.time;
        }
    }
    
    void CheckClearCondition()
    {
        // 正解ピント範囲内かチェック
        bool isCorrectFocus = Mathf.Abs(currentFocus - targetFocus) <= focusTolerance;
        
        if (isCorrectFocus)
        {
            correctFocusTime += Time.deltaTime;
            
            if (correctFocusTime >= keepTime)
            {
                GameClear();
            }
        }
        else
        {
            correctFocusTime = 0f; // リセット
        }
    }
    
    void GameClear()
    {
        gameCleared = true;
        Debug.Log("ゲームクリア！おめでとうございます！");
        
        // クリア時の効果（画像を正常状態に戻す）
        if (objectRenderer != null)
        {
            objectRenderer.UpdateCompositeImage(0f);
        }
    }
    
    void OnGUI()
    {
        // FPS表示
        if (showFPS)
        {
            // FPS背景
            GUI.Box(new Rect(10, 10, 120, 25), "");
            
            // FPS値の色を決定
            GUIStyle fpsStyle = new GUIStyle(GUI.skin.label);
            if (fps >= 60)
                fpsStyle.normal.textColor = Color.green;
            else if (fps >= 30)
                fpsStyle.normal.textColor = Color.yellow;
            else
                fpsStyle.normal.textColor = Color.red;
            
            GUI.Label(new Rect(15, 15, 110, 20), $"FPS: {fps:F1}", fpsStyle);
        }
        
        // ゲーム情報表示
        int yOffset = showFPS ? 45 : 10;
        GUI.Box(new Rect(10, yOffset, 200, 120), "");
        GUI.Label(new Rect(20, yOffset + 10, 180, 20), $"ピント: {currentFocus}/100");
        GUI.Label(new Rect(20, yOffset + 30, 180, 20), $"目標: {targetFocus}±{focusTolerance}");
        GUI.Label(new Rect(20, yOffset + 50, 180, 20), $"維持時間: {correctFocusTime:F1}/{keepTime}秒");
        
        // ピントずれ情報を追加
        float currentFocusError = Mathf.Abs(currentFocus - targetFocus) / 100f;
        GUI.Label(new Rect(20, yOffset + 70, 180, 20), $"ずれ量: {currentFocusError:F3}");
        GUI.Label(new Rect(20, yOffset + 90, 180, 20), $"分離率: {currentFocusError:F1}%");
        
        if (gameCleared)
        {
            GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 25, 200, 50), "");
            GUI.Label(new Rect(Screen.width / 2 - 80, Screen.height / 2 - 10, 160, 20), "ゲームクリア！");
        }
        
        // 操作説明
        int instructionY = Screen.height - 80;
        GUI.Label(new Rect(10, instructionY, 400, 20), "SPACEキー: デバッグ情報表示");
        GUI.Label(new Rect(10, instructionY + 20, 400, 20), "Pキー: 強制ピント一致テスト");
        GUI.Label(new Rect(10, instructionY + 40, 400, 20), "T/Rキー: テスト用分離調整");
    }
}
