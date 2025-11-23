using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Tester.Services;

class Utilities
{
  public Utilities(WebApplication app, Toolkit.Types.ILogger logger)
  {
    Order order = new Order
    {
      Items = new List<Item>
      {
        new Item { Price = 10, Product = new Product { Name = "A" }},
        new Item { Price = 20, Product = new Product { Name = "B" }},
        new Item { Price = 30, Product = new Product { Name = "C" }},
      },
    };

    app.MapGet("/utilities/getByPath/{path}", async ([FromRoute] string path) =>
    {
      logger.Log(LogLevel.Warning, null, "Started processing the GET request to /utilities/getByPath/{path}");


      return Results.Ok($"Result: {JsonConvert.SerializeObject(Toolkit.Utilities.GetByPath(order, path))}");
    });
  }
}

public class Order
{
  public required List<Item> Items { get; set; }
}

public class Item
{
  public required int Price { get; set; }
  public required Product Product { get; set; }
}

public class Product
{
  public required string Name { get; set; }
}