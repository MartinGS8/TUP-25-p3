using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using cliente;
using cliente.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configurar el HttpClient para apuntar al servidor API
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5184") });

// Registrar el servicio API
builder.Services.AddScoped<ApiService>();

await builder.Build().RunAsync();
var builder = WebApplication.CreateBuilder(args);

// Agregar servicios CORS para permitir solicitudes desde el cliente
builder.Services.AddCors(options => {
    options.AddPolicy("AllowClientApp", policy => {
        policy.WithOrigins("http://localhost:5177", "https://localhost:7221")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Agregar controladores si es necesario
builder.Services.AddControllers();

var app = builder.Build();

// Configurar el pipeline de solicitudes HTTP
if (app.Environment.IsDevelopment()) {
    app.UseDeveloperExceptionPage();
}

// Usar CORS con la política definida
app.UseCors("AllowClientApp");

// Mapear rutas básicas
app.MapGet("/", () => "Servidor API está en funcionamiento");

// Ejemplo de endpoint de API
app.MapGet("/api/datos", () => new { Mensaje = "Datos desde el servidor", Fecha = DateTime.Now });

app.Run();

// Program.cs


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TiendaDb>(opt => opt.UseSqlite("Data Source=tienda.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Middleware
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Inicialización de la BD
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TiendaDb>();
    db.Database.EnsureCreated();
}

// Endpoints
app.MapGet("/productos", async (TiendaDb db, string? q) =>
{
    return await db.Productos
        .Where(p => string.IsNullOrEmpty(q) || p.Nombre.Contains(q))
        .ToListAsync();
});

app.MapPost("/carritos", () => Results.Ok(Guid.NewGuid().ToString()));

app.MapGet("/carritos/{id}", async (TiendaDb db, string id) =>
{
    var items = await db.ItemsCarrito
        .Where(i => i.CarritoId == id)
        .Include(i => i.Producto)
        .ToListAsync();

    return new
    {
        Id = id,
        Items = items.Select(i => new
        {
            i.ProductoId,
            i.Producto.Nombre,
            i.Cantidad,
            PrecioUnitario = i.Producto.Precio
        })
    };
});

app.MapPut("/carritos/{id}/{productoId}", async (TiendaDb db, string id, int productoId, int? cambio) =>
{
    var producto = await db.Productos.FindAsync(productoId);
    if (producto is null) return Results.NotFound();

    var item = await db.ItemsCarrito.FirstOrDefaultAsync(i => i.CarritoId == id && i.ProductoId == productoId);
    int cantidadCambio = cambio ?? 1;

    if (item is null)
    {
        if (producto.Stock < cantidadCambio) return Results.BadRequest("Sin stock suficiente");
        db.ItemsCarrito.Add(new ItemCarrito { CarritoId = id, ProductoId = productoId, Cantidad = cantidadCambio });
        producto.Stock -= cantidadCambio;
    }
    else
    {
        if (producto.Stock < cantidadCambio) return Results.BadRequest("Sin stock disponible");
        item.Cantidad += cantidadCambio;
        producto.Stock -= cantidadCambio;
    }

    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/carritos/{id}/{productoId}", async (TiendaDb db, string id, int productoId) =>
{
    var item = await db.ItemsCarrito.FirstOrDefaultAsync(i => i.CarritoId == id && i.ProductoId == productoId);
    if (item is null) return Results.NotFound();

    var producto = await db.Productos.FindAsync(productoId);
    producto!.Stock += item.Cantidad;
    db.ItemsCarrito.Remove(item);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/carritos/{id}", async (TiendaDb db, string id) =>
{
    var items = await db.ItemsCarrito.Where(i => i.CarritoId == id).ToListAsync();
    foreach (var item in items)
    {
        var producto = await db.Productos.FindAsync(item.ProductoId);
        if (producto != null) producto.Stock += item.Cantidad;
    }
    db.ItemsCarrito.RemoveRange(items);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPut("/carritos/{id}/confirmar", async (TiendaDb db, string id, ClienteDto cliente) =>
{
    var items = await db.ItemsCarrito.Where(i => i.CarritoId == id).Include(i => i.Producto).ToListAsync();
    if (items.Count == 0) return Results.BadRequest("El carrito está vacío");

    var compra = new Compra
    {
        Fecha = DateTime.Now,
        NombreCliente = cliente.Nombre,
        ApellidoCliente = cliente.Apellido,
        EmailCliente = cliente.Email,
        Total = items.Sum(i => i.Cantidad * i.Producto.Precio),
        Items = items.Select(i => new ItemCompra
        {
            ProductoId = i.ProductoId,
            Cantidad = i.Cantidad,
            PrecioUnitario = i.Producto.Precio
        }).ToList()
    };

    db.Compras.Add(compra);
    db.ItemsCarrito.RemoveRange(items);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.Run();


// Modelos.cs


namespace Servidor
{
    public class TiendaDb : DbContext
    {
        public TiendaDb(DbContextOptions<TiendaDb> opt) : base(opt) { }

        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<ItemCarrito> ItemsCarrito => Set<ItemCarrito>();
        public DbSet<Compra> Compras => Set<Compra>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Producto>().HasData(GenerarProductosIniciales());
        }

        private static List<Producto> GenerarProductosIniciales() => new()
        {
            new Producto { Id = 1, Nombre = "Smartphone X", Descripcion = "Pantalla AMOLED", Precio = 999, Stock = 10, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 2, Nombre = "Auriculares Pro", Descripcion = "Cancelación de ruido", Precio = 199, Stock = 15, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 3, Nombre = "Cargador Rápido", Descripcion = "Carga de 45W", Precio = 49, Stock = 20, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 4, Nombre = "Tablet 10\"", Descripcion = "Alta resolución", Precio = 599, Stock = 8, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 5, Nombre = "Laptop Gamer", Descripcion = "RTX 3060", Precio = 1500, Stock = 5, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 6, Nombre = "Monitor 27\"", Descripcion = "4K UHD", Precio = 300, Stock = 7, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 7, Nombre = "Teclado Mecánico", Descripcion = "RGB", Precio = 120, Stock = 12, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 8, Nombre = "Mouse Inalámbrico", Descripcion = "Precisión alta", Precio = 60, Stock = 10, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 9, Nombre = "Silla Ergonómica", Descripcion = "Diseño cómodo", Precio = 220, Stock = 6, ImagenUrl = "https://via.placeholder.com/150" },
            new Producto { Id = 10, Nombre = "Disco SSD 1TB", Descripcion = "Alta velocidad", Precio = 130, Stock = 14, ImagenUrl = "https://via.placeholder.com/150" },
        };
    }

    public class Producto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
    }

    public class ItemCarrito
    {
        public int Id { get; set; }
        public string CarritoId { get; set; } = string.Empty;
        public int ProductoId { get; set; }
        public Producto Producto { get; set; } = null!;
        public int Cantidad { get; set; }
    }

    public class Compra
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string ApellidoCliente { get; set; } = string.Empty;
        public string EmailCliente { get; set; } = string.Empty;
        public List<ItemCompra> Items { get; set; } = new();
    }

    public class ItemCompra
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public int CompraId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }

    public class ClienteDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
} 
