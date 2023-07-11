var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    // Personalizado para suportar alterações
    var pobjConfiguration = builder.Configuration;

    // Obtem o servidor de destino
    var strTarget = pobjConfiguration.GetValue<string>("Deploy:Target");
    if (strTarget == "NGINX")
    {
        // Para funcionar em sub-diretorio
        app.UsePathBase(pobjConfiguration.GetValue<string>("Deploy:UsePathBase"));
    }
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
