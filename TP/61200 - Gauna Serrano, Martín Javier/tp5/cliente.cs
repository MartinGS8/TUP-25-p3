using System;                     // Console, etc.
using System.Linq;                // OrderBy
using System.Net.Http;            // HttpClient, StringContent ✔
using System.Text.Json;           // JsonSerializer, JsonNamingPolicy
using System.Threading.Tasks;     // Task

var baseUrl = "http://localhost:5000";
var http = new HttpClient();
var jsonOpt = new JsonSerializerOptions {
    PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

// Codigo de ejemplo: Reemplazar por la implementacion real 

async Task<List<Producto>> TraerAsync(){
    var json = await http.GetStringAsync($"{baseUrl}/productos");
    return JsonSerializer.Deserialize<List<Producto>>(json, jsonOpt)!;
}

Console.WriteLine("=== Productos ===");
foreach (var p in await TraerAsync()) {
    Console.WriteLine($"{p.Id} {p.Nombre,-20} {p.Precio,15:c}");
}
class Producto {
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public decimal Precio { get; set; }
}

// Fin del ejemplo// Fin del ejemplo
#r "nuget: System.Net.Http.Json"

using System.Net.Http;
using System.Net.Http.Json;

HttpClient http = new HttpClient();
http.BaseAddress = new Uri("http://localhost:5000");

while (true)
{
    Console.WriteLine("\n--- Menú ---");
    Console.WriteLine("1. Listar productos");
    Console.WriteLine("2. Listar productos con bajo stock");
    Console.WriteLine("3. Agregar stock a un producto");
    Console.WriteLine("4. Quitar stock a un producto");
    Console.WriteLine("0. Salir");

    Console.Write("Opción: ");
    var opcion = Console.ReadLine();

    switch (opcion)
    {
        case "1":
            var productos = await http.GetFromJsonAsync<List<Producto>>("/productos");
            Mostrar(productos);
            break;
        case "2":
            var bajos = await http.GetFromJsonAsync<List<Producto>>("/productos/bajo-stock");
            Mostrar(bajos);
            break;
        case "3":
            Console.Write("ID producto: ");
            int idAdd = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Cantidad a agregar: ");
            int cantAdd = int.Parse(Console.ReadLine() ?? "0");
            var respAdd = await http.PostAsync($"/productos/{idAdd}/agregar?cantidad={cantAdd}", null);
            Console.WriteLine(await respAdd.Content.ReadAsStringAsync());
            break;
        case "4":
            Console.Write("ID producto: ");
            int idQuitar = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Cantidad a quitar: ");
            int cantQuitar = int.Parse(Console.ReadLine() ?? "0");
            var respQuitar = await http.PostAsync($"/productos/{idQuitar}/quitar?cantidad={cantQuitar}", null);
            Console.WriteLine(await respQuitar.Content.ReadAsStringAsync());
            break;
        case "0":
            return;
        default:
            Console.WriteLine("Opción no válida");
            break;
    }
}

void Mostrar(List<Producto>? productos)
{
    if (productos == null || productos.Count == 0)
    {
        Console.WriteLine("No hay productos.");
        return;
    }
    foreach (var p in productos)
        Console.WriteLine($"ID: {p.Id} | {p.Nombre} | Stock: {p.Stock}");
}

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int Stock { get; set; }
}
