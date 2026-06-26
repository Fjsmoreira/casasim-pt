using System.Net;
using System.Text;
using CasaSim.Core.Models;
using CasaSim.Scraper.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasaSim.Scraper.Tests;

public sealed class Crm360ParserTests
{
    [Fact]
    public async Task ScrapeSiteAsync_UsesPreviewImagesAndVisibleDescription()
    {
        using var http = new HttpClient(new ValorfinFixtureHandler())
        {
            BaseAddress = new Uri("https://valorfinimoveis.pt")
        };

        var properties = await Crm360Parser.ScrapeSiteAsync(
            "https://valorfinimoveis.pt",
            "Valorfin Imóveis",
            http,
            NullLogger.Instance,
            CancellationToken.None);

        var property = Assert.Single(properties);
        Assert.Equal("MOR-MOR_265", property.ExternalId);
        Assert.Equal("Moradia T2", property.Title);
        Assert.Equal(PropertyType.House, property.Type);
        Assert.Equal(PropertyStatus.Sold, property.Status);
        Assert.Equal("Leiria", property.District);
        Assert.Equal("Leiria", property.City);
        Assert.Equal(2, property.Bedrooms);
        Assert.Equal(0, property.Bathrooms);
        Assert.Equal(150, property.AreaM2);
        Assert.Equal(950, property.LandAreaM2);
        Assert.Equal(1996, property.YearBuilt);
        Assert.Contains("excelente oportunidade de investimento", property.Description);
        Assert.DoesNotContain("<br", property.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, property.Images.Count);
        Assert.All(property.Images, image =>
            Assert.StartsWith("https://images.crm360.pt/imoveis/3o9llv/foto_marca_agua/", image));
    }

    private sealed class ValorfinFixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri?.PathAndQuery ?? "";

            var content = pathAndQuery switch
            {
                "/Imoveis?page=1" => """
                    <html><body>
                        <a href="/Imovel/3o9llv">Moradia T2</a>
                    </body></html>
                    """,
                "/Imovel/3o9llv" => """
                    <html>
                    <head>
                        <title>Moradia T2</title>
                        <meta name="description" content="Weak metadata">
                        <meta property="og:image" content="https://images.crm360.pt/imoveis/3o9llv/tn/thumb.jpg">
                    </head>
                    <body>
                        <span class="imovs_title_price">Moradia T2</span>
                        <span class="imovs_negocio imov_detail_negocio">Comprar</span>
                        <span class="imovs_negocio sell">Vendido</span>
                        <p class="imovs_place_ref">Ref. MOR-MOR_265</p>
                        <a class="open-popup-link" data-id="3o9llv">
                            <img src="https://images.crm360.pt/imoveis/3o9llv/foto_marca_agua/main.jpg">
                        </a>
                        <ul class="detail_imovs_indications">
                            <li><p>2 Quartos</p></li>
                            <li><p>0 WC</p></li>
                            <li><p>150,00 m²</p><p>Área útil</p></li>
                        </ul>
                        <span class="detail_imovs_description">
                            Moradia T2 para Reabilitar<br>
                            Apresentamos esta excelente oportunidade de investimento.
                        </span>
                        <div class="another_details">Distrito: Leiria</div>
                        <div class="another_details">Concelho: Leiria</div>
                        <div class="another_details">Ano de construção: 1996</div>
                        <div class="another_details">Área do terreno: 950,00 m²</div>
                        <div class="another_details">Área bruta: 150,00 m²</div>
                    </body>
                    </html>
                    """,
                "/imovel/preview/images?id=3o9llv" => """
                    [
                        "https://images.crm360.pt/imoveis/3o9llv/foto_marca_agua/main.jpg",
                        "https://images.crm360.pt/imoveis/3o9llv/foto_marca_agua/second.jpg",
                        "https://images.crm360.pt/imoveis/3o9llv/foto_marca_agua/third.jpg"
                    ]
                    """,
                _ => ""
            };

            var statusCode = string.IsNullOrEmpty(content) ? HttpStatusCode.NotFound : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/html")
            });
        }
    }
}
