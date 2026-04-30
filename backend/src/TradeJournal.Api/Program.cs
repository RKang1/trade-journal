using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TradeJournal.Api;
using TradeJournal.Data;
using TradeJournal.Services;
using TradeJournal.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
	});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddTradeJournalServices(builder.Configuration);

builder.Services
	.AddAuthentication(options =>
	{
		options.DefaultAuthenticateScheme = TradeJournalAuthentication.DynamicBearerScheme;
		options.DefaultChallengeScheme = TradeJournalAuthentication.DynamicBearerScheme;
	})
	.AddPolicyScheme(TradeJournalAuthentication.DynamicBearerScheme, "Trade Journal bearer auth", options =>
	{
		options.ForwardDefaultSelector = TradeJournalAuthentication.SelectScheme;
	})
	.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme)
	.AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(TradeJournalAuthentication.ApiTokenScheme, _ => { });

builder.Services
	.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
	.Configure<IOptions<AuthOptions>>((jwt, authOptions) =>
	{
		var auth = authOptions.Value;
		if (string.IsNullOrWhiteSpace(auth.JwtSigningKey))
		{
			throw new InvalidOperationException("Auth:JwtSigningKey is not configured.");
		}
		jwt.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = auth.JwtIssuer,
			ValidateAudience = true,
			ValidAudience = auth.JwtAudience,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.JwtSigningKey)),
			ClockSkew = TimeSpan.FromSeconds(30),
		};
	});

builder.Services.AddAuthorization(options =>
{
	options.AddPolicy(TradeJournalAuthentication.InteractiveUserPolicy, policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.RequireAssertion(context =>
			!context.User.HasClaim(claim => claim.Type == AuthClaimTypes.ApiTokenId));
	});
});

builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
			?? new[] { "http://localhost:4200" };
		policy.WithOrigins(origins)
			.AllowAnyHeader()
			.AllowAnyMethod();
	});
});

builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<TradeJournalDbContext>();
	if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
	{
		await db.Database.MigrateAsync();
	}
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
