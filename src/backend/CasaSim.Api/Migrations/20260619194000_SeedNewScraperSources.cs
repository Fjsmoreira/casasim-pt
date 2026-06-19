using System;
using CasaSim.Api;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace CasaSim.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260619194000_SeedNewScraperSources")]
public partial class SeedNewScraperSources : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var now = "2026-06-11 00:00:00+00";

        // Only insert if the scraper_key doesn't already exist (idempotent)
        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000004', 'Valorfin Imóveis', 'Valorfin Imóveis', 'valorfin-imoveis', 'https://valorfinimoveis.pt', 'CRM360 platform – Valorfin Imóveis', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000005', 'Argilipe', 'Argilipe', 'argilipe', 'https://www.argilipe.pt', 'CRM360 platform – Argilipe Imobiliária', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000006', 'ImoPombal', 'ImoPombal', 'imopombal', 'https://www.imopombal.pt', 'eGO platform – ImoPombal', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000007', 'LionsCastles', 'LionsCastles', 'lionscastles', 'https://www.lionscastles.pt', 'eGO platform – LionsCastles', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000008', 'Habifit', 'Habifit', 'habifit', 'https://www.habifit.pt', 'eGO platform – Habifit', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000009', 'Cosy Imobiliária', 'Cosy Imobiliária', 'cosy-imobiliaria', 'https://www.cosyimobiliaria.pt', 'eGO platform – Cosy Imobiliária', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000010', 'Moderno Imóveis', 'Moderno Imóveis', 'moderno-imoveis', 'https://www.modernoimoveis.pt', 'WordPress – Moderno Imóveis', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000011', 'Neves & Terlouw', 'Neves & Terlouw', 'neves-terlouw', 'https://www.nevesterlouw.com', 'Custom site – Neves & Terlouw', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000012', 'Veigas', 'Veigas', 'veigas', 'https://www.veigas.eu', 'Next.js – Veigas Imobiliária', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);

        migrationBuilder.Sql($$"""
            INSERT INTO scraper_source (id, name, scraper_key, agency_slug, source_url, target_description, enabled, interval, created_at, updated_at)
            VALUES ('b1000000-0000-0000-0000-000000000013', 'Zome', 'Zome', 'zome', 'https://www.zome.pt/pt/leiria-h40157/imoveis', 'Nuxt/Vue – Zome Leiria district', true, '6 hours', '{{now}}', '{{now}}')
            ON CONFLICT (scraper_key) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM scraper_source WHERE id >= 'b1000000-0000-0000-0000-000000000004' AND id <= 'b1000000-0000-0000-0000-000000000013';");
    }
}
