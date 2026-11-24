using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>
/// Textos de los diagnósticos. El texto base está en inglés, tal cual aparece en el código; cuando la cultura de la
/// interfaz es español se busca la traducción en <see cref="Es"/> (la clave es el texto en inglés). Si falta una
/// traducción se muestra el inglés, nunca un error. Traducir un texto nuevo es añadir una línea a <c>Loc.Es.cs</c>.
/// </summary>
internal static partial class Loc
{
    /// <summary>Traduce <paramref name="english"/> a la cultura de la interfaz actual.</summary>
    public static string T(string english) => T(english, null);

    /// <summary>Traduce <paramref name="english"/> a <paramref name="culture"/> (<c>null</c>: cultura de la interfaz actual).</summary>
    public static string T(string english, IFormatProvider? culture) =>
        IsSpanish(culture) && Es.TryGetValue(english, out var spanish) ? spanish : english;

    /// <summary>Traduce la plantilla y le aplica <see cref="string.Format(string, object[])"/>.</summary>
    public static string F(string english, params object?[] args) => string.Format(T(english), args);

    /// <summary>Texto localizable para título y mensaje de un <see cref="DiagnosticDescriptor"/>.</summary>
    public static LocalizableString Text(string english) => new Localized(english);

    // Los textos armados en el código no reciben la cultura del compilador (solo lo hace el descriptor), así que sin
    // proveedor se usa la cultura de la interfaz del proceso: el IDE y el compilador ponen en ella el idioma del usuario.
#pragma warning disable RS1035
    private static bool IsSpanish(IFormatProvider? provider)
    {
        var culture = provider as CultureInfo ?? CultureInfo.CurrentUICulture;
        return culture.TwoLetterISOLanguageName == "es";
    }
#pragma warning restore RS1035

    private sealed class Localized : LocalizableString
    {
        private readonly string _english;

        public Localized(string english) => _english = english;

        protected override string GetText(IFormatProvider? formatProvider) => T(_english, formatProvider);

        protected override int GetHash() => _english.GetHashCode();

        protected override bool AreEqual(object? other) => other is Localized text && text._english == _english;
    }
}
