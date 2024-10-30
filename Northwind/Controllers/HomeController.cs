using NorthwindModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Northwind.Controllers
{
    public class HomeController : Controller
    {
        private const string url = "https://services.odata.org/V3/Northwind/Northwind.svc";

        [Authorize]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        public ActionResult OrderList()
        {
            try
            {
                int filterRecord = 0;
                var draw = Request.Form["draw"];
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"];
                var sortColumnDirection = Request.Form["order[0][dir]"];
                var searchValue = Request.Form["search[value]"]; // Capturing search term
                int pageSize = Convert.ToInt32(string.IsNullOrEmpty(Request.Form["length"]) ? "0" : Request.Form["length"]);
                int skip = Convert.ToInt32(string.IsNullOrEmpty(Request.Form["start"]) ? "0" : Request.Form["start"]);
                DateTime startDate = DateTime.ParseExact(Request["startDate"], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                DateTime endDate = DateTime.ParseExact(Request["endDate"], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                NorthwindEntities northwind = new NorthwindEntities(new Uri(url));
                var rawData = northwind.Orders;
                rawData.Expand("Customer");
                rawData.Expand("Order_Details");

                // Filter data by date range and apply Select to bring data into memory
                var data = rawData
                    .Where(w => w.OrderDate >= startDate && w.OrderDate <= endDate)
                    .Select(s => new
                    {
                        s.OrderID,
                        s.OrderDate,
                        s.Customer.CompanyName,
                        s.Customer.Phone,
                        s.ShipCity,
                        TotalPrice = s.Order_Details.Select(od => (od.UnitPrice * od.Quantity)).Sum()
                    })
                    .ToList();

                int totalRecord = data.Count();

                // Apply search filter using Contains (similar to SQL LIKE) for CompanyName and ShipCity
                if (!string.IsNullOrEmpty(searchValue))
                {
                    var lowerSearchValue = searchValue.ToLower();
                    data = data.Where(w =>
                        (w.CompanyName != null && w.CompanyName.ToLower().Contains(lowerSearchValue)) ||
                        (w.ShipCity != null && w.ShipCity.ToLower().Contains(lowerSearchValue))
                    ).ToList();
                }

                filterRecord = data.Count();

                // Apply sorting based on sortColumn and sortColumnDirection
                if (!string.IsNullOrEmpty(sortColumn))
                {
                    data = sortColumnDirection == "asc"
                        ? data.OrderBy(d => GetPropertyValue(d, sortColumn)).ToList()
                        : data.OrderByDescending(d => GetPropertyValue(d, sortColumn)).ToList();
                }

                // Apply pagination
                data = data.Skip(skip).Take(pageSize).ToList();

                return Json(new
                {
                    draw,
                    recordsTotal = totalRecord,
                    recordsFiltered = filterRecord,
                    data
                });
            }
            catch (Exception excp)
            {
                return Json(new
                {
                    status = "Error",
                    message = excp.Message
                });
            }
        }

        // Helper function to get property value by name using reflection
        private static object GetPropertyValue<T>(T obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName)?.GetValue(obj, null);
        }



        [HttpPost]
        [Authorize]
        public ActionResult ShipCity(string start, string end)
        {
            try
            {
                NorthwindEntities northwind = new NorthwindEntities(new Uri(url));
                DateTime startDate = DateTime.ParseExact(start, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                DateTime endDate = DateTime.ParseExact(end, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                // Fetch data into a list first to allow in-memory GroupBy
                var orderData = northwind.Orders
                    .Where(w => w.OrderDate >= startDate && w.OrderDate <= endDate)
                    .Select(s => new
                    {
                        s.ShipCity,
                        s.OrderID
                    })
                    .ToList();

                // Apply GroupBy in-memory on the list
                var data = orderData
                    .GroupBy(g => g.ShipCity)
                    .Select(s => new
                    {
                        ShipCity = s.Key,
                        Qty = s.Count()
                    })
                    .ToList();

                return Json(new
                {
                    status = "OK",
                    data
                });
            }
            catch (Exception excp)
            {
                return Json(new
                {
                    status = "Error",
                    message = excp.Message
                });
            }
        }



        public class ProductTransaction
        {
            public int? CategoryID { get; set; }
            public string CategoryName { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }

        [HttpPost]
        [Authorize]
        public ActionResult Sales(string start, string end)
        {
            try
            {
                NorthwindEntities northwind = new NorthwindEntities(new Uri(url));
                DateTime startDate = DateTime.ParseExact(start, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                DateTime endDate = DateTime.ParseExact(end, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                var dataOrder = northwind.Order_Details
                    .Where(w => w.Order.OrderDate >= startDate && w.Order.OrderDate <= endDate)
                    .Select(s => new ProductTransaction
                    {
                        CategoryID = s.Product.CategoryID,
                        CategoryName = s.Product.Category.CategoryName ?? "Other Category",
                        Quantity = s.Quantity,
                        Price = s.UnitPrice * s.Quantity
                    })
                    .ToList();

                var data = dataOrder.GroupBy(g => g.CategoryID)
                    .Select(s => new
                    {
                        CategoryName = s.First().CategoryName,
                        Quantity = s.Sum(u => u.Quantity),
                        TotalPrice = s.Sum(u => u.Price)
                    })
                    .ToList();

                return Json(new
                {
                    status = "OK",
                    data
                });
            }
            catch (Exception excp)
            {
                return Json(new
                {
                    status = "Error",
                    message = excp.Message
                });
            }
        }

    }
}
