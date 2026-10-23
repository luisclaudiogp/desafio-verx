using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lancamentos.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PosicaoContaEHashRequisicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HashRequisicao",
                table: "lancamentos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "posicoes_contas",
                columns: table => new
                {
                    ContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Saldo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posicoes_contas", x => x.ContaId);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO posicoes_contas ("ContaId", "Saldo")
                SELECT "ContaId", SUM(CASE WHEN "Tipo" = 1 THEN "Valor" ELSE -"Valor" END)
                FROM lancamentos
                GROUP BY "ContaId"
                """);

            migrationBuilder.Sql(
                """
                UPDATE lancamentos SET "HashRequisicao" = upper(encode(sha256(convert_to(
                    CASE WHEN "EstornoDeId" IS NULL
                        THEN 'registro|' || "Tipo"::text
                             || '|' || to_char("Valor", 'FM999999999999990.00')
                             || '|' || to_char("OcorridoEm" AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US') || '0+00:00'
                        ELSE 'estorno|' || replace("EstornoDeId"::text, '-', '')
                    END, 'UTF8')), 'hex'))
                WHERE "HashRequisicao" = ''
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "posicoes_contas");

            migrationBuilder.DropColumn(
                name: "HashRequisicao",
                table: "lancamentos");
        }
    }
}
