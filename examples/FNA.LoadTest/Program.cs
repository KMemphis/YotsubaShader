using Examples;
using Examples.Shaders;
using YotsubaShader;

// Carga el efecto generado por ToFNA_FX (fx_2_0) en un GraphicsDevice real de FNA.
using var game = new EffectLoadTest(new TriangleShader().ToFNA_FX());
game.Run();

Console.WriteLine($"FNA -> {game.Report}");
return game.Passed ? 0 : 1;
