using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Examples;

/// <summary>
/// Carga un efecto compilado en un GraphicsDevice real, dibuja un triángulo rojo en un render target
/// y comprueba los píxeles. MonoGame, KNI y FNA exponen la misma API, así que lo comparten.
/// </summary>
public sealed class EffectLoadTest : Game
{
    private const int Size = 64;

    private readonly byte[] _effectBytes;

    public bool Passed { get; private set; }

    public string Report { get; private set; } = "No se ejecutó.";

    public EffectLoadTest(byte[] effectBytes)
    {
        _effectBytes = effectBytes;

        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 128,
            PreferredBackBufferHeight = 128,
        };
    }

    protected override void Draw(GameTime gameTime)
    {
        try
        {
            RunTest();
        }
        catch (Exception exception)
        {
            Passed = false;
            Report = $"FAIL: {exception.GetType().Name}: {exception.Message}";
        }

        Exit();
    }

    private void RunTest()
    {
        using var effect = new Effect(GraphicsDevice, _effectBytes);
        using var target = new RenderTarget2D(
            GraphicsDevice, Size, Size, false, SurfaceFormat.Color, DepthFormat.None);

        Vector4 red = new(1f, 0f, 0f, 1f);
        Vertex[] triangle =
        [
            new(new Vector2(-1f, -1f), red),
            new(new Vector2(1f, -1f), red),
            new(new Vector2(0f, 1f), red),
        ];

        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Black);
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;

        effect.CurrentTechnique.Passes[0].Apply();
        GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, triangle, 0, 1);

        GraphicsDevice.SetRenderTarget(null);

        var pixels = new Color[Size * Size];
        target.GetData(pixels);

        Color center = pixels[(Size / 2) * Size + Size / 2];
        Color corner = pixels[0];

        // El centro cae dentro del triángulo (rojo); la esquina superior izquierda, fuera (negro).
        Passed = center.R > 200 && center.G < 50 && center.B < 50 && corner == Color.Black;
        Report = $"{(Passed ? "PASS" : "FAIL")}: centro={center}, esquina={corner}";
    }

    private readonly struct Vertex(Vector2 position, Vector4 color) : IVertexType
    {
        private static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            new VertexElement(8, VertexElementFormat.Vector4, VertexElementUsage.Color, 0));

        public readonly Vector2 Position = position;
        public readonly Vector4 Color = color;

        VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }
}
