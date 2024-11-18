using Inventory.Business.App;
using Inventory.Model.APP;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Inventory.Presentaion.App.ViewModel;
using Inventory.Presentation.App.ViewModel;
using static Inventory.Presentaion.App.ViewModel.OrderVM;


namespace Inventory.Presentaion.App.Controllers
{
    public class OrderController : Controller
    {
        private readonly IRepositry<Order> _orderRepo;
        private readonly IRepositry<Product> _productRepo;
        private readonly IRepositry<Customer> _customerRepo;

        public OrderController(IRepositry<Order> orderRepo, IRepositry<Product> productRepo, IRepositry<Customer> customerRepo)
        {
            _orderRepo = orderRepo;
            _productRepo = productRepo;
            _customerRepo = customerRepo;
        }

        // GET: OrderController
        public ActionResult Index()
        {
            var orders = _orderRepo.GetALL();
            var model = orders.Select(order => new OrderViewModel
            {
                OrderID = order.OrderID,
                OrderDate = order.OrderDate,
                // Add null check for Customer and its Name property
                CustomerName = order.Customer != null ? order.Customer.Name : "Unknown Customer",
                TotalAmount = order.TotalAmount,
                Status = order.Status
            }).ToList();

            return View(model);
        }


        // Get details of a specific order
        [HttpGet]
        public IActionResult Details(int id)
        {
            var order = _orderRepo.GetById(id);
            if (order == null)
            {
                return NotFound();
            }

            var model = new OrderDetailsViewModel
            {
                OrderID = order.OrderID,
                OrderDate = order.OrderDate,
                CustomerName = order.Customer?.Name ?? "Unknown Customer", // Using null-conditional operator
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Products = order.OrderProducts?.Select(op => new OrderProductDetails
                {
                    ProductID = op.ProductID,
                    ProductName = op.Product?.Name ?? "Unknown Product", // Using null-conditional operator
                    Quantity = op.Quantity,
                    Price = op.Price
                }).ToList() ?? new List<OrderProductDetails>() // Default to an empty list if OrderProducts is null
            };


            return View(model);
        }

        // Render form for creating a new order
        [HttpGet]
        public IActionResult Create()
        {
            var model = new CreateOrderViewModel
            {
                Customers = _customerRepo.GetALL().ToList(),
                Products = _productRepo.GetALL().ToList(),
                SelectedProducts = _productRepo.GetALL().Select(p => new OrderProductViewModel
                {
                    ProductID = p.ProductID,
                    ProductName = p.Name,
                    IsSelected = false, // Initialize to false
                    Quantity = 0, // Default quantity
                    Price = p.Price // Default price
                }).ToList()
            };

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateOrderViewModel model)
        {
            // Populate the Customers and Products for the view in case of validation failure
            model.Customers = _customerRepo.GetALL().ToList();
            model.Products = _productRepo.GetALL().ToList();

            if (ModelState.IsValid)
            {
                // Filter the selected products based on IsSelected property
                var selectedProducts = model.SelectedProducts
                    .Where(p => p.IsSelected && p.ProductID > 0) // Ensure ProductID is valid
                    .ToList();

                // Debugging line
                Console.WriteLine($"Selected Products Count: {selectedProducts.Count}");

                // Validate that at least one product has been selected
                if (!selectedProducts.Any())
                {
                    ModelState.AddModelError(string.Empty, "At least one product must be selected.");
                    return View(model); // Return the view with validation errors
                }

                // Validate Product IDs
                var existingProductIds = _productRepo.GetALL().Select(p => p.ProductID).ToList();
                var invalidProductIds = selectedProducts.Select(sp => sp.ProductID).Where(id => !existingProductIds.Contains(id)).ToList();
                if (invalidProductIds.Any())
                {
                    // Log the invalid ProductIDs or handle accordingly
                    ModelState.AddModelError(string.Empty, $"Invalid ProductIDs: {string.Join(", ", invalidProductIds)}");
                    return View(model); // Return the view with validation errors
                }

                // Create the new order
                var newOrder = new Order
                {
                    CustomerID = model.CustomerID,
                    OrderDate = model.OrderDate,
                    Status = model.Status,
                    OrderProducts = selectedProducts.Select(p => new OrderProduct
                    {
                        ProductID = p.ProductID,
                        Quantity = p.Quantity,
                        Price = p.Price
                    }).ToList()
                };
                // Calculate Total Amount based on selected products
                newOrder.TotalAmount = selectedProducts.Sum(sp => sp.Price * sp.Quantity);

                // Add the order to the repository and save changes
                _orderRepo.Add(newOrder);
                _orderRepo.save();

                return RedirectToAction("Index");
            }

            // If we reach here, there was a validation error
            return View(model);
        }



        // Render form for editing an existing order
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var order = _orderRepo.GetById(id);
            if (order == null)
            {
                return NotFound();
            }

            // Handle null OrderProducts by using ?? to provide an empty list if it's null
            var orderProducts = order.OrderProducts ?? new List<OrderProduct>();

            var model = new EditOrderViewModel
            {
                OrderID = order.OrderID,
                CustomerID = order.CustomerID,
                OrderDate = order.OrderDate,
                Status = order.Status,
                SelectedProducts = orderProducts.Select(op => new OrderProductViewModel
                {
                    ProductID = op.ProductID,
                    ProductName = op.Product?.Name ?? "Unknown", // Handle possible null references to Product or Product.Name
                    Quantity = op.Quantity,
                    Price = op.Price
                }).ToList(),
                Customers = _customerRepo.GetALL()?.ToList() ?? new List<Customer>(), // Safeguard against null result from GetALL
                Products = _productRepo.GetALL()?.ToList() ?? new List<Product>() // Safeguard against null result from GetALL
            };

            return View(model);
        }

        // Post method for editing an existing order
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EditOrderViewModel model)
        {
            if (ModelState.IsValid)
            {
                var order = _orderRepo.GetById(model.OrderID);
                if (order == null)
                {
                    return NotFound();
                }

                // Update order properties
                order.CustomerID = model.CustomerID;
                order.OrderDate = model.OrderDate;
                order.Status = model.Status;

                // Update order products
                order.OrderProducts = model.SelectedProducts.Select(sp => new OrderProduct
                {
                    OrderProductID = sp.OrderProductID,
                    ProductID = sp.ProductID,
                    Quantity = sp.Quantity,
                    Price = sp.Price
                }).ToList();

                order.TotalAmount = model.SelectedProducts.Sum(sp => sp.Price * sp.Quantity);

                _orderRepo.Update(order);
                _orderRepo.save();

                return RedirectToAction("Index");
            }

            model.Customers = _customerRepo.GetALL().ToList();
            model.Products = _productRepo.GetALL().ToList();
            return View(model);
        }

        // Delete an order
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var order = _orderRepo.GetById(id);
            if (order == null)
            {
                return NotFound();
            }

            _orderRepo.delete(id);
            _orderRepo.save();

            return RedirectToAction("Index");
        }

        // POST: OrderController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
