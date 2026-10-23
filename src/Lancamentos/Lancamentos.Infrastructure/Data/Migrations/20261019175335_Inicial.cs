using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lancamentos.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lancamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<short>(type: "smallint", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OcorridoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegistradoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegistradoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChaveIdempotencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EstornoDeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lancamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_eventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_eventos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lancamentos_ContaId_ChaveIdempotencia",
                table: "lancamentos",
                columns: new[] { "ContaId", "ChaveIdempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lancamentos_ContaId_OcorridoEm",
                table: "lancamentos",
                columns: new[] { "ContaId", "OcorridoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_lancamentos_EstornoDeId",
                table: "lancamentos",
                column: "EstornoDeId",
                unique: true,
                filter: "\"EstornoDeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_eventos_CriadoEm",
                table: "outbox_eventos",
                column: "CriadoEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lancamentos");

            migrationBuilder.DropTable(
                name: "outbox_eventos");
        }
    }
}
