using UnityEngine;

public class ObjectRenderer : MonoBehaviour
{
    [Header("オブジェクト設定")]
    public Texture2D sourceImage; // 元画像
    public SpriteRenderer targetRenderer; // 表示用レンダラー
    
    [Header("配置設定")]
    public float maxSeparationDistance = 200f; // 最大間隔（ピクセル）
    
    [Header("ブラー設定")]
    public int blurRadius = 5; // ブラーの強度
    
    private Texture2D compositeTexture;
    private Sprite compositeSprite;
    
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
    
    // 外部からピントずれ値を受け取って画像を更新
    public void UpdateCompositeImage(float separationRatio)
    {
        Debug.Log($"UpdateCompositeImage 呼び出し: separationRatio = {separationRatio}");
        
        if (sourceImage == null) 
        {
            Debug.LogWarning("Source Image が null です");
            return;
        }
        
        // 間隔を計算（0.0 = 重なり、1.0 = 最大間隔）
        float separation = separationRatio * maxSeparationDistance;
        Debug.Log($"計算された間隔: {separation} ピクセル");
        
        // 合成画像を生成
        compositeTexture = CreateCompositeImage(sourceImage, separation);
        Debug.Log($"合成画像作成完了: {compositeTexture.width} x {compositeTexture.height}");
        
        // ブラー処理
        Texture2D blurredTexture = ApplyBlur(compositeTexture, blurRadius);
        Debug.Log($"ブラー処理完了: {blurredTexture.width} x {blurredTexture.height}");
        
        // Spriteを作成・更新
        if (compositeSprite != null)
        {
            DestroyImmediate(compositeSprite);
        }
        compositeSprite = CreateSpriteFromTexture(blurredTexture);
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
        if (compositeTexture != blurredTexture)
        {
            DestroyImmediate(compositeTexture);
        }
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
        
        // 合成画像のサイズ（幅は2倍 + 間隔）
        int compositeWidth = readableSource.width * 2 + Mathf.RoundToInt(separation);
        int compositeHeight = readableSource.height;
        
        // 新しいテクスチャを作成
        Texture2D composite = new Texture2D(compositeWidth, compositeHeight, TextureFormat.RGBA32, false);
        
        // 背景を透明で塗りつぶし
        Color[] clearPixels = new Color[compositeWidth * compositeHeight];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = Color.clear;
        }
        composite.SetPixels(clearPixels);
        
        // 元画像のピクセルを取得
        Color[] sourcePixels = readableSource.GetPixels();
        
        // 左側のオブジェクトを配置
        for (int y = 0; y < readableSource.height; y++)
        {
            for (int x = 0; x < readableSource.width; x++)
            {
                int sourceIndex = y * readableSource.width + x;
                int leftIndex = y * compositeWidth + x;
                composite.SetPixel(x, y, sourcePixels[sourceIndex]);
            }
        }
        
        // 右側のオブジェクトを配置
        int rightStartX = readableSource.width + Mathf.RoundToInt(separation);
        for (int y = 0; y < readableSource.height; y++)
        {
            for (int x = 0; x < readableSource.width; x++)
            {
                if (rightStartX + x < compositeWidth)
                {
                    int sourceIndex = y * readableSource.width + x;
                    composite.SetPixel(rightStartX + x, y, sourcePixels[sourceIndex]);
                }
            }
        }
        
        composite.Apply();
        
        // 一時的なコピーを削除
        if (readableSource != source)
        {
            DestroyImmediate(readableSource);
        }
        
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
