using GlooApiGateway;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication()
              .AddJwtBearer("SSO", options =>
              {
                  //Its better to read the values form appsetting
                  options.Audience = "Auc-API";
                  options.Authority = "http://orderapigateway.westus.cloudapp.azure.com/identitymgmt/realms/AucRealm";
                  options.MapInboundClaims = false;

                  //since we are running KeyClock on http 
                  options.RequireHttpsMetadata = false;

                  //you can use following for add or remove criteria from validaion process
                  options.TokenValidationParameters = new TokenValidationParameters
                  {
                      //ValidateAudience = false,

                      RoleClaimType = "role",  //Sets which claim should be used for user roles.
                      NameClaimType = "preferred_username"  //Defines which claim should be used for the username.
                  };
              });

builder.Services.AddAuthorizationBuilder()
                            .AddPolicy(AuthorizationPolicies.Admin, builder =>
                            {
                                //Check the Role
                                builder.RequireRole("Admin");

                                //builder.RequireClaim("scope", "Auc.FullAccess");
                                //instead of following you can write 
                                builder.RequireAssertion(context =>
                                {
                                    return context.User.Claims.Any(claim =>
                                        claim.Type == "scope" && claim.Value.Split(' ').Contains("Auc.FullAccess"));
                                });

                            });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
