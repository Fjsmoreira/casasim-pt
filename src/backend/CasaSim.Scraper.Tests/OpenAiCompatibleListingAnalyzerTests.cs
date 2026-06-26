using CasaSim.Scraper.Services;

namespace CasaSim.Scraper.Tests;

public sealed class OpenAiCompatibleListingAnalyzerTests
{
    [Fact]
    public void ExtractJsonContent_Removes_Markdown_Code_Fence()
    {
        var response = """
            ```json
            {"generatedDescription":"Casa em bom estado"}
            ```
            """;

        var json = OpenAiCompatibleListingAnalyzer.ExtractJsonContent(response);

        Assert.Equal("""{"generatedDescription":"Casa em bom estado"}""", json);
    }

    [Fact]
    public void ExtractJsonContent_Recovers_Object_After_Preamble()
    {
        var response = """
            Aqui esta a analise:
            {"generatedDescription":"Apartamento central"}
            """;

        var json = OpenAiCompatibleListingAnalyzer.ExtractJsonContent(response);

        Assert.Equal("""{"generatedDescription":"Apartamento central"}""", json);
    }
}
