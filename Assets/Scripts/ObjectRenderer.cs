using UnityEngine;

public enum BlendMode { Additive, Multiply, Alpha }

public class ObjectRenderer : MonoBehaviour
{
    [Header("オブジェクト設定")]
    public Texture2D sourceImage; // 元画像
    public SpriteRenderer targetRenderer; // 表示用レンダラー

    [Header("配置設定")]
    [Header("配置設定")]
    public float maxSeparationDistance = 100f; // 最大間隔（ピクセル）- 調整可能

    [Header("重ね合わせ設定")]
    public BlendMode blendMode = BlendMode.Alpha; // 重ね合わせモード

    [Header("ブラー設定")]
    public int blurRadiusBase = 2; // ブラーの強度（軽量化のため小さく）
    public bool useFastBlur = true; // 高速ブラーを使用

    [Header("最適化設定")]
    public bool enableBlur = true; // ブラー処理のON/OFF
    public int maxImageSize = 512; // 最大画像サイズ（ピクセル）

    private Texture2D compositeTexture;
    private Sprite compositeSprite;
    private float lastSeparationRatio = -1f; // 前回の分離率（キャッシュ用）

    void Start()
    {
        Debug.Log("ObjectRenderer Start() 開始");

        if (sourceImage != null)
        {
            Debug.Log($"Source Image が設定されています: {sourceImage.name}");
            // 初期状態（間隔0）で画像を生成
            UpdateCompositeImage(0f);
        }
        else
        {
            Debug.LogWarning("Source Image が設定されていません");
        }

        if (targetRenderer == null)
        {
            Debug.LogWarning("Target Renderer が設定されていません");
        }
        else
        {
            Debug.Log($"Target Renderer が設定されています: {targetRenderer.name}");
        }
    }

