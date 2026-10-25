using System.Text.Json.Serialization;
using Growcery.Api.Infrastructure;
using Growcery.Core;
using Growcery.Core.Abstractions;
using Growcery.Data;
using Growcery.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
    {
        // A malformed field already produces its own error; don't add a second, misleading "request is required" one.
        // An empty body is still rejected by [ApiController].
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));

        // Don't leak internal type names from JSON parsing errors to clients.
        options.AllowInputFormatterExceptionMessages = false;
    });
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Growcery API",
        Version = "v1"
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Composition root: the business layer, the data layer that implements its interfaces, and the current user.
builder.Services.AddGrowceryCore();
builder.Services.AddGrowceryData(connectionString);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    // HTTPS ends at the Azure Container Apps ingress, so SameAsRequest wouldn't mark the cookie Secure.
    if (!builder.Environment.IsDevelopment())
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }

    // API clients get a ProblemDetails status code instead of a redirect to a login page.
    options.Events.OnRedirectToLogin = context =>
        WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = context =>
        WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden);

    static Task WriteProblemAsync(HttpContext httpContext, int statusCode)
    {
        httpContext.Response.StatusCode = statusCode;
        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        return problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext }).AsTask();
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.Run();

public partial class Program { }
