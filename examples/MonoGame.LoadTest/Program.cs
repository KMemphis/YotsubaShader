using Examples;
using Examples.Shaders;
using YotsubaShader;

// Carga el efecto de DesktopGL generado por ToMGFX en un GraphicsDevice real de MonoGame.
using var game = new EffectLoadTest(new TriangleShader().ToMGFX(MonoGamePlatform.DesktopGL));
game.Run();

Console.WriteLine($"MonoGame DesktopGL -> {game.Report}");
return game.Passed ? 0 : 1;
