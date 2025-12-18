using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TranspoLink.Migrations
{
    /// <inheritdoc />
    public partial class NewDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripStops_RouteStops_RouteStopId",
                table: "TripStops");

            migrationBuilder.AddForeignKey(
                name: "FK_TripStops_RouteStops_RouteStopId",
                table: "TripStops",
                column: "RouteStopId",
                principalTable: "RouteStops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripStops_RouteStops_RouteStopId",
                table: "TripStops");

            migrationBuilder.AddForeignKey(
                name: "FK_TripStops_RouteStops_RouteStopId",
                table: "TripStops",
                column: "RouteStopId",
                principalTable: "RouteStops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
