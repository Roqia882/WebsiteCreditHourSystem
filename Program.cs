using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebsiteCreditHoursSystem.Models;
using WebsiteCreditHoursSystem.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<EmailService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Credit System", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "الصق التوكن فقط بدون كلمة Bearer لأن Swagger يضيفها تلقائياً"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Use the legacy JwtSecurityTokenHandler while debugging invalid_token behavior.
        options.UseSecurityTokenValidators = true;
        options.SecurityTokenValidators.Clear();
        options.SecurityTokenValidators.Add(new JwtSecurityTokenHandler());

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            NameClaimType = "UserID",
          
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();

                if (string.IsNullOrWhiteSpace(authHeader))
                {
                    Console.WriteLine("AUTH DEBUG: Authorization header is missing.");
                }
                else
                {
                    Console.WriteLine($"AUTH DEBUG: Authorization header present. Prefix={GetHeaderPreview(authHeader)}");

                    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var extractedToken = authHeader["Bearer ".Length..].Trim();

                        // Normalize accidental double-prefix cases like "Bearer Bearer eyJ..."
                        if (extractedToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            extractedToken = extractedToken["Bearer ".Length..].Trim();
                        }

                        // Normalize common copy/paste artifacts from JSON responses.
                        extractedToken = extractedToken.Trim().Trim('"', '\'', ',', ';');

                        context.Token = extractedToken;

                        var tokenParts = extractedToken.Split('.', StringSplitOptions.RemoveEmptyEntries);
                        var canReadToken = new JsonWebTokenHandler().CanReadToken(extractedToken);
                        Console.WriteLine(
                            $"AUTH DEBUG: Extracted token. Length={extractedToken.Length}, Segments={tokenParts.Length}, CanRead={canReadToken}, Start={GetTokenPreview(extractedToken, true)}, End={GetTokenPreview(extractedToken, false)}"
                        );
                    }
                    else
                    {
                        Console.WriteLine("AUTH DEBUG: Authorization header does not start with 'Bearer '.");
                    }
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("ERROR: " + context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine(" Token Valid!");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"AUTH DEBUG: Challenge triggered. Error={context.Error}, Description={context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// هنخلي السواجر يشتغل في كل الحالات عشان تقدري تجربي من اللينك
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Website Credit System V1");
    // السطر اللي جاي ده مهم جداً عشان يفتح السواجر أول ما تفتحي اللينك مباشرة
    c.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static string GetHeaderPreview(string headerValue)
{
    const int previewLength = 24;

    if (headerValue.Length <= previewLength)
        return headerValue;

    return headerValue[..previewLength] + "...";
}

static string GetTokenPreview(string tokenValue, bool fromStart)
{
    const int previewLength = 12;

    if (string.IsNullOrEmpty(tokenValue))
        return "<empty>";

    if (tokenValue.Length <= previewLength)
        return tokenValue;

    return fromStart
        ? tokenValue[..previewLength] + "..."
        : "..." + tokenValue[^previewLength..];
}
