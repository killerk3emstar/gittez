namespace Gittez.Api;

// Anonimowy UUID z localStorage przeglądarki. To nie jest uwierzytelnienie:
// identyfikator da się podrobić, ale nie chroni niczego wrażliwego (SPEC §7).
// Wiersz w tabeli sessions powstaje leniwie, przy pierwszym zapisie.
static class Session
{
    public const string HeaderName = "X-Session-Id";

    public static bool TryRead(HttpContext http, out Guid sessionId)
    {
        sessionId = Guid.Empty;

        var raw = http.Request.Headers[HeaderName].ToString();

        return !string.IsNullOrWhiteSpace(raw)
            && Guid.TryParse(raw, out sessionId)
            && sessionId != Guid.Empty;
    }

    public static IResult Missing() => Results.Problem(
        type: "missing-session",
        title: "Brak identyfikatora sesji",
        detail: $"Dodaj nagłówek {HeaderName} z UUID wygenerowanym w przeglądarce.",
        statusCode: StatusCodes.Status400BadRequest);
}
