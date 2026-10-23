using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lancamentos.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PosicaoComTokenDeConcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // sem alteracao de esquema: o token de concorrencia e mapeado sobre a coluna xmin do PostgreSQL
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // nada a desfazer (ver Up)
        }
    }
}
