using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeJournal.Data.Migrations
{
	/// <inheritdoc />
	public partial class InitialCreate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "users",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					GoogleSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
					Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
					DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
					CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_users", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "trades",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					UserId = table.Column<Guid>(type: "uuid", nullable: false),
					Symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
					Side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
					Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
					EntryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					EntryPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
					Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
					ExitAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					ExitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
					Fees = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
					Setup = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
					Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
					RealizedPnl = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
					CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_trades", x => x.Id);
					table.ForeignKey(
						name: "FK_trades_users_UserId",
						column: x => x.UserId,
						principalTable: "users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_trades_UserId_EntryAt",
				table: "trades",
				columns: new[] { "UserId", "EntryAt" });

			migrationBuilder.CreateIndex(
				name: "IX_users_GoogleSubject",
				table: "users",
				column: "GoogleSubject",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "trades");

			migrationBuilder.DropTable(
				name: "users");
		}
	}
}
