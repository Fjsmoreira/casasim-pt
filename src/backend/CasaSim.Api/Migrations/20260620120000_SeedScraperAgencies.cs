using CasaSim.Api;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace CasaSim.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260620120000_SeedScraperAgencies")]
public partial class SeedScraperAgencies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        const string now = "2026-06-20 00:00:00+00";
        migrationBuilder.Sql($$"""
            INSERT INTO agency (id, name, slug, website_url, is_active, created_at, updated_at)
            VALUES
              ('a1000000-0000-0000-0000-000000000004', 'Valorfin Imóveis', 'valorfin-imoveis', 'https://valorfinimoveis.pt', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000005', 'Argilipe', 'argilipe', 'https://www.argilipe.pt', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000006', 'ImoPombal', 'imopombal', 'https://www.imopombal.pt', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000007', 'LionsCastles', 'lionscastles', 'https://www.lionscastles.pt', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000008', 'Habifit', 'habifit', 'https://www.habifit.pt', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000009', 'Cosy Imobiliária', 'cosy-imobiliaria', 'https://www.cosyimobiliaria.pt', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000010', 'Moderno Imóveis', 'moderno-imoveis', 'https://www.modernoimoveis.pt', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000011', 'Neves & Terlouw', 'neves-terlouw', 'https://www.nevesterlouw.com', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000012', 'Veigas', 'veigas', 'https://www.veigas.eu', true, '{{now}}', '{{now}}'),
              ('a1000000-0000-0000-0000-000000000013', 'Zome', 'zome', 'https://www.zome.pt', true, '{{now}}', '{{now}}')
            ON CONFLICT (slug) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM agency WHERE slug IN ('valorfin-imoveis', 'argilipe', 'imopombal', 'lionscastles', 'habifit', 'cosy-imobiliaria', 'moderno-imoveis', 'neves-terlouw', 'veigas', 'zome');");
    }
}
