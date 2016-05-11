#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.SelfHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace MyWebTests
{
    /*
     PROOF : THAT ASYNC WEB API IS FASTER (>15X) THEN SYNC WEB API
     Using Owin & Web API
     */
    [TestClass]
    public class UnitTest1
    {
        private const int TotalNumberOfRequests = 1000;
        private const int TotalServiceProcessingTimeMilliseconds = 400;

        [TestMethod]
        public void TestMySyncWeb()
        {
            TestMyWeb("http://localhost:8098/", "sync");
        }

        [TestMethod]
        public void TestMyASyncWeb()
        {
            TestMyWeb("http://localhost:8099/", "async");
        }
        [TestMethod]
        public void TestMyASyncWeb1()
        {
            TestMyWeb("http://localhost:8079/", "async1");
        }
        [TestMethod]
        public void TestMyASyncWeb2()
        {
            TestMyWeb("http://localhost:8089/", "async2");
        }

        public void TestMyWeb(string endpoint, string action)
        {
            Helper = new TestHelper(endpoint);

            Helper.StartWebApiServer(endpoint, () =>
            {
                //initial request, to warm up the server
                var result = Helper.ListAllProducts(action).Result;
                Console.WriteLine("First Id :" + result.ToList().First());

                Helper.Profile(() =>
                {
                    var tasks = new List<Task>();
                    for (var i = 0; i < TotalNumberOfRequests; i++)
                    {
                        tasks.Add(Helper.ListAllProducts(action));
                    }
                    Task.WaitAll(tasks.ToArray());
                });
            });
        }
    /*
     * packages.config
            <packages>
             <package id="Microsoft.AspNet.WebApi.Client" version="5.2.3" targetFramework="net452" />
             <package id="Microsoft.AspNet.WebApi.Core" version="5.2.3" targetFramework="net452" />
             <package id="Microsoft.AspNet.WebApi.SelfHost" version="5.2.3" targetFramework="net452" />
             <package id="Newtonsoft.Json" version="6.0.4" targetFramework="net452" />
           </packages>
    */
        #region SetUps

        private TestHelper Helper { set; get; }

        public class MyApplication
        {
            public class Product
            {
                public int Id { get; set; }
            }

            public class ProductAdvanced
            {
                public int ProductId { get; set; }
            }

            public class ProductsFactory
            {
                public static Product[] GetProducts()
                {
                    return new[]
                    {
                        new Product {Id = 1}
                    };
                }
                public static ProductAdvanced[] GetProductsAdvanced()
                {
                    return new[]
                    {
                        new ProductAdvanced {ProductId = 1}
                    };
                }

            }

            public class ProductService
            {
                public async Task<Product[]> Load()
                {
                    return await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProducts());
                }
                public async Task<ProductAdvanced> LoadFirstAdvanced()
                {
                    return await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductsAdvanced().FirstOrDefault());
                }

                public async Task<ProductAdvanced[]> LoadMany()
                {
                    var products= await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProducts());

                    var result=new List<ProductAdvanced>();
                    foreach (var product in products)
                    {
                         result.Add(await LoadFirstAdvanced());
                    }

                    return result.ToArray();
                }

                public async Task<ProductAdvanced[]> LoadMany2()
                {
                    var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProducts());
                    return await Task.WhenAll(products.AsParallel().Select(async (p) => await LoadFirstAdvanced()));
                }
            }

            public class ProductsController : ApiController
            {
                [HttpGet]
                public IEnumerable<Product> Sync()
                {
                    return new ProductService().Load().Result;
                }

                [HttpGet]
                public async Task<IEnumerable<Product>> Async()
                {
                    return await new ProductService().Load();
                }

                [HttpGet]
                public async Task<ProductAdvanced[]> Async1()
                {
                    return await new ProductService().LoadMany();
                }

                [HttpGet]
                public async Task<ProductAdvanced[]> Async2()
                {
                    return await new ProductService().LoadMany2();
                }
            }
        }

        public class TestHelper
        {
            public TestHelper(string endpoint)
            {
                Client = new HttpClient() {BaseAddress = new Uri(endpoint)};
            }

            private HttpClient Client { set; get; }

            public async Task<IEnumerable<object>> ListAllProducts(string action)
            {
                var result = await Client.GetAsync("api/products/" + action);
                result.EnsureSuccessStatusCode();
                var products = result.Content.ReadAsAsync<IEnumerable<object>>().Result;
                return products;
            }

            public void Profile(Action operation)
            {
                var start = DateTime.Now;
                operation();
                var end = DateTime.Now;
                var duration = (end - start).TotalMilliseconds;
                Console.WriteLine("Operation took " + duration + " ms");
            }

            public void StartWebApiServer(string endpoint, Action operation)
            {
                var config = new HttpSelfHostConfiguration(endpoint);
                config.Routes.MapHttpRoute("API Default", "api/{controller}/{action}/{id}", new {id = RouteParameter.Optional});
                using (var server = new HttpSelfHostServer(config))
                {
                    server.OpenAsync().Wait();
                    operation();
                }
            }
        }

        #endregion
    }
}
