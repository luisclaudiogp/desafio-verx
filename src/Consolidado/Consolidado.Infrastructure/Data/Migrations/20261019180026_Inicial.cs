using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consolidado.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos_rejeitados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LancamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RejeitadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_rejeitados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "movimentos",
                columns: table => new
                {
                    LancamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<short>(type: "smallint", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OcorridoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentos", x => x.LancamentoId);
                });

            migrationBuilder.CreateTable(
                name: "saldos_diarios",
                columns: table => new
                {
                    ContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalCreditos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDebitos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saldos_diarios", x => new { x.ContaId, x.Data });
                });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_rejeitados_RejeitadoEm",
                table: "eventos_rejeitados",
                column: "RejeitadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_movimentos_ContaId_OcorridoEm",
                table: "movimentos",
                columns: new[] { "ContaId", "OcorridoEm" })
                .Annotation("Npgsql:IndexInclude", new[] { "Tipo", "Valor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_rejeitados");

            migrationBuilder.DropTable(
                name: "movimentos");

            migrationBuilder.DropTable(
                name: "saldos_diarios");
        }
    }
}