    void Update()
    {
        // テスト用：スペースキーを押すと強制的に分離を大きくする
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("テスト: 最大分離で画像を生成");
            UpdateCompositeImage(1.0f);  // 最大分離
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("テスト: 分離なしで画像を生成");
            UpdateCompositeImage(0.0f);  // 分離なし
        }
    }
    public void UpdateCompositeImage(float separationRatio)
    {
        // 同じ値の場合は処理をスキップ（キャッシュ）
        if (Mathf.Approximately(separationRatio, lastSeparationRatio))
        {
            Debug.Log($"キャッシュ使用: separationRatio = {separationRatio}");
            return;
        }

        Debug.Log($"UpdateCompositeImage 呼び出し: separationRatio = {separationRatio}");

        if (sourceImage == null)
        {
            Debug.LogWarning("Source Image が null です");
            return;
        }

        // 画像サイズを制限してパフォーマンス向上
        Texture2D processImage = sourceImage;
        bool needsResize = sourceImage.width > maxImageSize || sourceImage.height > maxImageSize;

        if (needsResize)
        {
            Debug.Log($"画像をリサイズ: {sourceImage.width}x{sourceImage.height} → 制限サイズ");
            processImage = ResizeTexture(sourceImage, maxImageSize);
        }

        // 間隔を計算（0.0 = 重なり、1.0 = 最大間隔）
        float separation = separationRatio * maxSeparationDistance;
        Debug.Log($"計算された間隔: {separation} ピクセル");

        // 合成画像を生成
        compositeTexture = CreateCompositeImage(processImage, separation);
        Debug.Log($"合成画像作成完了: {compositeTexture.width} x {compositeTexture.height}");

        // ブラー処理（オプション）
        Texture2D finalTexture = compositeTexture;
        // 分離率×基準値を四捨五入し、0〜blurRadiusBaseにクランプ
        int blurRadius = Mathf.Clamp(Mathf.RoundToInt(separationRatio * blurRadiusBase), 0, blurRadiusBase);
        if (enableBlur && blurRadius > 0)
        {
            if (useFastBlur)
            {
                finalTexture = ApplyFastBlur(compositeTexture, blurRadius);
                Debug.Log($"高速ブラー処理完了: {finalTexture.width} x {finalTexture.height}");
            }
            else
            {
                finalTexture = ApplyBlur(compositeTexture, blurRadius);
                Debug.Log($"通常ブラー処理完了: {finalTexture.width} x {finalTexture.height}");
            }
        }
        else
        {
            Debug.Log("ブラー処理をスキップ");
        }

        // Spriteを作成・更新
        if (compositeSprite != null)
        {
            DestroyImmediate(compositeSprite);
        }
        compositeSprite = CreateSpriteFromTexture(finalTexture);
        Debug.Log($"Sprite作成完了: {compositeSprite.name}");

        // SpriteRendererに設定
        if (targetRenderer != null)
        {
            targetRenderer.sprite = compositeSprite;
            Debug.Log($"SpriteRenderer に設定完了: {targetRenderer.sprite.name}");
        }
        else
        {
            Debug.LogWarning("Target Renderer が null です");
        }

        // 古いテクスチャを削除
        if (compositeTexture != finalTexture)
        {
            DestroyImmediate(compositeTexture);
        }

        // リサイズした画像を削除
        if (processImage != sourceImage)
        {
            DestroyImmediate(processImage);
        }

        // キャッシュ更新
        lastSeparationRatio = separationRatio;
    }

    Texture2D CreateCompositeImage(Texture2D source, float separation)
    {
        // テクスチャが読み取り可能でない場合は読み取り可能なコピーを作成
        Texture2D readableSource = source;
        if (!source.isReadable)
        {
            Debug.LogWarning($"テクスチャ '{source.name}' が読み取り不可能です。読み取り可能なコピーを作成します。");
            readableSource = MakeTextureReadable(source);
        }

        // 合成画像のサイズ（元画像幅 + 分離距離、両端に余裕を持たせる）
        int compositeWidth = readableSource.width + Mathf.RoundToInt(separation) * 2;
        int compositeHeight = readableSource.height;

        Debug.Log($"元画像サイズ: {readableSource.width} x {readableSource.height}");
        Debug.Log($"間隔: {separation} ピクセル");
        Debug.Log($"合成画像サイズ: {compositeWidth} x {compositeHeight}");

        // 新しいテクスチャを作成
        Texture2D composite = new Texture2D(compositeWidth, compositeHeight, TextureFormat.RGBA32, false);

        // 背景を透明で塗りつぶし
        Color[] compositePixels = new Color[compositeWidth * compositeHeight];
        for (int i = 0; i < compositePixels.Length; i++)
        {
            compositePixels[i] = Color.clear;
        }

        // 元画像のピクセルを取得
        Color[] sourcePixels = readableSource.GetPixels();

        // 中央基準位置を計算
        int centerX = compositeWidth / 2;

        // 左側のオブジェクトを配置（中央から左に separation/2 離れた位置）
        int leftStartX = centerX - Mathf.RoundToInt(separation / 2f) - readableSource.width / 2;
        Debug.Log($"左側オブジェクトを配置中... 開始X座標: {leftStartX}");

        BlendPixels(compositePixels, sourcePixels, readableSource.width, readableSource.height,
                   compositeWidth, compositeHeight, leftStartX, 0);

        // 右側のオブジェクトを配置（中央から右に separation/2 離れた位置）
        int rightStartX = centerX + Mathf.RoundToInt(separation / 2f) - readableSource.width / 2;
        Debug.Log($"右側オブジェクトを配置中... 開始X座標: {rightStartX}");

        BlendPixels(compositePixels, sourcePixels, readableSource.width, readableSource.height,
                   compositeWidth, compositeHeight, rightStartX, 0);

        // 最終的にテクスチャに設定
        composite.SetPixels(compositePixels);

        composite.Apply();

        // 一時的なコピーを削除
        if (readableSource != source)
        {
            DestroyImmediate(readableSource);
        }

        Debug.Log("合成画像作成完了");
        return composite;
    }

    Texture2D MakeTextureReadable(Texture2D source)
    {
        // RenderTextureを使用してテクスチャを読み取り可能にする
        RenderTexture renderTex = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, renderTex);

        RenderTexture previousRT = RenderTexture.active;
        RenderTexture.active = renderTex;

        Texture2D readableTexture = new Texture2D(source.width, source.height);
        readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableTexture.Apply();

        RenderTexture.active = previousRT;
        RenderTexture.ReleaseTemporary(renderTex);

        return readableTexture;
    }

    Texture2D ResizeTexture(Texture2D source, int maxSize)
    {
        int newWidth = source.width;
        int newHeight = source.height;

        // アスペクト比を保ったまま最大サイズに制限
        if (newWidth > maxSize || newHeight > maxSize)
        {
            float scale = Mathf.Min((float)maxSize / newWidth, (float)maxSize / newHeight);
            newWidth = Mathf.RoundToInt(newWidth * scale);
            newHeight = Mathf.RoundToInt(newHeight * scale);
        }

        RenderTexture renderTex = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, renderTex);

        RenderTexture previousRT = RenderTexture.active;
        RenderTexture.active = renderTex;

        Texture2D resizedTexture = new Texture2D(newWidth, newHeight);
        resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resizedTexture.Apply();

        RenderTexture.active = previousRT;
        RenderTexture.ReleaseTemporary(renderTex);

        return resizedTexture;
    }

    void BlendPixels(Color[] compositePixels, Color[] sourcePixels, int sourceWidth, int sourceHeight,
                     int compositeWidth, int compositeHeight, int offsetX, int offsetY)
    {
        for (int y = 0; y < sourceHeight; y++)
        {
            for (int x = 0; x < sourceWidth; x++)
            {
                int targetX = offsetX + x;
                int targetY = offsetY + y;

                // 範囲チェック
                if (targetX >= 0 && targetX < compositeWidth && targetY >= 0 && targetY < compositeHeight)
                {
                    int sourceIndex = y * sourceWidth + x;
                    int targetIndex = targetY * compositeWidth + targetX;

                    Color sourceColor = sourcePixels[sourceIndex];
                    Color currentColor = compositePixels[targetIndex];

                    // 透明部分はスキップ
                    if (sourceColor.a > 0.01f)
                    {
                        Color blendedColor;

                        switch (blendMode)
                        {
                            case BlendMode.Additive:
                                blendedColor = new Color(
                                    Mathf.Clamp01(currentColor.r + sourceColor.r),
                                    Mathf.Clamp01(currentColor.g + sourceColor.g),
                                    Mathf.Clamp01(currentColor.b + sourceColor.b),
                                    Mathf.Clamp01(currentColor.a + sourceColor.a)
                                );
                                break;

                            case BlendMode.Multiply:
                                blendedColor = new Color(
                                    currentColor.r * sourceColor.r + sourceColor.r * (1 - currentColor.a),
                                    currentColor.g * sourceColor.g + sourceColor.g * (1 - currentColor.a),
                                    currentColor.b * sourceColor.b + sourceColor.b * (1 - currentColor.a),
                                    Mathf.Clamp01(currentColor.a + sourceColor.a)
                                );
                                break;

                            case BlendMode.Alpha:
                            default:
                                // 通常のアルファブレンド
                                float alpha = sourceColor.a + currentColor.a * (1 - sourceColor.a);
                                if (alpha > 0)
                                {
                                    blendedColor = new Color(
                                        (sourceColor.r * sourceColor.a + currentColor.r * currentColor.a * (1 - sourceColor.a)) / alpha,
                                        (sourceColor.g * sourceColor.a + currentColor.g * currentColor.a * (1 - sourceColor.a)) / alpha,
                                        (sourceColor.b * sourceColor.a + currentColor.b * currentColor.a * (1 - sourceColor.a)) / alpha,
                                        alpha
                                    );
                                }
                                else
                                {
                                    blendedColor = Color.clear;
                                }
                                break;
                        }

                        compositePixels[targetIndex] = blendedColor;
                    }
                }
            }
        }
    }

    Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    Texture2D ApplyBlur(Texture2D source, int radius)
    {
        // 元のテクスチャをコピー（読み書き可能に）
        Texture2D blurred = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Color[] sourcePixels = source.GetPixels();
        Color[] blurredPixels = new Color[sourcePixels.Length];

        int width = source.width;
        int height = source.height;

        // シンプルなボックスブラーを適用
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color avgColor = Color.clear;
                int samples = 0;

                // 周囲のピクセルを平均化
                for (int oy = -radius; oy <= radius; oy++)
                {
                    for (int ox = -radius; ox <= radius; ox++)
                    {
                        int sampleX = Mathf.Clamp(x + ox, 0, width - 1);
                        int sampleY = Mathf.Clamp(y + oy, 0, height - 1);

                        avgColor += sourcePixels[sampleY * width + sampleX];
                        samples++;
                    }
                }

                blurredPixels[y * width + x] = avgColor / samples;
            }
        }

        blurred.SetPixels(blurredPixels);
        blurred.Apply();

        return blurred;
    }

    Texture2D ApplyFastBlur(Texture2D source, int radius)
    {
        // 高速ブラー：ダウンサンプリング → ブラー → アップサンプリング
        int downScale = Mathf.Max(1, radius / 2); // ダウンスケール率
        int smallWidth = source.width / downScale;
        int smallHeight = source.height / downScale;

        // ダウンサンプリング
        Texture2D smallTexture = new Texture2D(smallWidth, smallHeight, TextureFormat.RGBA32, false);
        Color[] sourcePixels = source.GetPixels();
        Color[] smallPixels = new Color[smallWidth * smallHeight];

        for (int y = 0; y < smallHeight; y++)
        {
            for (int x = 0; x < smallWidth; x++)
            {
                // 近傍のピクセルを平均化してダウンサンプリング
                int sourceX = x * downScale;
                int sourceY = y * downScale;

                if (sourceX < source.width && sourceY < source.height)
                {
                    smallPixels[y * smallWidth + x] = sourcePixels[sourceY * source.width + sourceX];
                }
            }
        }
        smallTexture.SetPixels(smallPixels);
        smallTexture.Apply();

        // 小さな画像に軽量ブラーを適用
        Texture2D blurredSmall = ApplySimpleBlur(smallTexture, 1); // 小さな半径で十分

        // アップサンプリング
        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Color[] blurredPixels = blurredSmall.GetPixels();
        Color[] resultPixels = new Color[source.width * source.height];

        for (int y = 0; y < source.height; y++)
        {
            for (int x = 0; x < source.width; x++)
            {
                int smallX = Mathf.Clamp(x / downScale, 0, smallWidth - 1);
                int smallY = Mathf.Clamp(y / downScale, 0, smallHeight - 1);

                resultPixels[y * source.width + x] = blurredPixels[smallY * smallWidth + smallX];
            }
        }

        result.SetPixels(resultPixels);
        result.Apply();

        // 一時テクスチャを削除
        DestroyImmediate(smallTexture);
        DestroyImmediate(blurredSmall);

        return result;
    }

    Texture2D ApplySimpleBlur(Texture2D source, int radius)
    {
        // シンプルな3x3ブラー（高速）
        Texture2D blurred = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Color[] sourcePixels = source.GetPixels();
        Color[] blurredPixels = new Color[sourcePixels.Length];

        int width = source.width;
        int height = source.height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color avgColor = Color.clear;
                int samples = 0;

                // 3x3の範囲のみ処理（高速化）
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sampleX = Mathf.Clamp(x + ox, 0, width - 1);
                        int sampleY = Mathf.Clamp(y + oy, 0, height - 1);

                        avgColor += sourcePixels[sampleY * width + sampleX];
                        samples++;
                    }
                }

                blurredPixels[y * width + x] = avgColor / samples;
            }
        }

        blurred.SetPixels(blurredPixels);
        blurred.Apply();

        return blurred;
    }

    // 外部から画像を変更する場合
    public void SetSourceImage(Texture2D newImage)
    {
        sourceImage = newImage;
        if (sourceImage != null)
        {
            UpdateCompositeImage(0f);
        }
    }
}
