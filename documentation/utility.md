# Toolkit for Utility functionality

## Package
This service is included in the `PJHToolkit` package.

## How to use
```c#
using Toolkit;

string nodeValue = Utilities.GetByPath(someObject, "node1.node2[1].node3");
```

The `Utilities` static class offers the following functionality:

```c#
public static object? GetByPath(object root, string path);
```

### GetByPath
Searches the provided `root` object for the valuue of the node at `path`.<br>
The `path` argument is expected to be in JSON path schema.<br>
If the provided `path` doesn't exist in the provided `root` object then `null` will be returned.<br><br>
Throws system Exceptions on error.

**Example use**
```c#
using Toolkit;

var order = new Order
{
  Id = "some GUID",
  CustomerId = "some customer id",
  Items = new List<Item>
  {
    new Item { Price = 10, Product = new Product { Name = "A" }},
    new Item { Price = 20, Product = new Product { Name = "B" }},
    new Item { Price = 30, Product = new Product { Name = "C" }},
  }
};

string cstomerId = Utilities.GetByPath(order, "CustomerId"); // some customer id
Item secondItem = Utilities.GetByPath(order, "Items[1]"); // pointer to Item { Price = 20, Product = new Product { Name = "B" }}
Product thirdItemProduct = Utilities.GetByPath(order, "Items[2].Product"); // pointer to new Product { Name = "C" }
string firstItemProductName = Utilities.GetByPath(order, "Items[0].Product.Name"); // C
```