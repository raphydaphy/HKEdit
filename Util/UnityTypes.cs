using AssetsTools.NET;

public static class UnityTypes {
    public const uint Mesh = 0x2b;
    public const uint GameObject = 0x1;
    public const uint Transform = 0x4;
    public const uint MeshRenderer = 0x17;
    public const uint MeshFilter = 0x21;
    public const uint Rigidbody2D = 0x32;
    public const uint CircleCollider2D = 0x3a;
    public const uint PolygonCollider2D = 0x3c;
    public const uint BoxCollider2D = 0x3d;
    public const uint EdgeCollider2D = 0x44;
    public const uint AudioSource = 0x52;
    public const uint Animator = 0x5f;
    public const uint RenderSettings = 0x68;
    public const uint LightmapSettings = 0x9d;
    public const uint ParticleSystem = 0xc6;
    public const uint ParticleSystemRenderer = 0xc7;
    public const uint SpriteRenderer = 0xd4;
    public const uint CanvasRenderer = 0xde;
    public const uint RectTransform = 0xe0;
    public const uint MonoBehaviour = 0x72;

    public const uint MonoScript = 0x73;
    public const uint Texture2D = 0x1c;
    public const uint AudioClip = 0x53;
    public const uint Shader = 0x30;
    public const uint Material = 0x15;
    public const uint AnimationClip = 0x4A;
    
    public static bool IsAsset(AssetFileInfoEx inf) {
        return inf.curFileType == Texture2D || inf.curFileType == Shader || inf.curFileType == AudioClip || inf.curFileType == Material || inf.curFileType == AnimationClip;
    }
}