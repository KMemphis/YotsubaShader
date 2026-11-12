using Examples;
using Examples.Shaders;
using YotsubaShader;

// Carga el efecto generado por ToKNIWebGL_FX en un GraphicsDevice real de KNI (SDL2 + OpenGL).
using var game = new EffectLoadTest(new TriangleShader().ToKNIWebGL_FX());
game.Run();

Console.WriteLine($"KNI SDL2.GL -> {game.Report}");
return game.Passed ? 0 : 1;
