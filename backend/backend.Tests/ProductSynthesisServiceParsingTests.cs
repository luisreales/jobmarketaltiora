using System.Reflection;
using backend.Infrastructure.Services;
using Xunit;

namespace backend.Tests;

public class ProductSynthesisServiceParsingTests
{
    [Fact]
    public void ParsePlan_ShouldAcceptJsonInsideFencesAndIgnoreTrailingText()
    {
        var raw = """
            Aqui tienes el plan solicitado.
            ```json
            {
              "implementacion": "Paso 1. Discovery. Paso 2. MVP.",
              "requerimientos": "Accesos, owners y alcance.",
              "tiempo_y_tecnologias": "2 semanas con .NET y Azure.",
              "empresas_objetivo": "Acme, Globex"
            }
            ```
            Nota: adaptar por industria.
            """;

        var result = InvokeParsePlan(raw);
        var implementacion = result.GetType().GetProperty("Implementacion")?.GetValue(result) as string;
        var requerimientos = result.GetType().GetProperty("Requerimientos")?.GetValue(result) as string;

        Assert.Equal("Paso 1. Discovery. Paso 2. MVP.", implementacion);
        Assert.Equal("Accesos, owners y alcance.", requerimientos);
    }

    [Fact]
    public void ParsePlan_ShouldThrowInvalidOperationException_WhenJsonIsTruncated()
    {
        var raw = """
            {
              "implementacion": "Paso 1. Discovery. Paso 2. MVP.",
              "requerimientos": "Accesos, owners y alcance.",
              "tiempo_y_tecnologias": "2 semanas con .NET y Azure.
            """;

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeParsePlan(raw));

        var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("LLM response was not valid JSON", inner.Message);
    }

    private static object InvokeParsePlan(string raw)
    {
        var method = typeof(ProductSynthesisService)
            .GetMethod("ParsePlan", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return method!.Invoke(null, [raw])!;
    }
}
