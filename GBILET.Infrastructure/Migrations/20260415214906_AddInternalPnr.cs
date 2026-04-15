using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GBILET.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalPnr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Airlines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NameTr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Alliance = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDomestic = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airlines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Airports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IataCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IcaoCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    NameTr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CityTr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CityEn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CountryTr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CountryEn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CityCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsCity = table.Column<bool>(type: "boolean", nullable: false),
                    IsDomestic = table.Column<bool>(type: "boolean", nullable: false),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FareTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NameTr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FareTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuestSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PopularRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DestinationCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DisplayPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PopularRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerNumber = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    FareTypeId = table.Column<int>(type: "integer", nullable: false),
                    NameTr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRefundable = table.Column<bool>(type: "boolean", nullable: false),
                    IsChangeable = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingClasses_FareTypes_FareTypeId",
                        column: x => x.FareTypeId,
                        principalTable: "FareTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BiletBankFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PNR = table.Column<string>(type: "text", nullable: true),
                    InternalPnr = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    IsFinalized = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Origin = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Destination = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    AirlineCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    FlightNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SessionToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductItemId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ServiceFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OurCommission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BookedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TicketedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    AdultCount = table.Column<int>(type: "integer", nullable: false),
                    ChildCount = table.Column<int>(type: "integer", nullable: false),
                    InfantCount = table.Column<int>(type: "integer", nullable: false),
                    GuestSessionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_GuestSessions_GuestSessionId",
                        column: x => x.GuestSessionId,
                        principalTable: "GuestSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BiletBankSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BiletBankSessionToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShoppingFileId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SystemLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<string>(type: "text", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TripName = table.Column<string>(type: "text", nullable: true),
                    TripType = table.Column<string>(type: "text", nullable: false),
                    Origin = table.Column<string>(type: "text", nullable: false),
                    Destination = table.Column<string>(type: "text", nullable: false),
                    DepartureDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalPassengers = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillingInfo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingName = table.Column<string>(type: "text", nullable: false),
                    TaxNo = table.Column<string>(type: "text", nullable: true),
                    TaxOffice = table.Column<string>(type: "text", nullable: true),
                    AddressCity = table.Column<string>(type: "text", nullable: true),
                    AddressDistrict = table.Column<string>(type: "text", nullable: true),
                    AddressDetail = table.Column<string>(type: "text", nullable: true),
                    AddressZipCode = table.Column<string>(type: "text", nullable: true),
                    CountryCode = table.Column<string>(type: "text", nullable: true),
                    IsCompany = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingInfo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingInfo_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<string>(type: "text", nullable: true),
                    SessionToken = table.Column<string>(type: "text", nullable: true),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    RequestBody = table.Column<string>(type: "text", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingLogs_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FareDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseFare = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    BiletBankCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    OurPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Profit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FareDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FareDetails_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlightSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    MarketingAirline = table.Column<string>(type: "text", nullable: false),
                    OperatingAirline = table.Column<string>(type: "text", nullable: true),
                    FlightNumber = table.Column<string>(type: "text", nullable: false),
                    OriginCode = table.Column<string>(type: "text", nullable: false),
                    DestinationCode = table.Column<string>(type: "text", nullable: false),
                    DepartureDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DepartureTime = table.Column<string>(type: "text", nullable: true),
                    ArrivalDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ArrivalTime = table.Column<string>(type: "text", nullable: true),
                    BookingClass = table.Column<string>(type: "text", nullable: true),
                    Cabin = table.Column<string>(type: "text", nullable: true),
                    FareBasis = table.Column<string>(type: "text", nullable: true),
                    Baggage = table.Column<string>(type: "text", nullable: true),
                    TicketNumber = table.Column<string>(type: "text", nullable: true),
                    Equipment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightSegments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Passengers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    BirthDate = table.Column<string>(type: "text", nullable: false),
                    CitizenNo = table.Column<string>(type: "text", nullable: true),
                    PassportNo = table.Column<string>(type: "text", nullable: true),
                    PassportCountry = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    HesCode = table.Column<string>(type: "text", nullable: true),
                    TempTag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaxReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TicketNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Passengers_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CardHolderName = table.Column<string>(type: "text", nullable: true),
                    MaskedCardNumber = table.Column<string>(type: "text", nullable: true),
                    InstallmentCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CardLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    CardHolder = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Is3DSecure = table.Column<bool>(type: "boolean", nullable: false),
                    RedirectUrl = table.Column<string>(type: "text", nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SearchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Origin = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Destination = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DepartureDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FlightType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    FlightClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AdultCount = table.Column<int>(type: "integer", nullable: false),
                    ChildCount = table.Column<int>(type: "integer", nullable: false),
                    InfantCount = table.Column<int>(type: "integer", nullable: false),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    MinPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: true),
                    HasError = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchLogs_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripBookings_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripBookings_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripPassengers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    PassengerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPassengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripPassengers_Passengers_PassengerId",
                        column: x => x.PassengerId,
                        principalTable: "Passengers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripPassengers_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Airlines",
                columns: new[] { "Id", "Alliance", "Code", "CountryCode", "CreatedAt", "IsActive", "IsDomestic", "LogoUrl", "NameEn", "NameTr", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Star Alliance", "TK", "TR", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(49), true, true, "/assets/airlines/tk.png", "Turkish Airlines", "Türk Hava Yolları", 1, null },
                    { 2, null, "PC", "TR", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(52), true, true, "/assets/airlines/pc.png", "Pegasus Airlines", "Pegasus Hava Yolları", 2, null },
                    { 3, null, "VF", "TR", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(54), true, true, "/assets/airlines/vf.png", "AnadoluJet", "AnadoluJet", 3, null },
                    { 4, null, "XQ", "TR", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(55), true, true, "/assets/airlines/xq.png", "SunExpress", "SunExpress", 4, null },
                    { 5, null, "XC", "TR", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(57), true, true, "/assets/airlines/xc.png", "Corendon Airlines", "Corendon Airlines", 5, null },
                    { 6, "Star Alliance", "LH", "DE", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(58), true, false, "/assets/airlines/lh.png", "Lufthansa", "Lufthansa", 10, null },
                    { 7, "Oneworld", "BA", "GB", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(59), true, false, "/assets/airlines/ba.png", "British Airways", "British Airways", 11, null },
                    { 8, "SkyTeam", "AF", "FR", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(61), true, false, "/assets/airlines/af.png", "Air France", "Air France", 12, null },
                    { 9, null, "EK", "AE", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(62), true, false, "/assets/airlines/ek.png", "Emirates", "Emirates", 13, null },
                    { 10, "Oneworld", "QR", "QA", new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(64), true, false, "/assets/airlines/qr.png", "Qatar Airways", "Qatar Airways", 14, null }
                });

            migrationBuilder.InsertData(
                table: "FareTypes",
                columns: new[] { "Id", "Code", "IsActive", "NameEn", "NameTr", "SortOrder" },
                values: new object[,]
                {
                    { 1, "ECO", true, "Economy", "Ekonomi", 1 },
                    { 2, "PEF", true, "Premium Economy", "Premium Ekonomi", 2 },
                    { 3, "BUS", true, "Business", "Business", 3 },
                    { 4, "FIR", true, "First Class", "First Class", 4 }
                });

            migrationBuilder.InsertData(
                table: "PopularRoutes",
                columns: new[] { "Id", "CreatedAt", "Currency", "DestinationCode", "DisplayPrice", "IsActive", "OriginCode", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(468), "TRY", "AYT", 899.00m, true, "IST", 1, null },
                    { 2, new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(472), "TRY", "ADB", 749.00m, true, "IST", 2, null },
                    { 3, new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(473), "TRY", "TZX", 699.00m, true, "IST", 3, null },
                    { 4, new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(475), "TRY", "IST", 599.00m, true, "ESB", 4, null },
                    { 5, new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(476), "TRY", "BJV", 949.00m, true, "IST", 5, null },
                    { 6, new DateTime(2026, 4, 15, 21, 49, 5, 969, DateTimeKind.Utc).AddTicks(478), "TRY", "AYT", 799.00m, true, "ESB", 6, null }
                });

            migrationBuilder.InsertData(
                table: "BookingClasses",
                columns: new[] { "Id", "Code", "FareTypeId", "IsChangeable", "IsRefundable", "NameEn", "NameTr", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Y", 1, true, true, "Economy Full", "Ekonomi Full", 1 },
                    { 2, "M", 1, true, true, "Economy Flexible", "Ekonomi Esnek", 2 },
                    { 3, "S", 1, true, false, "Economy Restricted", "Ekonomi Kısıtlı", 3 },
                    { 4, "V", 1, false, false, "Economy Discounted", "Ekonomi İndirimli", 4 },
                    { 5, "C", 3, true, true, "Business Full", "Business Full", 5 },
                    { 6, "J", 3, true, true, "Business Discounted", "Business İndirimli", 6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Airlines_Code",
                table: "Airlines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Airports_CountryCode",
                table: "Airports",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_IataCode",
                table: "Airports",
                column: "IataCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Airports_IsDomestic",
                table: "Airports",
                column: "IsDomestic");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_IsPopular",
                table: "Airports",
                column: "IsPopular");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInfo_BookingId",
                table: "BillingInfo",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingClasses_Code",
                table: "BookingClasses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingClasses_FareTypeId",
                table: "BookingClasses",
                column: "FareTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingLogs_BookingId",
                table: "BookingLogs",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingLogs_CreatedAt",
                table: "BookingLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BookingLogs_Operation",
                table: "BookingLogs",
                column: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_BookingLogs_UserId",
                table: "BookingLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_GuestSessionId",
                table: "Bookings",
                column: "GuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PNR",
                table: "Bookings",
                column: "PNR");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status",
                table: "Bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId",
                table: "Bookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FareDetails_BookingId",
                table: "FareDetails",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_FareTypes_Code",
                table: "FareTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightSegments_BookingId",
                table: "FlightSegments",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestSessions_Email",
                table: "GuestSessions",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Passengers_BookingId",
                table: "Passengers",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchLogs_CreatedAt",
                table: "SearchLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SearchLogs_SessionId",
                table: "SearchLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                table: "Sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_Action",
                table: "SystemLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_CreatedAt",
                table: "SystemLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_UserId",
                table: "SystemLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_BookingId",
                table: "TripBookings",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_TripId",
                table: "TripBookings",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPassengers_PassengerId",
                table: "TripPassengers",
                column: "PassengerId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPassengers_TripId",
                table: "TripPassengers",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_UserId",
                table: "Trips",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomerNumber",
                table: "Users",
                column: "CustomerNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Airlines");

            migrationBuilder.DropTable(
                name: "Airports");

            migrationBuilder.DropTable(
                name: "BillingInfo");

            migrationBuilder.DropTable(
                name: "BookingClasses");

            migrationBuilder.DropTable(
                name: "BookingLogs");

            migrationBuilder.DropTable(
                name: "FareDetails");

            migrationBuilder.DropTable(
                name: "FlightSegments");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PopularRoutes");

            migrationBuilder.DropTable(
                name: "SearchLogs");

            migrationBuilder.DropTable(
                name: "SystemLogs");

            migrationBuilder.DropTable(
                name: "TripBookings");

            migrationBuilder.DropTable(
                name: "TripPassengers");

            migrationBuilder.DropTable(
                name: "FareTypes");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Passengers");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "GuestSessions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
